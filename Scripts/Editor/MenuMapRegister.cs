#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-20's fix at the source: the menu is read from the attributes Unity itself reads, never counted by
    /// hand.
    ///
    /// The manual stated two different totals for the same tree -- 126 in one section, 134 in another -- and both came
    /// from counting by hand. A hand count cannot stay right here for two reasons. Five of the paths are declared as a
    /// <c>private const string</c> and referenced by name, so a text search for <c>MenuItem("Window/</c> misses them and
    /// lands short; and the tree grows whenever a gate is added, which is most topics. Reflection counts a literal, a
    /// const, and a const built from another const identically, and it is current the moment the new item compiles.
    ///
    /// It also answers the claim the manual makes about the suite and nothing checked: that every gate has its own menu
    /// item, so a gate can be run alone -- which is exactly when an author wants one.
    ///
    /// Determinism (C-06): the entry list is sorted ordinally, so two runs on two machines produce byte-identical text.
    /// The list is built once per domain reload, because a menu item cannot appear without a recompile. </summary>
    public static class MenuMapRegister
    {
        /// <summary> The tool's single menu root (RM-108: one home, no second tree). </summary>
        public const string Root = "Window/Road City Master/";

        /// <summary> The group name for items sitting directly under the root. It sorts first ordinally -- '(' is below
        /// every letter -- which is also where the manual lists it, so the table needs no special case. </summary>
        public const string TopLevel = "(top level)";

        private static List<Entry> _entries;

        /// <summary> One menu item, as its attribute declares it. </summary>
        public struct Entry
        {
            public string group;
            public string item;
            public string owner;

            public Entry(string group, string item, string owner)
            {
                this.group = group;
                this.item = item;
                this.owner = owner;
            }

            /// <summary> The full path Unity registers, rebuilt from the parts. </summary>
            public string Path
            {
                get { return group == TopLevel ? Root + item : Root + group + "/" + item; }
            }
        }

        /// <summary> The tool's own types, from its two assemblies. One place names them, so the gate suite and this
        /// register cannot disagree about what "the tool" is. Types that failed to load are skipped rather than
        /// throwing, because a partially loaded assembly must still be reportable. </summary>
        public static List<Type> ToolTypes()
        {
            List<Type> types = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                string name = assemblies[a].GetName().Name;
                if (name != "RoadCityMaster" && name != "RoadCityMaster.Editor") { continue; }

                Type[] found;
                try { found = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException e) { found = e.Types; }

                for (int t = 0; t < found.Length; t++)
                {
                    if (found[t] != null) { types.Add(found[t]); }
                }
            }
            return types;
        }
        /// <summary> Every declared menu item, split into the ones under this tool's root and the paths that are not.
        /// Both come from the same pass, so a path can never be counted by one and missed by the other. Validate
        /// functions are skipped: a validate attribute repeats a path it does not add. </summary>
        private static void Collect(List<Entry> inside, List<string> outside)
        {
            List<Type> types = ToolTypes();
            for (int t = 0; t < types.Count; t++)
            {
                MethodInfo[] methods = types[t].GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int m = 0; m < methods.Length; m++)
                {
                    object[] attributes = methods[m].GetCustomAttributes(typeof(MenuItem), false);
                    for (int a = 0; a < attributes.Length; a++)
                    {
                        MenuItem attribute = (MenuItem)attributes[a];
                        if (attribute.validate) { continue; }

                        string path = attribute.menuItem;
                        if (string.IsNullOrEmpty(path)) { continue; }
                        if (!path.StartsWith(Root, StringComparison.Ordinal))
                        {
                            if (outside != null) { outside.Add(path); }
                            continue;
                        }
                        if (inside == null) { continue; }

                        string rest = path.Substring(Root.Length);
                        int slash = rest.IndexOf('/');
                        string group = slash < 0 ? TopLevel : rest.Substring(0, slash);
                        string item = slash < 0 ? rest : rest.Substring(slash + 1);
                        inside.Add(new Entry(group, item, types[t].FullName));
                    }
                }
            }
        }
        /// <summary> Every menu item under the tool's root, sorted by group then item, handed out by copy. </summary>
        public static List<Entry> Entries()
        {
            if (_entries != null) { return new List<Entry>(_entries); }

            List<Entry> found = new List<Entry>();
            Collect(found, null);
            found.Sort(delegate (Entry x, Entry y)
            {
                int byGroup = string.CompareOrdinal(x.group, y.group);
                return byGroup != 0 ? byGroup : string.CompareOrdinal(x.item, y.item);
            });

            _entries = found;
            return new List<Entry>(_entries);
        }

        /// <summary> Any menu path this tool declares outside its own root, which RM-108 forbids. </summary>
        public static List<string> PathsOutsideTheRoot()
        {
            List<string> outside = new List<string>();
            Collect(null, outside);
            outside.Sort(StringComparer.Ordinal);
            return outside;
        }

        /// <summary> The groups the menu has: the top level first, then each submenu ordinally. </summary>
        public static List<string> Groups()
        {
            List<string> groups = new List<string>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            foreach (Entry e in Entries())
            {
                if (seen.ContainsKey(e.group)) { continue; }
                seen[e.group] = true;
                groups.Add(e.group);
            }
            return groups;
        }

        /// <summary> How many items one group holds. </summary>
        public static int CountIn(string group)
        {
            int n = 0;
            foreach (Entry e in Entries())
            {
                if (e.group == group) { n++; }
            }
            return n;
        }

        /// <summary> How many menu items the tool has, all told. </summary>
        public static int Total()
        {
            return Entries().Count;
        }

        /// <summary> How many rows <see cref="MapTable"/> writes: one per group, plus the total. </summary>
        public static int RowCount()
        {
            return Groups().Count + 1;
        }

        /// <summary> Two paths registered twice, which shadow each other in the menu and are counted twice here. </summary>
        public static List<string> DuplicatePaths()
        {
            Dictionary<string, int> times = new Dictionary<string, int>();
            foreach (Entry e in Entries())
            {
                string path = e.Path;
                times[path] = times.ContainsKey(path) ? times[path] + 1 : 1;
            }

            List<string> repeated = new List<string>();
            foreach (KeyValuePair<string, int> pair in times)
            {
                if (pair.Value > 1) { repeated.Add(pair.Key + " (" + pair.Value + " times)"); }
            }
            repeated.Sort(StringComparer.Ordinal);
            return repeated;
        }

        /// <summary> Gates in the suite whose own class declares no menu item, so they can only be run as part of a
        /// ninety-gate sweep. Named rather than counted, because the fix is one attribute in the file named. </summary>
        public static List<string> GatesWithoutMenuItem()
        {
            Dictionary<string, bool> owners = new Dictionary<string, bool>();
            foreach (Entry e in Entries())
            {
                if (!string.IsNullOrEmpty(e.owner)) { owners[e.owner] = true; }
            }

            List<string> missing = new List<string>();
            List<MethodInfo> gates = RcmGateSuite.Discover();
            for (int i = 0; i < gates.Count; i++)
            {
                Type owner = gates[i].DeclaringType;
                if (owner == null || owner.FullName == null) { continue; }
                if (!owners.ContainsKey(owner.FullName)) { missing.Add(owner.FullName); }
            }
            missing.Sort(StringComparer.Ordinal);
            return missing;
        }

        /// <summary> The menu map the manual states: one row per group with the number of items in it, then the total.
        /// The numbers are the attributes' own, which is the whole point -- § 3 of the manual is generated from here
        /// rather than counted into it. </summary>
        public static string MapTable()
        {
            string t = "| Submenu | Items |\n| --- | --- |";
            foreach (string group in Groups())
            {
                t += "\n| " + group + " | " + CountIn(group) + " |";
            }
            t += "\n| **Total** | **" + Total() + "** |";
            return t;
        }
    }
}
#endif
