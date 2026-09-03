// The main road surface. Asphalt or concrete with an optional normal map, and the one thing a URP Lit material
// cannot do: a polygon offset that pulls the road a hair towards the camera so a coplanar lane marking, kerb inlay
// or intersection plate laid on top of it never z-fights along a curve.
//
// Ported from Built-in ShaderLab (`#pragma surface surf BlinnPhong`, `Offset -1,-1`, `FallBack "Diffuse"`) to URP.
// The surface maths is not restated here: it lives in `Include/RoadSurfaceInput.hlsl` and
// `Include/RoadSurfaceForwardPass.hlsl`, shared with the other nine shaders in this folder, so this file is only the
// state ShaderLab owns -- queue, offset, cull, and which passes exist. The old `o.Gloss = tex.a` is kept by defining
// `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`: smoothness still comes from the albedo's alpha channel.
Shader "RoadCityMaster/Roads/Road Shader"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 1.0
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

        // Read by `IsSurfaceTypeTransparent` in the fragment. 0 is opaque and this shader is never anything else, so
        // it is hidden rather than exposed as a switch that would need blend state the pass does not declare.
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
        LOD 400

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // The reason this shader exists instead of a URP Lit material.
            Offset -1, -1
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RoadSurfaceVertex
            #pragma fragment RoadSurfaceFragment

            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1

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

            // The offset is repeated in the depth passes on purpose: a depth prepass that wrote the un-offset road
            // would disagree with what the forward pass shaded, and every screen-space effect that reads depth --
            // SSAO, soft particles, decals -- would sample the wrong surface by exactly the offset.
            Offset -1, -1
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

            Offset -1, -1
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
