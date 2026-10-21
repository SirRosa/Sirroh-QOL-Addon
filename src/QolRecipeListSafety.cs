using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    /// <summary>
    /// Valheim 1.0 UpdateRecipeList unconditionally dereferences Recipe.m_item,
    /// ItemData.m_shared and recipe UI fields. A stale mod recipe can otherwise
    /// throw during InventoryGui.Show, before any QOL Show postfix can run.
    /// This is a narrowly scoped input validation, not a replacement for vanilla
    /// crafting, and never destroys or edits ObjectDB recipes.
    /// </summary>
    internal static class QolRecipeListSafety
    {
        private static readonly FieldInfo RecipeRoot =
            AccessTools.Field(typeof(InventoryGui), "m_recipeListRoot");
        private static readonly FieldInfo Available =
            AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
        private static readonly FieldInfo TempItems =
            AccessTools.Field(typeof(InventoryGui), "m_tempItemList");
        private static bool _reportedLayout;
        private static readonly HashSet<string> ReportedBadRecipes =
            new HashSet<string>(StringComparer.Ordinal);

        internal static void Install(Plugin host)
        {
            host.Patch("QOL.RecipeListSafety", AccessTools.Method(
                    typeof(InventoryGui), "UpdateRecipeList",
                    new [] { typeof(List<Recipe>) }),
                prefix: AccessTools.Method(typeof(QolRecipeListSafety), nameof(Validate)),
                priority: Priority.First);
        }

        private static bool Validate(InventoryGui __instance, ref List<Recipe> __0)
        {
            if (__instance == null) return false;
            try
            {
                // On a normal initialized InventoryGui these are already set.
                // A UI replacement mod may clear references during rebuilding.
                if (RecipeRoot == null || Available == null || TempItems == null ||
                    RecipeRoot.GetValue(__instance) == null)
                {
                    if (!_reportedLayout)
                    {
                        _reportedLayout = true;
                        Plugin.Instance?.Log.LogWarning(
                            "RecipeListSafety: crafting list root is not initialized. " +
                            "Skipping recipe refresh rather than breaking the whole inventory window. " +
                            "Check for conflicting inventory UI replacement mods.");
                    }
                    return false;
                }

                if (Available.GetValue(__instance) == null)
                    Available.SetValue(__instance,
                        Activator.CreateInstance(Available.FieldType));
                if (TempItems.GetValue(__instance) == null)
                    TempItems.SetValue(__instance,
                        Activator.CreateInstance(TempItems.FieldType));

                if (ZoneSystem.instance == null) return false;
                if (__0 == null)
                {
                    __0 = new List<Recipe>(0);
                    Plugin.Instance?.Log.LogWarning(
                        "RecipeListSafety: received null recipe list; using empty list for this refresh.");
                    return true;
                }

                List<Recipe> safe = null;
                for (int i = 0; i < __0.Count; i++)
                {
                    Recipe recipe = __0[i];
                    bool valid = recipe != null && recipe.m_item != null &&
                        recipe.m_item.m_itemData != null &&
                        recipe.m_item.m_itemData.m_shared != null &&
                        recipe.m_resources != null;
                    if (valid)
                    {
                        if (safe != null) safe.Add(recipe);
                        continue;
                    }
                    if (safe == null)
                    {
                        safe = new List<Recipe>(__0.Count);
                        for (int j = 0; j < i; j++) safe.Add(__0[j]);
                    }
                    string name = recipe != null ? recipe.name : "(null recipe)";
                    if (ReportedBadRecipes.Add(name))
                        Plugin.Instance?.Log.LogWarning(
                            "RecipeListSafety: skipping broken recipe in UI refresh: " + name +
                            " (null item, shared data or requirements). Source ObjectDB is unchanged.");
                }
                if (safe != null) __0 = safe;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning(
                    "RecipeListSafety: validation failure (vanilla recipe data untouched): " + ex);
                return true;
            }
        }
    }
}
