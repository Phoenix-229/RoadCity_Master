// The variant set every `UniversalForward` pass in this folder needs, in one place.
//
// Pulled in with `#include_with_pragmas` so the ten shaders above this folder do not each carry their own copy of
// eighteen `#pragma multi_compile` lines. A keyword missing from one shader is not a compile error, it is a shader
// that quietly stops receiving shadows or reflection probes on some quality tier -- exactly the kind of drift a
// shared list prevents.
//
// Only the pipeline-wide keywords are here. Per-material `shader_feature_local` toggles stay in the `.shader` files
// because they differ: a cutout marker needs `_ALPHATEST_ON` and a road surface must not have it.
#ifndef ROADCITYMASTER_ROAD_SURFACE_FORWARD_PRAGMAS_INCLUDED
#define ROADCITYMASTER_ROAD_SURFACE_FORWARD_PRAGMAS_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _LIGHT_LAYERS

// `_CLUSTER_LIGHT_LOOP` is the current name of the keyword that was `_FORWARD_PLUS`. Both names exist in URP's
// history; only this one is set by URP 17.
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP

#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile_fog
#pragma multi_compile_instancing

#endif
