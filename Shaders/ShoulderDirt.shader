// The road shoulder: the dirt, gravel or grass strip the carriageway sits in.
//
// Ported from Built-in ShaderLab (`Offset -1,-1`, `LOD 400`, `FallBack "Diffuse"`). The original had its normal map,
// specular colour and gloss lines commented out and shaded from the base texture alone, so this keeps exactly that
// and does not reinstate maps the asset set never authored.
//
// The offset is kept for the same reason the road surface keeps one: a shoulder is coplanar with the road edge along
// its whole length, and a shoulder that is not pulled towards the camera stitches into it.
//
// AUTHORED: smoothness defaults to 0.15. A dry unsealed shoulder is a rough dielectric -- gravel and compacted soil
// scatter almost specularly nothing, unlike the sealed carriageway beside it, whose default is 1 scaled by its
// smoothness map. Metallic is 0 because soil, gravel and grass are dielectrics.
Shader "RoadCityMaster/Roads/Road Shoulder Shader"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.15
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Toggle(_NORMALMAP)] _NormalMapToggle("Use Normal Map", Float) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Toggle(_OCCLUSIONMAP)] _OcclusionToggle("Use Occlusion Map", Float) = 0.0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        _SpecColor("Specular Colour", Color) = (0.2,0.2,0.2,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0
        [HDR] _EmissionColor("Emission Colour", Color) = (0,0,0,1)

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

            Offset -1, -1
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RoadSurfaceVertex
            #pragma fragment RoadSurfaceFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP

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

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
