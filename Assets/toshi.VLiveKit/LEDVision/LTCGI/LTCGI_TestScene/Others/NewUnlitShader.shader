Shader "Unlit/LTCGI_ReceiveDebug"
{
    Properties
    {
        [ToggleUI] _LTCGI ("LTCGI enabled", Float) = 1.0
        _Roughness ("Roughness", Range(0,1)) = 0.5

        // 0=diff, 1=spec, 2=diff+spec
        [Enum(Diffuse,0, Specular,1, Sum,2)] _DebugMode ("Debug Mode", Float) = 2

        // 見えづらい時用のブースト（1=そのまま）
        _Boost ("Boost", Range(0.0, 50.0)) = 5.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "LTCGI"="_LTCGI" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // APIv1 で十分（diff/specを受け取るだけなので）
            #include "Assets/toshi.VLiveKit/LEDVision/LTCGI/_pi_/_LTCGI/Shaders/LTCGI.cginc"

            float _LTCGI;
            float _Roughness;
            float _DebugMode;
            float _Boost;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv2    : TEXCOORD1; // lightmap UV (uv1)
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldN   : TEXCOORD1;
                float2 lmuv     : TEXCOORD2;
            };

            float3 get_camera_pos()
            {
                float3 worldCam;
                worldCam.x = unity_CameraToWorld[0][3];
                worldCam.y = unity_CameraToWorld[1][3];
                worldCam.z = unity_CameraToWorld[2][3];
                return worldCam;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float4 wpos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wpos.xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.lmuv = v.uv2;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // LTCGI off なら真っ黒
                if (_LTCGI == 0) return 0;

                half3 diff = 0;
                half3 spec = 0;

                float3 V = normalize(get_camera_pos() - i.worldPos);
                float3 N = normalize(i.worldN);

                // APIv1（diff/specを直接受け取る）
                LTCGI_Contribution(
                    i.worldPos,
                    N,
                    V,
                    _Roughness,
                    i.lmuv,
                    diff,
                    spec
                );

                float3 outCol;
                if (_DebugMode < 0.5)      outCol = diff;
                else if (_DebugMode < 1.5) outCol = spec;
                else                       outCol = diff + spec;

                // 見やすくブースト
                outCol *= _Boost;

                return float4(outCol, 1);
            }
            ENDCG
        }
    }
}
