using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Sirroh.Qol
{
    // Zero per-frame indexing. Prebuilt dictionaries provide O(1) average lookup.
    internal static class QolRecipeCache
    {
        private static readonly Dictionary<string, Recipe> ByPrefab =
            new Dictionary<string, Recipe>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Recipe> BySharedName =
            new Dictionary<string, Recipe>(StringComparer.Ordinal);
        private static ObjectDB _source;
        private static int _count = -1;
        private static Recipe _first, _last;
        private static bool _dirty = true;

        internal static void Install(Plugin host)
        {
            // Only invalidate on a database lifecycle event. Build lazily on reclaim,
            // after all other mods have had a chance to register their recipes.
            string[] mutations = { "Awake", "CopyOtherDB", "UpdateRecipes" };
            for (int i = 0; i < mutations.Length; i++)
            {
                var method = AccessTools.Method(typeof(ObjectDB), mutations[i]);
                if (method != null)
                    host.Patch("ReclaimCache." + mutations[i], method,
                        postfix: AccessTools.Method(typeof(QolRecipeCache), nameof(Invalidate)));
            }
        }

        private static void Invalidate() { _dirty = true; }

        private static string Normalized(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            const string suffix = "(Clone)";
            return name.EndsWith(suffix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - suffix.Length).TrimEnd() : name;
        }

        private static bool Valid(Recipe recipe) =>
            recipe != null && recipe.m_item != null && recipe.m_item.m_itemData != null &&
            recipe.m_amount > 0 && recipe.m_resources != null && recipe.m_resources.Length > 0;

        private static void Add(Dictionary<string,Recipe> cache, string key, Recipe recipe)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!cache.TryGetValue(key, out Recipe current) || recipe.m_amount < current.m_amount)
                cache[key] = recipe;
        }

        private static void Ensure()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_recipes == null) { Shutdown(); return; }
            var list = db.m_recipes;
            int length = list.Count;
            Recipe first = length != 0 ? list[0] : null;
            Recipe last = length != 0 ? list[length - 1] : null;
            if (!_dirty && ReferenceEquals(db, _source) && _count == length &&
                ReferenceEquals(first, _first) && ReferenceEquals(last, _last)) return;
            if (length == 0) { Invalidate(); return; }
            ByPrefab.Clear();
            BySharedName.Clear();
            for (int i = 0; i < length; i++)
            {
                Recipe recipe = list[i];
                if (!Valid(recipe)) continue;
                Add(ByPrefab, Normalized(recipe.m_item.gameObject.name), recipe);
                var shared = recipe.m_item.m_itemData.m_shared;
                if (shared != null) Add(BySharedName, shared.m_name, recipe);
            }
            _source = db; _count = length; _first = first; _last = last; _dirty = false;
        }

        internal static Recipe Find(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;
            Ensure();
            string prefab = item.m_dropPrefab != null ? Normalized(item.m_dropPrefab.name) : null;
            if (prefab != null && ByPrefab.TryGetValue(prefab, out Recipe exact) && Valid(exact))
                return exact;
            // Dynamic registry fallback before weaker shared-name matching.
            ObjectDB db = ObjectDB.instance;
            if (db != null)
            {
                try
                {
                    Recipe registered = db.GetRecipe(item);
                    if (Valid(registered)) return registered;
                }
                catch (Exception ex)
                {
                    Plugin.Instance?.Log.LogDebug("Reclaim recipe fallback: " + ex.Message);
                }
            }
            return BySharedName.TryGetValue(item.m_shared.m_name, out Recipe fallback) && Valid(fallback)
                ? fallback : null;
        }

        internal static void Shutdown()
        {
            ByPrefab.Clear(); BySharedName.Clear();
            _source = null; _first = null; _last = null; _count = -1; _dirty = true;
        }
    }
}
