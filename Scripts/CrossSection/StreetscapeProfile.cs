#region "Imports"
using System;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> RM-13's streetscape as MORE of the existing cross-section, not a new mesh pipeline: a kerb
    /// is a profile element with a vertical face and an upstand, a footway is a profile element with a
    /// crossfall toward the kerb, a verge is a profile element with no surface material. Built this way they
    /// inherit -- for free -- the superelevation, variable width (RM-31), terraform footprint, splat mapping
    /// and LOD the carriageway already has.
    ///
    /// Every quantity here is a stated design value that DIFFERS BETWEEN JURISDICTIONS, which is exactly why
    /// it is an AUTHORED field on the road class rather than a constant in code. Where the project has not
    /// adopted a standard, the number is AUTHORED, NOT GENERATED and recorded, per § 10's governing rule.
    /// The <see cref="Default"/> values are AUTHORED placeholders in a common real-world range, to be
    /// replaced when a design standard is adopted -- never a pretend-cited figure. </summary>
    [Serializable]
    public struct StreetscapeProfile
    {
        /// <summary> Kerb upstand (kerb face height above the carriageway), metres. AUTHORED. </summary>
        public float kerbUpstandMetres;

        /// <summary> Kerb upstand lowered at a crossing -- flush or nearly flush with tactile paving,
        /// metres. AUTHORED; must be no greater than <see cref="kerbUpstandMetres"/>. </summary>
        public float kerbUpstandAtCrossingMetres;

        /// <summary> Footway minimum clear width, metres -- the usable width clear of furniture. AUTHORED. </summary>
        public float footwayClearWidthMetres;

        /// <summary> Footway crossfall as a fall toward the kerb, expressed as a fraction (rise/run, e.g.
        /// 0.02 = 2%). Must be positive so the footway DRAINS toward the kerb. AUTHORED. </summary>
        public float footwayCrossfall;

        /// <summary> The accessibility maximum the footway crossfall must not exceed, as a fraction.
        /// AUTHORED from the adopted accessibility standard. </summary>
        public float footwayCrossfallAccessibilityMax;

        /// <summary> Verge width (a profile element with no surface material), metres. AUTHORED. </summary>
        public float vergeWidthMetres;

        /// <summary> Whether the authored numbers are self-consistent: the crossing kerb is not taller than
        /// the normal kerb, the crossfall drains toward the kerb (positive), and it does not exceed the
        /// accessibility maximum. Writes a numbered reason on failure, per the report discipline. </summary>
        public bool IsConsistent(out string reason)
        {
            if (kerbUpstandAtCrossingMetres > kerbUpstandMetres + 1e-4f)
            {
                reason = "crossing kerb " + kerbUpstandAtCrossingMetres + " m is taller than the normal kerb "
                    + kerbUpstandMetres + " m";
                return false;
            }
            if (footwayCrossfall <= 0f)
            {
                reason = "footway crossfall " + footwayCrossfall + " does not drain toward the kerb (must be > 0)";
                return false;
            }
            if (footwayCrossfall > footwayCrossfallAccessibilityMax + 1e-6f)
            {
                reason = "footway crossfall " + footwayCrossfall + " exceeds the accessibility maximum "
                    + footwayCrossfallAccessibilityMax;
                return false;
            }
            reason = "consistent";
            return true;
        }

        /// <summary> AUTHORED placeholder streetscape in a common real-world range (0.1 m kerb upstand,
        /// flush 0.006 m at crossings, 2.0 m clear footway, 2% crossfall against a 2.5% accessibility cap,
        /// 0.5 m verge). NOT a cited standard -- replace when one is adopted. </summary>
        public static StreetscapeProfile Default()
        {
            StreetscapeProfile s;
            s.kerbUpstandMetres = 0.1f;
            s.kerbUpstandAtCrossingMetres = 0.006f;
            s.footwayClearWidthMetres = 2.0f;
            s.footwayCrossfall = 0.02f;
            s.footwayCrossfallAccessibilityMax = 0.025f;
            s.vergeWidthMetres = 0.5f;
            return s;
        }
    }
}
