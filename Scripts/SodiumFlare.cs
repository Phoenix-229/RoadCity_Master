using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadCityMaster
{
    /// <summary>
    /// The street-lamp lens flare, expressed the way URP draws lens flares, and carrying the light source a road
    /// authority actually installs TODAY: a phosphor-converted white LED luminaire, not the high-pressure sodium
    /// lamp the original asset was drawn for.
    ///
    /// Why the emitter changed (this is a real-world correction, not a restyle). The file this class was ported from,
    /// `Flares/SodiumBulb.flare`, describes a high-pressure sodium (HPS) lamp: an arc discharge whose output is
    /// dominated by the sodium D-line doublet near 589 nm, so its light is the familiar near-monochromatic amber at a
    /// correlated colour temperature of roughly 2100 K. Roadway lighting has moved off that lamp. New and retrofit
    /// installations use LED luminaires, which are broadband white emitters (a blue die behind a phosphor), for
    /// reasons that are all measured, not stylistic: luminous efficacy of order 130-150 lm/W against roughly
    /// 100-120 lm/W for HPS, service life measured in tens of thousands of hours, and a white spectrum that gives
    /// real colour rendering at night instead of amber monochrome. So the flare this tool draws is white.
    ///
    /// The colour is a cited figure, not a taste call. <see cref="ColourTemperatureKelvin"/> is 4000 K -- the
    /// "neutral white" nominal CCT bin of ANSI C78.377, and the CCT most widely specified for roadway LED under IES
    /// RP-8 practice. (3000 K is the warmer bin recommended where sky-glow and glare are governed; it is the same
    /// class of luminaire, one bin down, and is the value to change this constant to for a dark-sky district.)
    /// <see cref="LedWhite"/> is that temperature rendered as sRGB.
    ///
    /// How URP draws it. Built-in drew a flare by assigning a <c>Flare</c> asset to <c>Light.flare</c>. URP draws
    /// nothing at all for that field: its lens flares are a <c>LensFlareDataSRP</c> asset referenced by a
    /// <c>LensFlareComponentSRP</c> on the light's own GameObject, rendered by the data-driven lens flare pass.
    /// (Those type names carry "SRP" because they are the Scriptable Render Pipeline core types URP itself uses --
    /// URP is an SRP, so this IS the URP lens-flare API, not a foreign pipeline's.) This class is the single owner of
    /// every number involved, so the editor-side minting operator and the runtime attach path cannot drift apart.
    ///
    /// What carried over from the Built-in file unchanged, because it is geometry and not emitter physics:
    ///
    ///     m_Size: 25      -> see ElementUniformScale      m_Position: 0    -> the element sits on the light
    ///     m_Rotate: 0     -> AutoRotate                   m_Fade: 1        -> AllowOffScreen
    ///     m_UseFog: 1     -> EnvironmentOcclusion
    ///
    /// What did NOT carry over: the amber. The Built-in element took its colour from `Flares/SodiumBulb.tga`, whose
    /// pigment is the sodium lamp's. A texture cannot be brightened back to white by tinting -- a multiply can only
    /// darken -- so the LED glow is drawn as URP's procedural <c>Circle</c> instead, whose colour is entirely the
    /// tint set here. That also matches the source: an LED street lamp's flare is a round, radially symmetric bloom
    /// off a flat lens, which is what a circle is. The `.tga` stays on disk, still referenced by the legacy `.flare`
    /// as the record of what the sodium asset looked like.
    /// </summary>
    public static class SodiumFlare
    {
        /// <summary> Where the URP flare data asset lives, relative to the tool's base path. Minted by Unity through
        /// the editor-side operator, never hand-authored, so its GUID is Unity's. The filename is the one the asset
        /// has always had; renaming it would break every reference for no render benefit, so the historical name is
        /// kept and the emitter inside it is what changed. </summary>
        public const string DataAssetRelativePath = "/Flares/SodiumBulb.asset";

        /// <summary> The legacy flare image. No longer drawn -- see the note on the procedural circle -- but still on
        /// disk and still referenced by `Flares/SodiumBulb.flare`, so it is not orphaned. </summary>
        public const string TextureRelativePath = "/Flares/SodiumBulb.tga";

        /// <summary> The luminaire's correlated colour temperature in kelvin. 4000 K is the "neutral white" nominal
        /// CCT bin of ANSI C78.377 and the temperature most widely specified for roadway LED (IES RP-8 practice).
        /// Change this to 3000 for a dark-sky district -- the warmer bin governed where sky-glow and glare limits
        /// apply -- and change <see cref="LedWhite"/> with it. </summary>
        public const float ColourTemperatureKelvin = 4000.0f;

        /// <summary> <see cref="ColourTemperatureKelvin"/> rendered as sRGB: 4000 K on the Planckian locus is
        /// approximately (255, 209, 163) in 8-bit sRGB. This is a conversion of the cited colour temperature, not a
        /// colour picked to look nice; it reads as white with the faint warmth a 4000 K fixture actually has, which is
        /// what separates it from the 2100 K amber the sodium asset carried. </summary>
        public static Color LedWhite { get { return new Color(1.0f, 0.820f, 0.639f, 1.0f); } }

        /// <summary> The Built-in element's <c>m_Size: 25</c> converted, not re-authored: Built-in flare element size
        /// is in hundredths of screen height, and URP's uniform scale is in screen height, so 25 becomes 0.25. Halo
        /// size is geometry, not emitter physics, so the LED conversion leaves it alone. What a viewer actually sees is
        /// this times the component's distance attenuation, which the component owns. </summary>
        public const float ElementUniformScale = 0.25f;

        /// <summary> <c>m_Position: 0</c> -- on the light, not offset along the flare axis. </summary>
        public const float ElementPosition = 0.0f;

        /// <summary> Softness of the procedural circle's edge. A street lamp's lens is a diffusing surface, so the
        /// bloom has no hard rim; a full falloff is the round, edge-soft glow that produces. </summary>
        public const float CircleFallOff = 1.0f;

        /// <summary> No inset on the circle: the glow is a filled disc centred on the lamp, not a ring. </summary>
        public const float CircleEdgeOffset = 0.0f;

        /// <summary> False, and for a different reason than the sodium flare had. Sodium was excluded from light-colour
        /// modulation because an HPS arc is near-monochromatic amber whatever colour the Unity light is set to. An LED
        /// luminaire is likewise a fixed-CCT emitter: its 4000 K white is the fixture's, set here in
        /// <see cref="LedWhite"/>, and must not be re-tinted by a light colour a scene may have changed for gameplay
        /// or time-of-day reasons. The physical claim is the same shape -- the emitter owns its colour -- with the real
        /// emitter substituted. </summary>
        public const bool ModulateByLightColor = false;

        /// <summary> <c>m_Rotate: 0</c> -- the glow is radially symmetric, so screen rotation is meaningless. </summary>
        public const bool AutoRotate = false;

        /// <summary> <c>m_Fade: 1</c> inverted: Built-in faded the flare out at the screen edge, which is what
        /// keeping URP's off-screen draw switched off does. </summary>
        public const bool AllowOffScreen = false;

        /// <summary> <c>m_UseFog: 1</c>. URP renamed this field twice -- the serialized name was
        /// <c>useFogOpacityOcclusion</c> before it became <c>environmentOcclusion</c> -- and it still means the same
        /// thing: a lamp seen through fog dims. Occlusion has to be on for it to be consulted. </summary>
        public const bool EnvironmentOcclusion = true;

        /// <summary> Built-in's <c>m_Zoom: 1</c> has no per-element equivalent in URP; the nearest thing is the
        /// component's radial screen attenuation curve, which is a curve and not a flag. It is deliberately left at
        /// URP's own default rather than filled with a curve invented here, so nothing in the ported flare is data
        /// this project made up. Recorded so the omission is a decision and not an oversight. </summary>
        public const string ZoomDisposition = "no per-element equivalent in URP; component radial curve left default";


        /// <summary> The one element the Built-in asset had, rebuilt as the LED luminaire's glow.
        /// <c>SRPLensFlareType.Circle</c> rather than <c>Image</c>: the source of the amber was the texture's pigment,
        /// and a tint multiply can only darken a texture, never bring it back to white, so the only honest way to draw
        /// a 4000 K white bloom is to draw it procedurally and let <see cref="LedWhite"/> BE the colour. A filled,
        /// edge-soft disc is also what a flat diffusing lamp lens actually throws. <paramref name="_texture"/> is
        /// accepted so the editor-side operator's call is unchanged, and deliberately not assigned -- a procedural
        /// element has no image. </summary>
        public static LensFlareDataElementSRP BuildBulbElement(Texture _texture)
        {
            LensFlareDataElementSRP element = new LensFlareDataElementSRP();

            element.visible = true;
            element.flareType = SRPLensFlareType.Circle;
            element.preserveAspectRatio = true;
            element.fallOff = CircleFallOff;
            element.edgeOffset = CircleEdgeOffset;
            element.inverseSDF = false;

            element.position = ElementPosition;
            element.positionOffset = Vector2.zero;
            element.uniformScale = ElementUniformScale;
            element.sizeXY = Vector2.one;

            element.tintColorType = SRPLensFlareColorType.Constant;
            element.tint = LedWhite;
            element.blendMode = SRPLensFlareBlendMode.Additive;
            element.modulateByLightColor = ModulateByLightColor;
            element.autoRotate = AutoRotate;
            element.rotation = 0.0f;

            element.allowMultipleElement = false;
            element.enableRadialDistortion = false;
            element.seed = 0;

            return element;
        }


        /// <summary> The one comparison of an element against this class's numbers, appending a sentence per
        /// disagreement to <paramref name="_failures"/> and returning true when it found none.
        ///
        /// It lives here, beside the constants it compares against, because two callers need the identical answer and
        /// for different reasons: the editor operator checks the asset it has just written to disk, so it is really
        /// asking whether Unity's serialiser round-tripped what it was handed, and the headless gate checks
        /// <see cref="BuildBulbElement"/> in memory, so it is asking whether the builder still sets every field the
        /// spec names. Written twice, those two would agree until the day one of them was edited -- and then the
        /// disagreement would read as a bug in the flare rather than as a bug in the copy. </summary>
        public static bool VerifyElement(LensFlareDataElementSRP _element, List<string> _failures)
        {
            if (_failures == null) { return false; }
            int before = _failures.Count;

            if (_element == null)
            {
                _failures.Add("there is no element at all, so nothing draws");
                return false;
            }

            if (_element.flareType != SRPLensFlareType.Circle)
            {
                _failures.Add("the element is not a Circle flare, so it is not drawing the LED luminaire's glow "
                    + "procedurally and its colour would come from a texture's pigment instead of the cited CCT");
            }
            if (_element.lensFlareTexture != null)
            {
                _failures.Add("the element carries a texture; a procedural circle has no image, and the amber lived "
                    + "in that image's pigment, which is exactly what the port moved away from");
            }
            if (!_element.preserveAspectRatio)
            {
                _failures.Add("the element does not preserve aspect ratio, so the glow is squashed");
            }
            if (_element.tintColorType != SRPLensFlareColorType.Constant)
            {
                _failures.Add("the element's tint is not a constant colour, so the LED's fixed "
                    + ColourTemperatureKelvin.ToString("0", CultureInfo.InvariantCulture)
                    + " K white is not what gets drawn");
            }

            Color expected = LedWhite;
            if (Mathf.Abs(_element.tint.r - expected.r) > TintTolerance
                || Mathf.Abs(_element.tint.g - expected.g) > TintTolerance
                || Mathf.Abs(_element.tint.b - expected.b) > TintTolerance)
            {
                _failures.Add("the element tint is " + _element.tint.ToString() + "; the LED luminaire's "
                    + ColourTemperatureKelvin.ToString("0", CultureInfo.InvariantCulture) + " K white is "
                    + expected.ToString());
            }
            if (Mathf.Abs(_element.uniformScale - ElementUniformScale) > 1e-4f)
            {
                _failures.Add("the element scale is "
                    + _element.uniformScale.ToString("0.####", CultureInfo.InvariantCulture)
                    + "; the Built-in size converted to "
                    + ElementUniformScale.ToString("0.####", CultureInfo.InvariantCulture));
            }
            if (_element.blendMode != SRPLensFlareBlendMode.Additive)
            {
                _failures.Add("the element is not additive, which is how emitted light composites");
            }
            if (_element.modulateByLightColor != ModulateByLightColor)
            {
                _failures.Add("the element modulates by light colour; a fixed-CCT LED luminaire owns its own white");
            }
            if (_element.autoRotate != AutoRotate)
            {
                _failures.Add("the element auto-rotates; the glow is radially symmetric, so rotation is meaningless");
            }
            if (_element.allowMultipleElement)
            {
                _failures.Add("the element is set to repeat; one lamp is one bulb, not a row of ghosts");
            }

            return _failures.Count == before;
        }


        /// <summary> How far a tint channel may drift before it counts as a different colour. Loose enough that a
        /// float round-trip through the asset serialiser passes, tight enough that a real repigment does not. </summary>
        public const float TintTolerance = 0.002f;


        /// <summary> Writes the flare into a data asset. Overwrites the element array wholesale, so running it twice
        /// leaves one element rather than two. </summary>
        public static void Apply(LensFlareDataSRP _data, Texture _texture)
        {
            if (_data == null) { return; }
            _data.elements = new LensFlareDataElementSRP[] { BuildBulbElement(_texture) };
        }


        /// <summary> Puts the flare on a light. Reuses the component if one is already there, because this runs every
        /// time an intersection is rebuilt and a second component would draw the glow twice. Returns null when the
        /// data asset is missing, which is the honest outcome: no flare rather than an empty one. </summary>
        public static LensFlareComponentSRP Attach(Light _light, LensFlareDataSRP _data)
        {
            if (_light == null || _data == null) { return null; }

            LensFlareComponentSRP flare = _light.gameObject.GetComponent<LensFlareComponentSRP>();
            if (flare == null)
            {
                flare = _light.gameObject.AddComponent<LensFlareComponentSRP>();
            }

            flare.lensFlareData = _data;
            flare.intensity = 1.0f;
            flare.scale = 1.0f;
            flare.allowOffScreen = AllowOffScreen;

            // Fog dimming is what m_UseFog asked for, and URP only consults the environment term when occlusion is on.
            flare.useOcclusion = true;
            flare.environmentOcclusion = EnvironmentOcclusion;

            return flare;
        }


        /// <summary> Loads the minted data asset and attaches it, given the tool's base path. The one call the
        /// generator makes; if the asset has not been minted yet the light simply has no flare. </summary>
        public static LensFlareComponentSRP AttachFromBasePath(Light _light, string _basePath)
        {
            LensFlareDataSRP data = EngineIntegration.LoadAssetFromPath<LensFlareDataSRP>(_basePath + DataAssetRelativePath);
            return Attach(_light, data);
        }


    }
}
