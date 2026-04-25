// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed. you can use this code freely.

// UvProjector_UnityPlane_Debug.cs
// Unity標準Plane(10x10, local XZ) を基準に、ワールド座標をPlane座標へ投影してUV化するための補助。
// ShaderGraph側は：
//   d = PositionWS - _UvPlaneOriginWS
//   Uraw = dot(d, _UvPlaneRightWS)
//   Vraw = dot(d, _UvPlaneUpWS)   // ※ここは plane.forward を入れる（V方向）
// を作って、既存の _U_min/_U_MAX/_V_min/_V_MAX の Remap に接続する想定。
// さらに、C#側でPlane方向オフセットやGizmo可視化、値の適用先(MPB / Material)切替が可能。

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UvProjector_UnityPlane_Debug : MonoBehaviour
{
    public enum ApplyMode
    {
        MaterialPropertyBlock, // Renderer単位で上書き（推奨）
        Material               // Materialに直接書き込み（デバッグ/固定用途）
    }

    [Header("基準Plane（Unity標準Plane）")]
    public Transform plane;

    [Header("投影先（値を書き込むRenderer）")]
    public Renderer targetRenderer;

    [Header("値の適用方法")]
    public ApplyMode applyMode = ApplyMode.MaterialPropertyBlock;

    [Tooltip("Material直書き時に使うMaterial（空なら targetRenderer.sharedMaterial）")]
    public Material targetMaterial;

    [Header("Unity標準Planeのメッシュは10x10")]
    public float unityPlaneMeshSize = 10f;

    [Header("Plane座標系でのオフセット（ワールド単位）")]
    [Tooltip("U方向(plane.right)へずらす")]
    public float offsetU = 0f;

    [Tooltip("V方向(plane.forward)へずらす")]
    public float offsetV = 0f;

    [Tooltip("法線方向(plane.up)へずらす（面を押し出す）")]
    public float offsetNormal = 0f;

    [Header("Gizmo表示")]
    public bool drawGizmos = true;

    [Tooltip("法線方向に箱を押し出す（枠の立体表示）")]
    public bool drawNormalExtrude = true;

    [Tooltip("法線方向に箱を押し出す長さ")]
    public float normalExtrudeLength = 0.5f;

    [Tooltip("投影テスト点（未指定なら targetRenderer.bounds.center）")]
    public Transform debugPoint;

    [Header("既存(min/max)のプロパティ名（あなたのグラフに合わせる）")]
    public string propUmin = "_U_min";
    public string propUmax = "_U_MAX";
    public string propVmin = "_V_min";
    public string propVmax = "_V_MAX";

    [Header("追加プロパティ名（ShaderGraphに追加）")]
    public string propOrigin = "_UvPlaneOriginWS";
    public string propRight  = "_UvPlaneRightWS";
    public string propUp     = "_UvPlaneUpWS"; // 中身は plane.forward を入れる（V方向）

    int _idUmin, _idUmax, _idVmin, _idVmax;
    int _idOrigin, _idRight, _idUp;

    void OnEnable() => CacheIds();
    void OnValidate() => CacheIds();

    void CacheIds()
    {
        _idUmin = Shader.PropertyToID(propUmin);
        _idUmax = Shader.PropertyToID(propUmax);
        _idVmin = Shader.PropertyToID(propVmin);
        _idVmax = Shader.PropertyToID(propVmax);

        _idOrigin = Shader.PropertyToID(propOrigin);
        _idRight  = Shader.PropertyToID(propRight);
        _idUp     = Shader.PropertyToID(propUp);
    }

    void LateUpdate()
    {
        if (!plane || !targetRenderer) return;

        ComputePlaneBasisAndSize(
            out Vector3 origin,
            out Vector3 uDir,
            out Vector3 vDir,
            out Vector3 nDir,
            out float width,
            out float height
        );

        // Remap用 min/max：Plane中心原点の距離（dot結果）に対して ±half
        float uMin = -width * 0.5f;
        float uMax = +width * 0.5f;
        float vMin = -height * 0.5f;
        float vMax = +height * 0.5f;

        Apply(
            origin, uDir, vDir,
            uMin, uMax, vMin, vMax
        );
    }

    void ComputePlaneBasisAndSize(
        out Vector3 origin,
        out Vector3 uDir,
        out Vector3 vDir,
        out Vector3 nDir,
        out float width,
        out float height
    )
    {
        // Unity標準Plane: local XZ
        uDir = plane.right.normalized;    // U方向（local X）
        vDir = plane.forward.normalized;  // V方向（local Z） ※propUpに入れる
        nDir = plane.up.normalized;       // 法線（local Y）

        // サイズ（10x10 * scale.x/z）
        Vector3 s = plane.lossyScale;
        width  = unityPlaneMeshSize * Mathf.Abs(s.x);
        height = unityPlaneMeshSize * Mathf.Abs(s.z);

        // origin：Plane中心 + Plane座標系オフセット
        origin =
            plane.position +
            uDir * offsetU +
            vDir * offsetV +
            nDir * offsetNormal;
    }

    void Apply(
        Vector3 origin, Vector3 uDir, Vector3 vDir,
        float uMin, float uMax, float vMin, float vMax
    )
    {
        if (applyMode == ApplyMode.Material)
        {
            Material mat = targetMaterial ? targetMaterial : targetRenderer.sharedMaterial;
            if (!mat) return;

            mat.SetVector(_idOrigin, origin);
            mat.SetVector(_idRight,  uDir);
            mat.SetVector(_idUp,     vDir);

            mat.SetFloat(_idUmin, uMin);
            mat.SetFloat(_idUmax, uMax);
            mat.SetFloat(_idVmin, vMin);
            mat.SetFloat(_idVmax, vMax);
        }
        else
        {
            var mpb = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(mpb);

            mpb.SetVector(_idOrigin, origin);
            mpb.SetVector(_idRight,  uDir);
            mpb.SetVector(_idUp,     vDir);

            mpb.SetFloat(_idUmin, uMin);
            mpb.SetFloat(_idUmax, uMax);
            mpb.SetFloat(_idVmin, vMin);
            mpb.SetFloat(_idVmax, vMax);

            targetRenderer.SetPropertyBlock(mpb);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos || !plane) return;

        ComputePlaneBasisAndSize(
            out Vector3 origin,
            out Vector3 uDir,
            out Vector3 vDir,
            out Vector3 nDir,
            out float width,
            out float height
        );

        // 四隅（中心から ±halfU ±halfV）
        Vector3 hu = uDir * (width * 0.5f);
        Vector3 hv = vDir * (height * 0.5f);

        Vector3 p00 = origin - hu - hv;
        Vector3 p10 = origin + hu - hv;
        Vector3 p11 = origin + hu + hv;
        Vector3 p01 = origin - hu + hv;

        // 枠線（平面）
        Gizmos.DrawLine(p00, p10);
        Gizmos.DrawLine(p10, p11);
        Gizmos.DrawLine(p11, p01);
        Gizmos.DrawLine(p01, p00);

        // 中心十字
        Gizmos.DrawLine(origin - hu * 0.1f, origin + hu * 0.1f);
        Gizmos.DrawLine(origin - hv * 0.1f, origin + hv * 0.1f);

        // 法線方向へ箱っぽく押し出し
        if (drawNormalExtrude)
        {
            Vector3 e = nDir * normalExtrudeLength;

            Gizmos.DrawLine(p00, p00 + e);
            Gizmos.DrawLine(p10, p10 + e);
            Gizmos.DrawLine(p11, p11 + e);
            Gizmos.DrawLine(p01, p01 + e);

            Gizmos.DrawLine(p00 + e, p10 + e);
            Gizmos.DrawLine(p10 + e, p11 + e);
            Gizmos.DrawLine(p11 + e, p01 + e);
            Gizmos.DrawLine(p01 + e, p00 + e);
        }

        // 投影テスト点：どこに転写されてるか
        Vector3 testPos;
        if (debugPoint) testPos = debugPoint.position;
        else if (targetRenderer) testPos = targetRenderer.bounds.center;
        else testPos = origin;

        // Plane座標へ投影（dot）
        Vector3 d = testPos - origin;
        float u = Vector3.Dot(d, uDir);
        float v = Vector3.Dot(d, vDir);

        // 0-1（中心原点なので +0.5）
        float u01 = (width  > 1e-6f) ? (u / width)  + 0.5f : 0.5f;
        float v01 = (height > 1e-6f) ? (v / height) + 0.5f : 0.5f;

        // 枠内に復元した点（表示用）
        Vector3 projected = origin + uDir * ((u01 - 0.5f) * width) + vDir * ((v01 - 0.5f) * height);

        Gizmos.DrawSphere(projected, 0.05f);
        Gizmos.DrawLine(testPos, projected);

        Handles.Label(projected, $"UV01=({u01:F3}, {v01:F3})");
    }
#endif
}
