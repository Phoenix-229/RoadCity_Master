// The low quality road surface without the polygon offset. `Queue Geometry+10` and `LOD 200` are kept from the
// Built-in original; the offset is the only thing the twin ever dropped, and under URP that makes it the shader for a
// distant road that has nothing laid over it. Shades through `Include/RoadSurfaceForwardPass.hlsl` like the rest.
Shader "RoadCityMaster/DX11/Road Shader Low Quality"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 1.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        _SpecColor("Specular Colour", Color) = (0.2,0.2,0.2,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0
        _BumpScale("Normal Scale", Float) = 1.0
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
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
            "Queue" = "Geometry+10"
        }
        LOD 200

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

            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1

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
