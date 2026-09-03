// The cutout marker: a lane marking, arrow, stop line or sign face stamped onto a surface it is coplanar with, with
// its shape cut out of the texture's alpha rather than modelled.
//
// Ported from a copy of Built-in's Standard shader (`RenderType Cutout`, `Offset -3,-2`, the full metallic property
// set, `UNITY_SETUP_BRDF_INPUT MetallicSetup`). Two things about it are load bearing and both are kept:
//
//   * `Offset -3,-2` -- three depth units towards the camera, a much stronger bias than the road surface's one. A
//     marking is laid ON the road, so it has to win the depth test against a surface it shares a plane with along a
//     curve, at every camera angle, at every distance. This is the case RM-68 names as justifying a custom shader
//     outright: "markings that must not swim through curves".
//   * alpha clipping, always on. `_ALPHATEST_ON` is defined rather than exposed as a material keyword, because a
//     marker material with alpha clipping switched off is a black rectangle around every arrow -- not a valid state.
//
// `AlphaToMask On` is URP's own answer to the hard edge alpha clipping leaves: it resolves the clip against the MSAA
// coverage mask, so a stop line drawn at a grazing angle keeps a straight edge instead of a stair. Built-in's copy of
// Standard had no equivalent.
Shader "RoadCityMaster/CutoutMarker"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Toggle(_NORMALMAP)] _NormalMapToggle("Use Normal Map", Float) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Toggle(_EMISSION)] _EmissionToggle("Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Colour", Color) = (0,0,0,1)

        [Toggle(_OCCLUSIONMAP)] _OcclusionToggle("Use Occlusion Map", Float) = 0.0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [Toggle(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlightsOff("Specular Highlights Off", Float) = 0.0
        [Toggle(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflectionsOff("Environment Reflections Off", Float) = 0.0

        _SpecColor("Specular Colour", Color) = (0.2,0.2,0.2,1)
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0

        [HideInInspector] _Surface("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "AlphaTest"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Offset -3, -2
            ZWrite On
            AlphaToMask On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RoadSurfaceVertex
            #pragma fragment RoadSurfaceFragment

            #define _ALPHATEST_ON 1

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #include_with_pragmas "Include/RoadSurfaceForwardPragmas.hlsl"

            #include "Include/RoadSurfaceForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // The clip has to happen in the shadow pass too, or the arrow casts the shadow of its bounding quad.
            #define _ALPHATEST_ON 1

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Offset -3, -2
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #define _ALPHATEST_ON 1

            #pragma multi_compile_instancing

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Offset -3, -2
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #define _ALPHATEST_ON 1

            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_instancing

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // Clipped here too, so the lightmapper bakes the arrow and not the quad it sits on.
            #define _ALPHATEST_ON 1

            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _EMISSION

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
