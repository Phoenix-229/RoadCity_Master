#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-64's fifth criterion, built once here instead of fifteen times badly: foldout state that survives a
    /// selection change.
    ///
    /// The usual mistake is a <c>bool showTerrain</c> field on the editor class. Unity creates and destroys editor
    /// instances as the selection moves, so that field resets every time an author clicks away and back -- which is
    /// precisely the behaviour that makes a tool feel like it is fighting you. The state therefore belongs OUTSIDE the
    /// editor instance, and it is keyed by component TYPE rather than by instance: an author who collapses "what it does
    /// to the world" while tuning twenty roads means it for roads, not for one road.
    ///
    /// The store is injected rather than hard-wired to <see cref="EditorPrefs"/> for one reason: a gate must be able to
    /// prove the key is stable and the default policy is what it claims without writing to the user's editor preferences
    /// as a side effect of a build. </summary>

    /// <summary> Where foldout state is kept. Two implementations: preferences for real use, memory for the gate. </summary>
    public interface IFoldoutStore
    {
        bool TryGet(string key, out bool open);
        void Set(string key, bool open);
    }

    /// <summary> The real store. Keys are prefixed so they are identifiable in a preferences file and cannot collide with
    /// another tool's. </summary>
    public sealed class EditorPrefsFoldoutStore : IFoldoutStore
    {
        public bool TryGet(string key, out bool open)
        {
            if (!EditorPrefs.HasKey(key)) { open = false; return false; }
            open = EditorPrefs.GetBool(key);
            return true;
        }

        public void Set(string key, bool open)
        {
            EditorPrefs.SetBool(key, open);
        }
    }

    /// <summary> A store that forgets when the process does. Used by the gate, and by anything that wants the policy
    /// without the persistence. </summary>
    public sealed class InMemoryFoldoutStore : IFoldoutStore
    {
        private readonly Dictionary<string, bool> _values = new Dictionary<string, bool>();

        public bool TryGet(string key, out bool open)
        {
            return _values.TryGetValue(key, out open);
        }

        public void Set(string key, bool open)
        {
            _values[key] = open;
        }

        public int Count { get { return _values.Count; } }
    }

    /// <summary> The convention: one key per component type per group, a stated default for a group nobody has touched,
    /// and no other way for an inspector to remember a foldout. </summary>
    public static class InspectorFoldoutState
    {
        /// <summary> Key prefix. The identifier form, not the display name -- a preferences key with a space and an
        /// ampersand in it is a key people mistype. </summary>
        public const string KeyPrefix = "RoadCityMaster.Foldout.";

        private static IFoldoutStore _store = new EditorPrefsFoldoutStore();

        /// <summary> Swap the store. The gate uses this; nothing else should. </summary>
        public static void UseStore(IFoldoutStore store)
        {
            _store = store ?? new EditorPrefsFoldoutStore();
        }

        /// <summary> The key for one component type and one group. Deterministic, culture-independent, and free of
        /// <c>GetHashCode</c> -- which C-06 bans because the runtime does not promise it across processes, and a foldout
        /// key that changes between Unity versions is a foldout that forgets. </summary>
        public static string Key(string componentType, FieldGroup group)
        {
            return KeyPrefix + (string.IsNullOrEmpty(componentType) ? "Unknown" : componentType)
                + "." + group.ToString();
        }

        /// <summary> The key for the measured group, which is not a <see cref="FieldGroup"/> because a measurement is not
        /// a decision an author makes. </summary>
        public static string MeasuredKey(string componentType)
        {
            return KeyPrefix + (string.IsNullOrEmpty(componentType) ? "Unknown" : componentType)
                + "." + MeasuredValuePresentation.GroupLabel;
        }

        /// <summary> What a group does when nobody has ever touched it: the identity group open, everything else closed.
        /// An inspector that opens fully collapsed looks broken, and one that opens fully expanded is a wall. </summary>
        public static bool DefaultOpen(FieldGroup group)
        {
            return group == FieldGroup.WhatItIs;
        }

        /// <summary> Is this group open for this component type? Falls back to the default policy, and the fallback does
        /// NOT write, so reading an inspector never manufactures preference entries. </summary>
        public static bool IsOpen(string componentType, FieldGroup group)
        {
            bool open;
            if (_store.TryGet(Key(componentType, group), out open)) { return open; }
            return DefaultOpen(group);
        }

        /// <summary> Record an author's choice for every instance of this component type. </summary>
        public static void SetOpen(string componentType, FieldGroup group, bool open)
        {
            _store.Set(Key(componentType, group), open);
        }

        /// <summary> The measured group defaults closed: it is reference material, not the reason an author opened the
        /// inspector. </summary>
        public static bool IsMeasuredOpen(string componentType)
        {
            bool open;
            if (_store.TryGet(MeasuredKey(componentType), out open)) { return open; }
            return false;
        }

        public static void SetMeasuredOpen(string componentType, bool open)
        {
            _store.Set(MeasuredKey(componentType), open);
        }
    }
}
#endif
