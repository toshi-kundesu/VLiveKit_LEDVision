#ifndef TOSHI_LTCGI_RECEIVE_DEBUG_NODE_INCLUDED
#define TOSHI_LTCGI_RECEIVE_DEBUG_NODE_INCLUDED

#ifndef UNITY_PI
    #define UNITY_PI 3.14159265359
#endif

#ifndef UNITY_HALF_PI
    #define UNITY_HALF_PI 1.57079632679
#endif

#ifndef UNITY_TWO_PI
    #define UNITY_TWO_PI 6.28318530718
#endif

#ifndef LTCGI_API_V1
    #define LTCGI_API_V1
#endif

float3 DecodeLightmap(float4 encodedIlluminance)
{
#if defined(UNITY_LIGHTMAP_DLDR_ENCODING)
    return encodedIlluminance.rgb * 2.0;
#else
    return encodedIlluminance.rgb;
#endif
}

#include "Assets/toshi.VLiveKit/LEDVision/LTCGI/_pi_/_LTCGI/Shaders/LTCGI.cginc"

void LTCGIReceiveDebug_float(
    float3 AbsoluteWorldPos,
    float3 WorldNormal,
    float3 CameraPos,
    float2 LightmapUV,
    float Roughness,
    float DebugMode,
    float Boost,
    float LTCGIEnabled,
    out float3 OutColor
)
{
    OutColor = 0;

    if (LTCGIEnabled <= 0.5)
    {
        return;
    }

    half3 diff = 0;
    half3 spec = 0;

    float3 N = normalize(WorldNormal);
    float3 V = normalize(CameraPos - AbsoluteWorldPos);

    LTCGI_Contribution(
        AbsoluteWorldPos,
        N,
        V,
        Roughness,
        LightmapUV,
        diff,
        spec
    );

    if (DebugMode < 0.5)
    {
        OutColor = diff * Boost;
    }
    else if (DebugMode < 1.5)
    {
        OutColor = spec * Boost;
    }
    else
    {
        OutColor = (diff + spec) * Boost;
    }
}

void LTCGIReceiveDebug_half(
    half3 AbsoluteWorldPos,
    half3 WorldNormal,
    half3 CameraPos,
    half2 LightmapUV,
    half Roughness,
    half DebugMode,
    half Boost,
    half LTCGIEnabled,
    out half3 OutColor
)
{
    float3 outColorFloat = 0;

    LTCGIReceiveDebug_float(
        (float3)AbsoluteWorldPos,
        (float3)WorldNormal,
        (float3)CameraPos,
        (float2)LightmapUV,
        (float)Roughness,
        (float)DebugMode,
        (float)Boost,
        (float)LTCGIEnabled,
        outColorFloat
    );

    OutColor = (half3)outColorFloat;
}

#endif