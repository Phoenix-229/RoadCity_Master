#ifndef ROADCITYMASTER_ROAD_SURFACE_FORWARD_PASS_INCLUDED
#define ROADCITYMASTER_ROAD_SURFACE_FORWARD_PASS_INCLUDED

// The `UniversalForward` pass shared by every shader in this folder.
//
// It is deliberately the plain URP forward path: build a `SurfaceData`, build an `InputData`, hand both to
// `UniversalFragmentPBR`, mix fog. Nothing here reimplements lighting, so a URP upgrade that changes how a light is
// evaluated changes these shaders with it -- which is the maintenance argument RM-68 makes for not writing a custom
// shader at all, honoured here by keeping the custom part down to the vertex plumbing.
//
// Unlike URP's own Lit pass this one always carries `positionWS` and `tangentWS` rather than switching them on and
// off behind five interpolator macros. Two extra interpolators on a road mesh is a rounding error next to a
// per-variant `#if` maze that only ever compiles one way here.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "RoadSurfaceInput.hlsl"

struct RoadAttributes
{
    float4 positionOS       : POSITION;
    float3 normalOS         : NORMAL;
    float4 tangentOS        : TANGENT;
    float2 texcoord         : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct RoadVaryings
{
    float2 uv                     : TEXCOORD0;
    float3 positionWS             : TEXCOORD1;
    half3  normalWS               : TEXCOORD2;
    half4  tangentWS              : TEXCOORD3;   // xyz tangent, w the handedness sign
    half4  fogFactorAndVertexLight : TEXCOORD4;  // x fog, yzw per-vertex light
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
    float4 positionCS             : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

RoadVaryings RoadSurfaceVertex(RoadAttributes input)
{
    RoadVaryings output = (RoadVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = normalInput.normalWS;

    real signWS = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, signWS);

    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    return output;
}

void BuildRoadInputData(RoadVaryings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.viewDirectionWS = viewDirWS;

    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;

#ifdef _NORMALMAP
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

#if defined(DEBUG_DISPLAY)
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
#endif
}

half4 RoadSurfaceFragment(RoadVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    BuildRoadSurfaceData(input.uv, surfaceData);

    InputData inputData;
    BuildRoadInputData(input, surfaceData.normalTS, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);

    // A transparent road marking is shadowed like anything else in URP; `_ShadowIntensity` is the artistic dimming
    // the Built-in workaround hard-coded, kept as a knob and left at 1 (physical) by default.
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    return color;
}

#endif
