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
        private static readonly FieldInfo CurrentStation = AccessTools.Field(typeof(InventoryGui), "m_currentCraftingStation");
        private static bool _pending;
        private static bool _errorLogged;

        internal static void Install(Plugin host)
        {
            _host = host;
            if (HaveRepairable == null || RepairOne == null)
            {
                host.Logger.LogWarning("AutoRepair: vanilla GUI repair methods missing; leaving vanilla intact.");
                return;
            }
            host.Patch("AutoRepair", AccessTools.Method(typeof(CraftingStation), "Interact"),
                postfix: AccessTools.Method(typeof(AutoRepair), nameof(AfterInteract)));
        }

        private static void AfterInteract(CraftingStation __instance, Humanoid user, bool repeat, bool alt, bool __result)
        {
            if (!_host.AutoRepairEnabled.Value || !__result || repeat || alt || _pending ||
                Player.m_localPlayer == null || user != Player.m_localPlayer)
                return;
            _pending = true;
            _host.StartCoroutine(RepairWhenGuiReady(__instance));
        }

        private static IEnumerator RepairWhenGuiReady(CraftingStation station)
        {
            // Crafting GUI initialization can happen after Interact; never race it.
            yield return null;
            try
            {
                if (!_host.AutoRepairEnabled.Value || station == null || Player.m_localPlayer == null)
                    yield break;
                InventoryGui gui = InventoryGui.instance;
                if (gui == null) yield break;
                // Fail closed if the GUI exposes a selected station and it does not match.
                if (CurrentStation != null && CurrentStation.GetValue(gui) != station) yield break;
                // One attempt per carried item. The vanilla routines enforce station level,
                // proper repair eligibility and other modded crafting rules.
                int limit = Player.m_localPlayer.GetInventory().GetAllItems().Count;
                int repaired = 0;
                for (int i = 0; i < limit; ++i)
                {
                    if (!(bool)HaveRepairable.Invoke(gui, null)) break;
                    float before = TotalDurability(Player.m_localPlayer);
                    RepairOne.Invoke(gui, null);
                    float after = TotalDurability(Player.m_localPlayer);
                    if (after <= before + 0.001f) break; // no progress: never loop indefinitely
                    repaired++;
                }
                if (repaired > 0) _host.Logger.LogDebug("AutoRepair: " + repaired + " items repaired.");
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    _host.Logger.LogWarning("AutoRepair disabled for this attempt: " + e);
                }
            }
            finally { _pending = false; }
        }

        private static float TotalDurability(Player p)
        {
            float sum = 0f;
            var items = p.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].m_shared.m_useDurability) sum += items[i].m_durability;
            return sum;
        }
    }
}
