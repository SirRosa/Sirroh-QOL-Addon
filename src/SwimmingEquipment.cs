using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Sirroh.Qol
{
    // Recreates the equipped-item logic of WieldEquipmentWhileSwimming without
    // the reference mod's expensive StackTrace on Character.IsSwimming calls.
    internal enum SwimmingEquipmentMode
    {
        All,
        WeaponsOnly,
        ToolsOnly,
        Custom
    }

    internal static class SwimmingEquipment
    {
        private static Plugin _host;
        [ThreadStatic] private static Humanoid _scopedHumanoid;
        private static HashSet<string> _blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Install(Plugin host)
        {
            _host = host;
            RefreshLists();
            host.SwimmingBlacklist.SettingChanged += (_, __) => RefreshLists();
            host.SwimmingCustomAllowlist.SettingChanged += (_, __) => RefreshLists();

            // Only these Humanoid routines may temporarily see IsSwimming=false.
            // Swimming movement, drowning, stamina, environment and enemy AI
            // always get the original Character.IsSwimming result.
            int scoped = 0;
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Humanoid)))
            {
                if (method.Name != "EquipItem" && method.Name != "UpdateEquipment") continue;
                host.Patch("SwimmingEquipment." + method.Name, method,
                    prefix: AccessTools.Method(typeof(SwimmingEquipment), nameof(BeforeEquipment)),
                    postfix: AccessTools.Method(typeof(SwimmingEquipment), nameof(AfterEquipment)),
                    finalizer: AccessTools.Method(typeof(SwimmingEquipment), nameof(AfterEquipmentFinalizer)));
                scoped++;
            }
            host.Patch("SwimmingEquipment.IsSwimming",
                AccessTools.Method(typeof(Character), "IsSwimming", Type.EmptyTypes),
                prefix: AccessTools.Method(typeof(SwimmingEquipment), nameof(BeforeIsSwimming)),
                priority: Priority.Last);

            host.Log.LogInfo("SwimmingEquipment initialized: " + scoped +
                " Humanoid equipment entry points; no global swimming override.");
        }

        private static void BeforeEquipment(Humanoid __instance, out Humanoid __state)
        {
            __state = _scopedHumanoid;
            _scopedHumanoid = _host != null && _host.SwimmingEquipmentEnabled.Value &&
                __instance != null && __instance.IsPlayer() ? __instance : null;
        }

        private static void AfterEquipment(Humanoid __state)
        {
            _scopedHumanoid = __state;
        }

        // Harmony may execute finalizers even on a normal return. Reapplying the
        // original subject is idempotent and also covers thrown exceptions.
        private static Exception AfterEquipmentFinalizer(Exception __exception, Humanoid __state)
        {
            _scopedHumanoid = __state;
            return __exception;
        }

        private static bool BeforeIsSwimming(Character __instance, ref bool __result)
        {
            Humanoid current = _scopedHumanoid;
            if (current == null || __instance != current || _host == null ||
                !_host.SwimmingEquipmentEnabled.Value || !AllowedEquipment(current))
                return true;
            __result = false;
            return false;
        }

        private static bool AllowedEquipment(Humanoid player)
        {
            return AllowedItem(player.GetRightItem()) && AllowedItem(player.GetLeftItem());
        }

        private static bool AllowedItem(ItemDrop.ItemData item)
        {
            if (item == null) return true;
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (_blacklist.Contains(prefab)) return false;
            switch (_host.SwimmingEquipMode.Value)
            {
                case SwimmingEquipmentMode.WeaponsOnly:
                    return item.IsWeapon();
                case SwimmingEquipmentMode.ToolsOnly:
                    // Game's ItemType.Tool (19), as in the supplied reference.
                    return (int)item.m_shared.m_itemType == 19;
                case SwimmingEquipmentMode.Custom:
                    return _custom.Contains(prefab);
                default:
                    return true;
            }
        }

        private static HashSet<string> Parse(string entries)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(entries)) return values;
            foreach (string name in entries.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0) values.Add(trimmed);
            }
            return values;
        }

        private static void RefreshLists()
        {
            if (_host == null) return;
            _blacklist = Parse(_host.SwimmingBlacklist.Value);
            _custom = Parse(_host.SwimmingCustomAllowlist.Value);
        }

        internal static void Shutdown()
        {
            _scopedHumanoid = null;
            _host = null;
            _blacklist.Clear();
            _custom.Clear();
        }
    }
}
