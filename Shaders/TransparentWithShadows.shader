// A transparent surface that both receives and casts shadows: a sign face, a glazed canopy, a shelter panel.
//
// Ported from Built-in ShaderLab, and this is the one port where a whole pass was deleted rather than translated. The
// original was two passes: a `#pragma surface surf BlinnPhong alpha` pass, plus a second additive `ShadowPass` tagged
// `ForwardBase` that sampled `LIGHT_ATTENUATION` and drew `(1 - atten) * _ShadowIntensity` as black alpha over the
// surface. That pass existed because Built-in's forward transparents received no real shadows, so the shadow had to be
// faked by compositing darkness on top. URP shades transparents in the same `UniversalForward` pass as opaques and
// samples the shadow map there, so the fake is not needed and keeping it would double-darken every shadowed pixel.
// `_ShadowIntensity` survives as a material knob, defaulting to 1 -- the physical value -- where the old shader
// hard-coded 0.6.
//
// The original also carried `#pragma exclude_renderers d3d11 xbox360`, which on any current graphics API meant the
// shader was skipped entirely. Nothing in the tool referenced it, which is consistent with it never having drawn.
//
// The `ShadowCaster` pass is clipped against `_Cutoff` rather than casting the whole quad: a 30% opaque panel that
// cast a solid shadow was the other half of the old workaround's wrongness.
Shader "RoadCityMaster/TranShadow"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Transparency (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Toggle(_NORMALMAP)] _NormalMapToggle("Use Normal Map", Float) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Toggle(_EMISSION)] _EmissionToggle("Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Colour", Color) = (0,0,0,1)

        _Cutoff("Shadow Cast Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0
        _SpecColor("Specular Colour", Color) = (0.2,0.2,0.2,1)
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        // 1 is transparent. Read by `IsSurfaceTypeTransparent` in the fragment so the blended alpha survives to the
        // frame buffer instead of being forced to 1 the way an opaque surface's is.
        [HideInInspector] _Surface("__surface", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Transparent"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RoadSurfaceVertex
            #pragma fragment RoadSurfaceFragment

            #define _SURFACE_TYPE_TRANSPARENT 1

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION

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

            // Clipped, not blended: a shadow map holds depth, not coverage, so the only honest shadow a transparent
            // surface can cast is the shadow of the part of it that is solid enough to stop light.
            #define _ALPHATEST_ON 1

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Include/RoadSurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
