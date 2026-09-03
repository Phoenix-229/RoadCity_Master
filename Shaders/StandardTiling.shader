// The tiling standard surface: an opaque building, kerb, wall or plate whose base map repeats across the mesh
// independently of the mesh's own UV scale, driven by the material's tiling on `_BaseMap`.
//
// About the file this replaces: the Built-in `StandardTiling.shader` was a byte-for-byte copy of
// `StandardOffset.shader` -- same `RenderType Cutout`, same `Offset -3,-2`, same commented-out property block -- with
// only line 1, the shader name, different. Two shader names for one shader is the "two things fighting for the same
// action" case, and the port resolves it in favour of what each name says: `RoadCityMaster/CutoutMarker` is the
// alpha-clipped, depth-biased marking, and this one is the plain opaque tiling surface. Tiling needs no custom code at
// all in URP -- `TRANSFORM_TEX` in the shared vertex stage applies `_BaseMap_ST`, which is what the material's Tiling
// and Offset fields write to -- so this shader exists to keep the name resolvable, not because URP Lit could not do
// it. Anything authored onto it can move to a URP Lit material with no visual change.
Shader "RoadCityMaster/Standard-Tiling"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

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
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0

        [HideInInspector] _Surface("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RoadSurfaceVertex
            #pragma fragment RoadSurfaceFragment

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

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

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

            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _EMISSION

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
