Shader "Unlit/LTCGI_ReceiveDebug"
{
    Properties
    {
        [ToggleUI] _LTCGI ("LTCGI enabled", Float) = 1.0
        _Roughness ("Roughness", Range(0,1)) = 0.5
        [Enum(Diffuse,0, Specular,1, Sum,2)] _DebugMode ("Debug Mode", Float) = 2
        _Boost ("Boost", Range(0.0, 50.0)) = 5.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        LOD 100

        Pass
        {
            Name "LTCGI_ReceiveDebug"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // ★ ここが重要：Built-in寄りに戻す
            #include "UnityCG.cginc"

            // 必須マクロ（LTCGI側依存）
            #ifndef UNITY_PI
                #define UNITY_PI 3.14159265359
            #endif

            #ifndef UNITY_HALF_PI
                #define UNITY_HALF_PI 1.57079632679
            #endif

            #ifndef UNITY_TWO_PI
                #define UNITY_TWO_PI 6.28318530718
            #endif

            // DecodeLightmap互換（SRP対策）
            float3 DecodeLightmap(float4 encodedIlluminance)
            {
            #if defined(UNITY_LIGHTMAP_DLDR_ENCODING)
                return encodedIlluminance.rgb * 2.0;
            #else
                return encodedIlluminance.rgb;
            #endif
            }

            #define LTCGI_API_V1
            #include "Assets/toshi.VLiveKit/LEDVision/LTCGI/_pi_/_LTCGI/Shaders/LTCGI.cginc"

            float _LTCGI;
            float _Roughness;
            float _DebugMode;
            float _Boost;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv2    : TEXCOORD1;
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

            v2f vert(appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);

                float4 wpos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wpos.xyz;

                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.lmuv = v.uv2;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // LTCGI off
                if (_LTCGI == 0)
                    return float4(0,0,0,1);

                // ★ デバッグ：まずここを見る
                // return float4(_Udon_LTCGI_GlobalEnable.xxx, 1);

                half3 diff = 0;
                half3 spec = 0;

                float3 V = normalize(get_camera_pos() - i.worldPos);
                float3 N = normalize(i.worldN);

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
                else                      outCol = diff + spec;

                outCol *= _Boost;

                return float4(outCol, 1);
            }

            ENDHLSL
        }
    }
}