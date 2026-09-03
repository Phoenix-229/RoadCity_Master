#ifndef ROADCITYMASTER_ROAD_SURFACE_INPUT_INCLUDED
#define ROADCITYMASTER_ROAD_SURFACE_INPUT_INCLUDED

// The material constants and the SurfaceData builder shared by every shader in this folder.
//
// One include instead of ten copies. The ten `.shader` files above this folder differ only in the things ShaderLab
// owns and HLSL cannot express -- polygon offset, render queue, blend state, cull -- so those stay in the `.shader`
// files and everything a fragment actually computes lives here. Adding a property here reaches all ten at once, which
// is the whole reason RM-68 prefers one shared surface over a shader per road part.
//
// Property names are URP's, not Built-in's: `_BaseMap` and `_BaseColor` carry `[MainTexture]` and `[MainColor]` in
// every `.shader` that includes this file, so `Material.mainTextureOffset` (the traffic-light atlas shift in
// `TrafficLightController`) and `Material.mainTextureScale` (the lane-marking tiling in `Road`) resolve to them the
// same way they resolve on URP Lit. Renaming them to `_MainTex` would match the old Built-in shaders and break the
// materials, which are on URP Lit.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _SpecColor;
    half4  _EmissionColor;
    half   _Cutoff;
    half   _Smoothness;
    half   _Metallic;
    half   _BumpScale;
    half   _OcclusionStrength;
    half   _Surface;
CBUFFER_END

// `_ShadowIntensity` belonged to the Built-in transparent-with-shadows workaround: a second additive pass that drew
// the directional light's attenuation as black alpha, because Built-in transparents received no shadows. URP shades
// transparents in the same forward pass as opaques and receives real shadows there, so the second pass is gone and
// this stays only as the strength of the shadow term on a transparent surface -- 1 is physically correct and is the
// default; lower values are the artistic dimming the old shader hard-coded at 0.6.
half _ShadowIntensity;

TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);

// AUTHORED. Asphalt and concrete are dielectrics: metallic 0. Smoothness is the one number a road surface really
// needs, and the Built-in shaders took it from the albedo's alpha channel (`o.Gloss = tex.a`) rather than a constant.
// A shader that defines `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` keeps that behaviour and treats `_Smoothness` as the
// scale on it, which is what URP's own Lit does for the same keyword; one that does not, uses `_Smoothness` flat.
void BuildRoadSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));

    surfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    surfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

#ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
    surfaceData.smoothness = albedoAlpha.a * _Smoothness;
#else
    surfaceData.smoothness = _Smoothness;
#endif

#ifdef _SPECULAR_SETUP
    surfaceData.metallic = half(1.0);
    surfaceData.specular = _SpecColor.rgb;
#else
    surfaceData.metallic = _Metallic;
    surfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    surfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#ifdef _OCCLUSIONMAP
    half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
    surfaceData.occlusion = LerpWhiteTo(occ, _OcclusionStrength);
#else
    surfaceData.occlusion = half(1.0);
#endif

    surfaceData.clearCoatMask = half(0.0);
    surfaceData.clearCoatSmoothness = half(0.0);
}

// URP's shipped `Meta` pass -- the one the lightmapper runs to extract albedo and emission -- calls a function of this
// exact name, the way its shipped `ShadowCaster` and `DepthOnly` passes call `Alpha` and `SampleAlbedoAlpha`. Providing
// it means these shaders bake into GI through URP's own code path instead of a hand-written copy of it, and a road or
// a marking marked Contribute GI contributes its real albedo rather than nothing.
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    BuildRoadSurfaceData(uv, surfaceData);
}

#endif
