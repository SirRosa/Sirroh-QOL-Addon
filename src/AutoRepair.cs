using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class AutoRepair
    {
        private static Plugin _host;
        private static readonly MethodInfo HaveRepairable = AccessTools.Method(typeof(InventoryGui), "HaveRepairableItems");
        private static readonly MethodInfo RepairOne = AccessTools.Method(typeof(InventoryGui), "RepairOneItem");
        private static bool _pending;
        private static bool _errorLogged;

        internal static void Install(Plugin host)
        {
            _host = host;
            if (HaveRepairable == null || RepairOne == null)
            {
                host.Log.LogWarning("AutoRepair: vanilla repair methods are missing; hook not installed.");
                return;
            }
            host.Patch("AutoRepair", AccessTools.Method(typeof(CraftingStation), "Interact"),
                postfix: AccessTools.Method(typeof(AutoRepair), nameof(AfterInteract)));
        }

        private static void AfterInteract(CraftingStation __instance, Humanoid user, bool repeat, bool alt)
        {
            // VALHEIM 1.0: CraftingStation.Interact ALWAYS returns false, including
            // a successful call to InventoryGui.Show. Do not inspect __result.
            if (_host == null || !_host.AutoRepairEnabled.Value || repeat || alt || _pending ||
                Player.m_localPlayer == null || user != Player.m_localPlayer ||
                Player.m_localPlayer.GetCurrentCraftingStation() != __instance)
                return;
            _pending = true;
            _host.StartCoroutine(RepairWhenGuiReady(__instance));
        }

        private static IEnumerator RepairWhenGuiReady(CraftingStation station)
        {
            yield return null;
            try
            {
                Player player = Player.m_localPlayer;
                if (_host == null || !_host.AutoRepairEnabled.Value ||
                    station == null || player == null ||
                    player.GetCurrentCraftingStation() != station || !station.m_canRepair ||
                    !station.CheckUsable(player, false))
                    yield break;
                InventoryGui gui = InventoryGui.instance;
                if (gui == null) yield break;
                // RepairOneItem/HaveRepairableItems use Player.GetCurrentCraftingStation
                // and CanRepair -> correct station, upgrades, restrictions and durability.
                int maxAttempts = player.GetInventory().GetAllItems().Count;
                int repaired = 0;
                for (int i = 0; i < maxAttempts; ++i)
                {
                    if (!(bool)HaveRepairable.Invoke(gui, null)) break;
                    float before = TotalDurability(player);
                    RepairOne.Invoke(gui, null);
                    if (TotalDurability(player) <= before + 0.001f) break;
                    repaired++;
                }
                if (repaired > 0) _host.Log.LogInfo("AutoRepair: repaired " + repaired + " items at " + station.m_name);
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    _host.Log.LogWarning("AutoRepair attempt failed; vanilla repairs remain available: " + e);
                }
            }
            finally { _pending = false; }
        }

        private static float TotalDurability(Player player)
        {
            float sum = 0f;
            var items = player.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; ++i)
                if (items[i] != null && items[i].m_shared.m_useDurability) sum += items[i].m_durability;
            return sum;
        }
    }
}
