// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Why a content lookup was REFUSED. RM-55 requires the three cases be told apart: a category with
    /// nothing registered, a category whose assets exist but none carries the required variant, and a category
    /// whose only matching asset(s) failed RM-48's validation. A refusal is never a silent grey box -- it names
    /// what was missing so the author can register it. </summary>
    public enum ContentRefusalReason
    {
        /// <summary> No asset is registered under the category at all. </summary>
        CategoryEmpty = 0,

        /// <summary> Assets are registered, but none carries the required variant tag. </summary>
        NoVariantMatch = 1,

        /// <summary> An asset is present and matches the variant, but every such asset failed RM-48 validation. </summary>
        FailedValidation = 2
    }


    /// <summary> One author-registered content entry: the ONLY thing the resolver may hand back. Identified by a
    /// stable <see cref="assetId"/> and filed under a <see cref="category"/> string key (the same key RM-48's
    /// validator and RM-77's manifest use). <see cref="passedValidation"/> is DATA here -- it is set by RM-48's
    /// editor-side <c>PrefabContractValidator</c> in the deferred scene half; the pure resolver only reads it, so
    /// the runtime core needs no editor dependency. <see cref="variantTags"/> are optional labels (e.g. "corner",
    /// "angle-tower") a caller may require. </summary>
    [Serializable]
    public struct ContentCandidate
    {
        public string assetId;
        public string category;
        public bool passedValidation;
        public string[] variantTags;

        /// <summary> True if this candidate satisfies the required variant. A null/empty requirement matches any
        /// candidate; otherwise the tag must appear in <see cref="variantTags"/>. </summary>
        public bool HasVariant(string variant)
        {
            if (string.IsNullOrEmpty(variant)) { return true; }
            if (variantTags == null) { return false; }
            for (int i = 0; i < variantTags.Length; i++)
            {
                if (variantTags[i] == variant) { return true; }
            }
            return false;
        }
    }


    /// <summary> A refusal to place content: the category asked for, the object that asked (its stable id), and
    /// the reason. This is the exact shape RM-55 mandates -- "a refusal carrying the category name, the requesting
    /// object, and the reason." </summary>
    public struct ContentRefusal
    {
        public string category;
        public string requester;
        public ContentRefusalReason reason;

        /// <summary> A human-readable one-liner for the refusal list (RM-52's home). </summary>
        public string Describe()
        {
            string why;
            switch (reason)
            {
                case ContentRefusalReason.CategoryEmpty:
                    why = "no asset is registered in this category"; break;
                case ContentRefusalReason.NoVariantMatch:
                    why = "no registered asset matches the required variant"; break;
                default:
                    why = "the only matching asset(s) failed validation"; break;
            }
            return string.Format("'{0}' requested by '{1}': {2}", category, requester, why);
        }
    }


    /// <summary> The outcome of ONE resolve. There is no third path: it is EITHER resolved with an asset OR refused
    /// with a reason -- never both, never neither. Callers branch on <see cref="resolved"/>; a resolved result
    /// carries <see cref="asset"/>, a refused one carries <see cref="refusal"/>. </summary>
    public struct ContentResolution
    {
        public bool resolved;
        public ContentCandidate asset;
        public ContentRefusal refusal;

        public static ContentResolution Resolved(ContentCandidate a)
        {
            ContentResolution r = new ContentResolution();
            r.resolved = true;
            r.asset = a;
            return r;
        }

        public static ContentResolution Refused(ContentRefusal refusal)
        {
            ContentResolution r = new ContentResolution();
            r.resolved = false;
            r.refusal = refusal;
            return r;
        }
    }


    /// <summary> The author-registered content, keyed by category string. FAIL-CLOSED by construction: a category
    /// nobody registered simply has no candidates, so every lookup on it refuses -- there is no default entry, no
    /// borrowing from a neighbouring category, no invented content. <see cref="Register"/> has set semantics (a
    /// repeated <see cref="ContentCandidate.assetId"/> in the same category is ignored), and <see cref="Candidates"/>
    /// hands back a STABLE (assetId-sorted) copy so the deterministic draw never depends on registration order
    /// (RM-47). </summary>
    public sealed class ContentCatalog
    {
        private readonly Dictionary<string, List<ContentCandidate>> byCategory =
            new Dictionary<string, List<ContentCandidate>>(StringComparer.Ordinal);

        /// <summary> Register one author-supplied asset under its category. Ignores an entry with no category or no
        /// assetId, and an assetId already present in that category (idempotent). </summary>
        public void Register(ContentCandidate candidate)
        {
            if (string.IsNullOrEmpty(candidate.category) || string.IsNullOrEmpty(candidate.assetId)) { return; }
            List<ContentCandidate> list;
            if (!byCategory.TryGetValue(candidate.category, out list))
            {
                list = new List<ContentCandidate>();
                byCategory[candidate.category] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].assetId == candidate.assetId) { return; }
            }
            list.Add(candidate);
        }

        /// <summary> The registered candidates for a category, assetId-sorted for a stable, registration-order-
        /// independent draw. Empty (never null) for an unknown category -- the fail-closed default. </summary>
        public List<ContentCandidate> Candidates(string category)
        {
            List<ContentCandidate> list;
            if (string.IsNullOrEmpty(category) || !byCategory.TryGetValue(category, out list))
            {
                return new List<ContentCandidate>();
            }
            List<ContentCandidate> copy = new List<ContentCandidate>(list);
            copy.Sort(delegate (ContentCandidate a, ContentCandidate b)
            {
                return string.CompareOrdinal(a.assetId, b.assetId);
            });
            return copy;
        }

        /// <summary> True if the category holds at least one registered asset (of any validation state). </summary>
        public bool HasCategory(string category)
        {
            List<ContentCandidate> list;
            return !string.IsNullOrEmpty(category) && byCategory.TryGetValue(category, out list) && list.Count > 0;
        }
    }


    /// <summary> RM-55's verifiable core: the ONE resolution path every prefab lookup in the city layer goes
    /// through. It returns EITHER an author-registered asset for the EXACT category asked for OR a refusal naming
    /// the category, the requester and the reason -- there is no third path (no grey box, no silent substitution,
    /// no "nearest category", no invented content). Selection WITHIN a category is a deterministic seeded draw
    /// keyed on the requester's own identity (RM-47), so the choice reproduces run-to-run and does not depend on
    /// iteration order. The live prefab instantiation and the RM-48 validation that sets
    /// <see cref="ContentCandidate.passedValidation"/> are the deferred scene half. </summary>
    public static class ContentResolver
    {
        /// <summary> Resolve a content request for an exact category, optionally requiring a variant tag. The three
        /// refusal reasons are told apart in order: empty category, then no asset matching the required variant,
        /// then all matching assets failed validation. When there is at least one eligible asset, one is chosen by
        /// a requester-seeded draw over the stable-sorted eligible set. </summary>
        public static ContentResolution Resolve(ContentCatalog catalog, string category, string requester,
            ulong citySeed, string requiredVariant)
        {
            ContentRefusal refusal;
            refusal.category = category;
            refusal.requester = requester;
            refusal.reason = ContentRefusalReason.CategoryEmpty;

            List<ContentCandidate> all = catalog != null
                ? catalog.Candidates(category) : new List<ContentCandidate>();
            if (all.Count == 0)
            {
                refusal.reason = ContentRefusalReason.CategoryEmpty;
                return ContentResolution.Refused(refusal);
            }

            // Assets matching the required variant, ignoring validation -- so "no such variant" is told from
            // "present but every match is invalid".
            List<ContentCandidate> matching = new List<ContentCandidate>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].HasVariant(requiredVariant)) { matching.Add(all[i]); }
            }
            if (matching.Count == 0)
            {
                refusal.reason = ContentRefusalReason.NoVariantMatch;
                return ContentResolution.Refused(refusal);
            }

            // Of those, the ones that passed RM-48 validation are eligible to place.
            List<ContentCandidate> eligible = new List<ContentCandidate>();
            for (int i = 0; i < matching.Count; i++)
            {
                if (matching[i].passedValidation) { eligible.Add(matching[i]); }
            }
            if (eligible.Count == 0)
            {
                refusal.reason = ContentRefusalReason.FailedValidation;
                return ContentResolution.Refused(refusal);
            }

            // Deterministic draw keyed on the REQUESTER's identity (RM-47) over the stable-sorted eligible set:
            // no shared advancing stream, no index that depends on iteration order.
            DeterministicRandom rng = DeterministicRandom.ForElement(citySeed, requester);
            int index = (int)(rng.NextUInt64() % (ulong)eligible.Count);
            return ContentResolution.Resolved(eligible[index]);
        }

        /// <summary> Resolve with no variant requirement. </summary>
        public static ContentResolution Resolve(ContentCatalog catalog, string category, string requester,
            ulong citySeed)
        {
            return Resolve(catalog, category, requester, citySeed, null);
        }
    }


    /// <summary> Collects refusals and AGGREGATES them, so a city missing one street-tree category produces ONE
    /// entry with a count and the first requester ("jump-to-first"), not four thousand identical lines (RM-55).
    /// Entries are grouped by (category, reason); the first requester seen for a group is kept. <see cref="Entries"/>
    /// returns them in a canonical (category, then reason) order so the refusal list is deterministic regardless of
    /// the order refusals arrived in. </summary>
    public sealed class ContentRefusalAggregator
    {
        /// <summary> One aggregated refusal group. </summary>
        public struct Entry
        {
            public string category;
            public ContentRefusalReason reason;
            public int count;
            public string firstRequester;
        }

        private readonly List<Entry> entries = new List<Entry>();

        /// <summary> Fold one refusal into its (category, reason) group. </summary>
        public void Add(ContentRefusal refusal)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category == refusal.category && entries[i].reason == refusal.reason)
                {
                    Entry e = entries[i];
                    e.count++;
                    entries[i] = e;
                    return;
                }
            }
            Entry ne;
            ne.category = refusal.category;
            ne.reason = refusal.reason;
            ne.count = 1;
            ne.firstRequester = refusal.requester;
            entries.Add(ne);
        }

        /// <summary> Convenience: fold a resolution only if it was a refusal (resolved results are ignored). </summary>
        public void Add(ContentResolution resolution)
        {
            if (!resolution.resolved) { Add(resolution.refusal); }
        }

        /// <summary> The aggregated groups in a canonical order (category, then reason). </summary>
        public IList<Entry> Entries()
        {
            List<Entry> copy = new List<Entry>(entries);
            copy.Sort(delegate (Entry a, Entry b)
            {
                int c = string.CompareOrdinal(a.category, b.category);
                return c != 0 ? c : ((int)a.reason - (int)b.reason);
            });
            return copy;
        }

        /// <summary> Number of distinct (category, reason) groups. </summary>
        public int DistinctCount { get { return entries.Count; } }

        /// <summary> Total refusals folded in, across all groups. </summary>
        public int TotalRefusals()
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++) { n += entries[i].count; }
            return n;
        }
    }
}
