using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace VLiveKit.LEDVision.Editor
{
    public sealed class HdrpLitToLedReceiveShaderReplacer : EditorWindow
    {
        private const string WindowTitle = "LED Receive Shader Replacer";
        private const string MenuPath = "toshi/VLiveKit/LEDVision/HDRP Lit To LED Receive Shader Replacer";
        private const string AssetsMenuPath = "Assets/toshi/VLiveKit/LEDVision/Replace HDRP Lit Shaders With LED Receive";
        private const string AssetsApplyBoostMenuPath = "Assets/toshi/VLiveKit/LEDVision/Apply LED Receive Boost";
        private const string DefaultLedReceiveMaterialGuid = "5936e4c900a1e454e8c55aa685518ef0";
        private const string UndoName = "Replace HDRP Lit Shader With LED Receive";
        private const string BoostUndoName = "Apply LED Receive Boost";
        private const string LtcgiPropertyName = "_LTCGI";
        private const string LegacyLtcgiEnabledPropertyName = "_LTCGIEnabled";
        private const string UdonLtcgiGlobalEnablePropertyName = "_Udon_LTCGI_Global_Enable";
        private const string BoostPropertyName = "_LTCGI_Boost";
        private const string DebugModePropertyName = "_DebugMode";
        private const float DefaultLedReceiveBoost = 100f;
        private const float DefaultDebugMode = 2f;

        private static readonly string[] HdrpLitShaderNames =
        {
            "HDRP/Lit",
            "HDRenderPipeline/Lit",
        };

        [SerializeField] private DefaultAsset targetFolder;
        [SerializeField] private Shader ledReceiveShader;
        [SerializeField] private float ledReceiveBoost = DefaultLedReceiveBoost;
        [SerializeField] private bool applyBoostAfterReplace = true;

        private readonly List<string> matchedMaterialPaths = new List<string>();
        private readonly List<string> convertedMaterialPaths = new List<string>();
        private readonly List<string> boostableMaterialPaths = new List<string>();
        private readonly List<string> skippedMaterialPaths = new List<string>();
        private Vector2 scrollPosition;
        private string lastScanFolderPath = "";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<HdrpLitToLedReceiveShaderReplacer>(WindowTitle);
            window.minSize = new Vector2(520, 340);
            window.TryUseSelectedFolder();
        }

        [MenuItem(AssetsMenuPath, false, 2100)]
        private static void ReplaceSelectedFolderShaders()
        {
            if (!TryGetSelectedFolderPath(out var folderPath))
            {
                EditorUtility.DisplayDialog(WindowTitle, "Select a target folder in the Project window.", "OK");
                return;
            }

            var targetShader = LoadDefaultLedReceiveShader();
            if (targetShader == null)
            {
                EditorUtility.DisplayDialog(WindowTitle, "The default LEDReceive shader was not found.", "OK");
                return;
            }

            var scanResult = ScanMaterials(folderPath, targetShader);
            if (scanResult.MatchedCount == 0)
            {
                EditorUtility.DisplayDialog(WindowTitle, "No HDRP/Lit materials were found under the target folder.", "OK");
                return;
            }

            var message = $"{folderPath}\n\nReplace the shader on {scanResult.MatchedCount} HDRP/Lit materials with {targetShader.name}, then apply LED receive settings, _DebugMode = {DefaultDebugMode}, and _LTCGI_Boost = {DefaultLedReceiveBoost}?";
            if (!EditorUtility.DisplayDialog(WindowTitle, message, "Replace", "Cancel"))
            {
                return;
            }

            var result = ReplaceShadersInFolder(folderPath, targetShader, true, DefaultLedReceiveBoost);
            Debug.Log(result.ToLogMessage(folderPath, targetShader));
        }

        [MenuItem(AssetsMenuPath, true)]
        private static bool ValidateReplaceSelectedFolderShaders()
        {
            return TryGetSelectedFolderPath(out _);
        }

        [MenuItem(AssetsApplyBoostMenuPath, false, 2101)]
        private static void ApplyBoostToSelectedFolderMaterials()
        {
            if (!TryGetSelectedFolderPath(out var folderPath))
            {
                EditorUtility.DisplayDialog(WindowTitle, "Select a target folder in the Project window.", "OK");
                return;
            }

            var scanResult = ScanMaterials(folderPath, null);
            if (scanResult.BoostableCount == 0)
            {
                EditorUtility.DisplayDialog(WindowTitle, "No materials with _LTCGI_Boost were found under the target folder.", "OK");
                return;
            }

            var message = $"{folderPath}\n\nApply LED receive settings, _DebugMode = {DefaultDebugMode}, and _LTCGI_Boost = {DefaultLedReceiveBoost} to {scanResult.BoostableCount} materials?";
            if (!EditorUtility.DisplayDialog(WindowTitle, message, "Apply", "Cancel"))
            {
                return;
            }

            var result = ApplyBoostInFolder(folderPath, DefaultLedReceiveBoost);
            Debug.Log(result.ToLogMessage(folderPath, DefaultLedReceiveBoost));
        }

        [MenuItem(AssetsApplyBoostMenuPath, true)]
        private static bool ValidateApplyBoostToSelectedFolderMaterials()
        {
            return TryGetSelectedFolderPath(out _);
        }

        private void OnEnable()
        {
            if (ledReceiveShader == null)
            {
                ledReceiveShader = LoadDefaultLedReceiveShader();
            }

            if (targetFolder == null)
            {
                TryUseSelectedFolder();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HDRP Lit To LED Receive", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scans Material assets under the selected folder, including descendant folders. HDRP/Lit materials can be converted to the LEDReceive shader, and LED receive settings plus _DebugMode = 2 and _LTCGI_Boost can be applied in bulk to already-converted materials.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
                if (GUILayout.Button("Use Selection", GUILayout.Width(110)))
                {
                    TryUseSelectedFolder();
                }
            }

            ledReceiveShader = (Shader)EditorGUILayout.ObjectField("LEDReceive Shader", ledReceiveShader, typeof(Shader), false);
            ledReceiveBoost = Mathf.Max(0f, EditorGUILayout.FloatField("LTCGI Boost", ledReceiveBoost));
            applyBoostAfterReplace = EditorGUILayout.ToggleLeft("Apply this boost when replacing shaders", applyBoostAfterReplace);

            var folderPath = GetFolderPath(targetFolder);
            var hasValidFolder = !string.IsNullOrEmpty(folderPath);
            var hasTargetShader = ledReceiveShader != null;

            if (!hasValidFolder)
            {
                EditorGUILayout.HelpBox("Select a folder from the Project window.", MessageType.Warning);
            }

            if (!hasTargetShader)
            {
                EditorGUILayout.HelpBox("Select the LEDReceive shader. The default is read from HDRP_HDRPLit_ReceiveLED.mat.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!hasValidFolder || !hasTargetShader))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Scan", GUILayout.Height(28)))
                    {
                        ScanForPreview(folderPath, ledReceiveShader);
                    }

                    using (new EditorGUI.DisabledScope(matchedMaterialPaths.Count == 0 || lastScanFolderPath != folderPath))
                    {
                        if (GUILayout.Button("Replace Shader", GUILayout.Height(28)))
                        {
                            ReplaceFromWindow(folderPath);
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!hasValidFolder))
            {
                if (GUILayout.Button("Apply Receive + Boost", GUILayout.Height(28)))
                {
                    ApplyBoostFromWindow(folderPath);
                }
            }

            DrawScanResult();
        }

        private void DrawScanResult()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Scan Result", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(lastScanFolderPath))
            {
                EditorGUILayout.HelpBox("Scan first to preview matching Material assets.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Folder", lastScanFolderPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Matched HDRP/Lit Materials", matchedMaterialPaths.Count.ToString());
            EditorGUILayout.LabelField("Already LEDReceive Materials", convertedMaterialPaths.Count.ToString());
            EditorGUILayout.LabelField("Boost-capable Materials", boostableMaterialPaths.Count.ToString());
            EditorGUILayout.LabelField("Skipped Materials", skippedMaterialPaths.Count.ToString());

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawPathList("Matched HDRP/Lit", matchedMaterialPaths);
            DrawPathList("Already LEDReceive", convertedMaterialPaths);
            DrawPathList("Boost-capable", boostableMaterialPaths);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawPathList(string label, IReadOnlyList<string> materialPaths)
        {
            if (materialPaths.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (var materialPath in materialPaths)
            {
                EditorGUILayout.LabelField(materialPath);
            }
        }

        private void ScanForPreview(string folderPath, Shader targetShader)
        {
            matchedMaterialPaths.Clear();
            convertedMaterialPaths.Clear();
            boostableMaterialPaths.Clear();
            skippedMaterialPaths.Clear();
            lastScanFolderPath = folderPath;

            var result = ScanMaterials(folderPath, targetShader);
            matchedMaterialPaths.AddRange(result.MatchedMaterialPaths);
            convertedMaterialPaths.AddRange(result.ConvertedMaterialPaths);
            boostableMaterialPaths.AddRange(result.BoostableMaterialPaths);
            skippedMaterialPaths.AddRange(result.SkippedMaterialPaths);

            ShowNotification(new GUIContent($"Matched {matchedMaterialPaths.Count}, boostable {boostableMaterialPaths.Count}"));
        }

        private void ReplaceFromWindow(string folderPath)
        {
            var boostLine = applyBoostAfterReplace ? $"\n\nAlso apply LED receive settings, _DebugMode = {DefaultDebugMode}, and _LTCGI_Boost = {ledReceiveBoost} to replaced materials." : $"\n\nLED receive settings and _DebugMode = {DefaultDebugMode} will be enabled on replaced materials.";
            var message = $"{folderPath}\n\nReplace the shader on {matchedMaterialPaths.Count} HDRP/Lit materials with {ledReceiveShader.name}?{boostLine}";
            if (!EditorUtility.DisplayDialog(WindowTitle, message, "Replace", "Cancel"))
            {
                return;
            }

            var result = ReplaceShadersInFolder(folderPath, ledReceiveShader, applyBoostAfterReplace, ledReceiveBoost);
            Debug.Log(result.ToLogMessage(folderPath, ledReceiveShader));
            ScanForPreview(folderPath, ledReceiveShader);
            ShowNotification(new GUIContent($"Replaced {result.ReplacedCount}, configured {result.ReceiveConfiguredCount}"));
        }

        private void ApplyBoostFromWindow(string folderPath)
        {
            var scanResult = ScanMaterials(folderPath, ledReceiveShader);
            if (scanResult.BoostableCount == 0)
            {
                EditorUtility.DisplayDialog(WindowTitle, "No materials with _LTCGI_Boost were found under the target folder.", "OK");
                return;
            }

            var message = $"{folderPath}\n\nApply LED receive settings, _DebugMode = {DefaultDebugMode}, and _LTCGI_Boost = {ledReceiveBoost} to {scanResult.BoostableCount} materials?";
            if (!EditorUtility.DisplayDialog(WindowTitle, message, "Apply", "Cancel"))
            {
                return;
            }

            var result = ApplyBoostInFolder(folderPath, ledReceiveBoost);
            Debug.Log(result.ToLogMessage(folderPath, ledReceiveBoost));
            ScanForPreview(folderPath, ledReceiveShader);
            ShowNotification(new GUIContent($"Configured {result.AppliedCount}, unchanged {result.UnchangedCount}"));
        }

        internal static ShaderReplaceResult ReplaceShadersInFolder(string folderPath, Shader targetShader)
        {
            return ReplaceShadersInFolder(folderPath, targetShader, true, DefaultLedReceiveBoost);
        }

        internal static ShaderReplaceResult ReplaceShadersInFolder(string folderPath, Shader targetShader, bool applyBoost, float boostValue)
        {
            var result = new ShaderReplaceResult();
            if (string.IsNullOrEmpty(folderPath) || targetShader == null)
            {
                return result;
            }

            var materialPaths = new List<string>(FindMaterialPaths(folderPath));

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var materialPath in materialPaths)
                {
                    result.ScannedCount++;

                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null || !IsHdrpLitMaterial(material) || material.shader == targetShader)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var applyResult = ReplaceShader(material, targetShader, applyBoost, boostValue);
                    if (applyResult.BoostApplied)
                    {
                        result.BoostAppliedCount++;
                    }

                    if (applyResult.ReceiveConfigured)
                    {
                        result.ReceiveConfiguredCount++;
                    }

                    if (applyResult.HdrpValidated)
                    {
                        result.HdrpValidatedCount++;
                    }

                    result.ReplacedMaterialPaths.Add(materialPath);
                    result.ReplacedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        internal static BoostApplyResult ApplyBoostInFolder(string folderPath, float boostValue)
        {
            var result = new BoostApplyResult();
            if (string.IsNullOrEmpty(folderPath))
            {
                return result;
            }

            var materialPaths = new List<string>(FindMaterialPaths(folderPath));

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var materialPath in materialPaths)
                {
                    result.ScannedCount++;

                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null || !material.HasProperty(BoostPropertyName))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var applyResult = ApplyLedReceiveSettings(material, true, boostValue);
                    if (!applyResult.Changed)
                    {
                        result.UnchangedCount++;
                        continue;
                    }

                    result.AppliedMaterialPaths.Add(materialPath);
                    result.AppliedCount++;
                    if (applyResult.ReceiveConfigured)
                    {
                        result.ReceiveConfiguredCount++;
                    }

                    if (applyResult.BoostApplied)
                    {
                        result.BoostAppliedCount++;
                    }

                    if (applyResult.HdrpValidated)
                    {
                        result.HdrpValidatedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        internal static MaterialScanResult ScanMaterials(string folderPath, Shader targetShader)
        {
            var result = new MaterialScanResult();
            if (string.IsNullOrEmpty(folderPath))
            {
                return result;
            }

            var materialPaths = FindMaterialPaths(folderPath);
            foreach (var materialPath in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    result.SkippedMaterialPaths.Add(materialPath);
                    continue;
                }

                var isTargetShader = targetShader != null && material.shader == targetShader;
                var isMatchedHdrpLit = IsHdrpLitMaterial(material) && !isTargetShader;
                var isBoostable = material.HasProperty(BoostPropertyName);

                if (isMatchedHdrpLit)
                {
                    result.MatchedMaterialPaths.Add(materialPath);
                }

                if (isTargetShader)
                {
                    result.ConvertedMaterialPaths.Add(materialPath);
                }

                if (isBoostable)
                {
                    result.BoostableMaterialPaths.Add(materialPath);
                }

                if (!isMatchedHdrpLit && !isTargetShader && !isBoostable)
                {
                    result.SkippedMaterialPaths.Add(materialPath);
                }
            }

            return result;
        }

        internal static bool IsHdrpLitMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            foreach (var shaderName in HdrpLitShaderNames)
            {
                if (string.Equals(material.shader.name, shaderName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static LedReceiveApplyResult ReplaceShader(Material material, Shader targetShader, bool applyBoost, float boostValue)
        {
            Undo.RegisterCompleteObjectUndo(material, UndoName);
            material.shader = targetShader;
            return ApplyLedReceiveSettings(material, applyBoost, boostValue);
        }

        private static LedReceiveApplyResult ApplyLedReceiveSettings(Material material, bool applyBoost, float boostValue)
        {
            var result = new LedReceiveApplyResult();
            if (material == null)
            {
                return result;
            }

            Undo.RegisterCompleteObjectUndo(material, BoostUndoName);
            result.ReceiveConfigured |= TrySetFloat(material, LtcgiPropertyName, 1f);
            result.ReceiveConfigured |= TrySetFloat(material, LegacyLtcgiEnabledPropertyName, 1f);
            result.ReceiveConfigured |= TrySetFloat(material, UdonLtcgiGlobalEnablePropertyName, 1f);
            result.ReceiveConfigured |= TrySetFloat(material, DebugModePropertyName, DefaultDebugMode);

            if (applyBoost)
            {
                result.BoostApplied = TrySetFloat(material, BoostPropertyName, boostValue);
            }

            result.HdrpValidated = HDShaderUtils.ResetMaterialKeywords(material);
            if (result.Changed)
            {
                EditorUtility.SetDirty(material);
            }

            return result;
        }

        private static bool TrySetFloat(Material material, string propertyName, float value)
        {
            if (material == null || !material.HasProperty(propertyName))
            {
                return false;
            }

            if (Mathf.Approximately(material.GetFloat(propertyName), value))
            {
                return false;
            }

            material.SetFloat(propertyName, value);
            return true;
        }

        private static IEnumerable<string> FindMaterialPaths(string folderPath)
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && string.Equals(Path.GetExtension(path), ".mat", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }
            }
        }

        private static Shader LoadDefaultLedReceiveShader()
        {
            var path = AssetDatabase.GUIDToAssetPath(DefaultLedReceiveMaterialGuid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null ? material.shader : null;
        }

        private void TryUseSelectedFolder()
        {
            if (TryGetSelectedFolderPath(out var folderPath))
            {
                targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            }
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = "";
            var selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return false;
            }

            var selectedPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return false;
            }

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                folderPath = selectedPath;
                return true;
            }

            var parentPath = Path.GetDirectoryName(selectedPath);
            if (!string.IsNullOrEmpty(parentPath))
            {
                parentPath = parentPath.Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(parentPath))
                {
                    folderPath = parentPath;
                    return true;
                }
            }

            return false;
        }

        private static string GetFolderPath(DefaultAsset folderAsset)
        {
            if (folderAsset == null)
            {
                return "";
            }

            var path = AssetDatabase.GetAssetPath(folderAsset);
            return AssetDatabase.IsValidFolder(path) ? path : "";
        }
    }

    internal sealed class MaterialScanResult
    {
        public readonly List<string> MatchedMaterialPaths = new List<string>();
        public readonly List<string> ConvertedMaterialPaths = new List<string>();
        public readonly List<string> BoostableMaterialPaths = new List<string>();
        public readonly List<string> SkippedMaterialPaths = new List<string>();

        public int MatchedCount
        {
            get { return MatchedMaterialPaths.Count; }
        }

        public int BoostableCount
        {
            get { return BoostableMaterialPaths.Count; }
        }
    }

    internal sealed class ShaderReplaceResult
    {
        public readonly List<string> ReplacedMaterialPaths = new List<string>();
        public int ScannedCount;
        public int ReplacedCount;
        public int ReceiveConfiguredCount;
        public int BoostAppliedCount;
        public int HdrpValidatedCount;
        public int SkippedCount;

        public string ToLogMessage(string folderPath, Shader targetShader)
        {
            var targetName = targetShader != null ? targetShader.name : "(null)";
            return $"LEDVision HDRP/Lit shader replacement complete. Folder: {folderPath}, target shader: {targetName}, scanned: {ScannedCount}, replaced: {ReplacedCount}, receive configured: {ReceiveConfiguredCount}, boost applied: {BoostAppliedCount}, HDRP validated: {HdrpValidatedCount}, skipped: {SkippedCount}";
        }
    }

    internal sealed class BoostApplyResult
    {
        public readonly List<string> AppliedMaterialPaths = new List<string>();
        public int ScannedCount;
        public int AppliedCount;
        public int ReceiveConfiguredCount;
        public int BoostAppliedCount;
        public int HdrpValidatedCount;
        public int UnchangedCount;
        public int SkippedCount;

        public string ToLogMessage(string folderPath, float boostValue)
        {
            return $"LEDVision receive settings apply complete. Folder: {folderPath}, _LTCGI_Boost: {boostValue}, scanned: {ScannedCount}, applied: {AppliedCount}, receive configured: {ReceiveConfiguredCount}, boost applied: {BoostAppliedCount}, HDRP validated: {HdrpValidatedCount}, unchanged: {UnchangedCount}, skipped: {SkippedCount}";
        }
    }

    internal struct LedReceiveApplyResult
    {
        public bool ReceiveConfigured;
        public bool BoostApplied;
        public bool HdrpValidated;

        public bool Changed
        {
            get { return ReceiveConfigured || BoostApplied || HdrpValidated; }
        }
    }
}
