using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace Sirroh.Qol
{
    /// <summary>
    /// AzuCraftyBoxes 1.8.28 excludes an otherwise valid chest when its creator
    /// ZDO value is zero. PlanBuild instantiates blueprint pieces before setting
    /// their creator, so Container.Awake can permanently miss those chests.
    ///
    /// Only relax that creator check for placeable chest prefabs. Azu's own
    /// enabled/pull toggle, ward/access checks, range, ownership, MineOnly and
    /// YAML restrictions continue to run without modification.
    /// </summary>
    internal static class QolAzuCraftyBoxesPlanBuildCompatibility
    {
        private const string AzuGuid = "Azumatt.AzuCraftyBoxes";
        private const string MiscFunctionsType = "AzuCraftyBoxes.Util.Functions.MiscFunctions";
        private static MethodInfo _shouldPrevent;
        private static Plugin _host;
        private static bool _warned;

        internal static void Install(Plugin host)
        {
            // Do not resolve or patch any optional Azu type unless it is installed.
            if (!Chainloader.PluginInfos.TryGetValue(AzuGuid, out var info) ||
                info == null || info.Instance == null) return;

            Type misc = info.Instance.GetType().Assembly.GetType(MiscFunctionsType, false);
            if (misc == null)
            {
                host.Log.LogWarning("AzuCraftyBoxes PlanBuild compatibility: expected API type unavailable.");
                return;
            }

            MethodInfo skip = AccessTools.Method(misc, "ShouldSkipContainer", new[] { typeof(Container) });
            MethodInfo prevent = AccessTools.Method(misc, "ShouldPrevent", Type.EmptyTypes);
            if (skip == null || !skip.IsStatic || skip.ReturnType != typeof(bool) ||
                prevent == null || !prevent.IsStatic || prevent.ReturnType != typeof(bool))
            {
                host.Log.LogWarning("AzuCraftyBoxes PlanBuild compatibility: unsupported Azu API; patch disabled.");
                return;
            }

            _host = host;
            _shouldPrevent = prevent;
            host.Patch("AzuCraftyBoxes.PlanBuildCreator", skip,
                postfix: AccessTools.Method(typeof(QolAzuCraftyBoxesPlanBuildCompatibility),
                    nameof(AfterShouldSkipContainer)));
        }

        private static void AfterShouldSkipContainer(Container container, ref bool __result)
        {
            // Preserve Azu's normal verdict, except for its creator==0 exclusion.
            if (!__result || _shouldPrevent == null || container == null) return;

            try
            {
                // The original method also returns true when the mod is disabled
                // or the player's no-pull toggle is active: never bypass either.
                if ((bool)_shouldPrevent.Invoke(null, null)) return;

                ZNetView view = QolGameAccess.View(container);
                if (view == null || !view.IsValid() || view.GetZDO() == null ||
                    container.GetInventory() == null ||
                    view.GetZDO().GetLong(ZDOVars.s_creator, 0L) != 0L) return;

                // Do not unlock dungeon/world loot or every unowned container.
                // PlanBuild's vanilla building chests are Piece-prefabs named
                // piece_chest, piece_chest_wood, piece_chest_blackmetal, etc.
                if (container.GetComponent<Piece>() == null ||
                    !container.gameObject.name.StartsWith("piece_chest", StringComparison.Ordinal))
                    return;

                // The caller in Azu still performs HasAccessToContainer;
                // the later query still checks MineOnly, InUse, range and YAML.
                __result = false;
            }
            catch (Exception error)
            {
                // Fail closed on an incompatible Azu version. Never spam logs.
                if (_warned) return;
                _warned = true;
                _host?.Log.LogWarning("AzuCraftyBoxes PlanBuild compatibility failed closed: " +
                    error.GetBaseException().Message);
            }
        }

        internal static void Shutdown()
        {
            _shouldPrevent = null;
            _host = null;
            _warned = false;
        }
    }
}
