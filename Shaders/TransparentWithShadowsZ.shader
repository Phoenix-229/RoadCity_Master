// The transparent marking: a lane line, crossing bar or worn arrow blended onto the surface it lies on, rather than
// cut out of it. `RoadCityMaster/CutoutMarker` is the alpha-clipped version of the same idea; this one keeps soft
// edges, so a faded line can actually look faded instead of dithering away pixel by pixel.
//
// Two things carried over from the Built-in original, and one deleted:
//
//   * `Offset -3,-2` is kept, and is the reason this is a shader and not a URP Lit material. A marking shares its
//     plane with the road along its whole length, so it needs the same strong depth bias the cutout marker gets.
//   * `_ShadowIntensity` is kept as a knob, defaulting to 1.
//   * The second `ForwardBase` pass that composited `(1 - LIGHT_ATTENUATION) * _ShadowIntensity` as black alpha is
//     deleted. It existed because Built-in transparents received no real shadows; URP's forward pass samples the
//     shadow map for transparents, so keeping it would darken every shadowed marking twice.
//
// There is deliberately no `ShadowCaster` pass. Paint on a road casts no shadow, and a marking that cast one would
// cast it onto the very surface it is painted on -- a dark halo tracking the sun around every line. `ZWrite Off` is
// for the same reason: a decal does not own the depth of the surface underneath it.
Shader "RoadCityMaster/TranShadowMarker"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Transparency (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)

        // AUTHORED: road marking paint is a thermoplastic or waterborne film with glass beads in it. It is smoother
        // than the asphalt around it -- that is why a wet line glares in headlights -- but it is not a mirror. 0.6.
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.6
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Toggle(_NORMALMAP)] _NormalMapToggle("Use Normal Map", Float) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Toggle(_EMISSION)] _EmissionToggle("Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Colour", Color) = (0,0,0,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0.0, 1.0)) = 1.0
        _SpecColor("Specular Colour", Color) = (0.2,0.2,0.2,1)
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

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

            Offset -3, -2
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
