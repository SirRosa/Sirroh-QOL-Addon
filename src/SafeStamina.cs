using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class SafeStamina
    {
        private static Plugin _host;
        private static Player _cachedPlayer;
        private static float _nextScan;
        private static bool _cachedSafe;
        private static string _ignoredRaw;
        private static readonly HashSet<string> Ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Install(Plugin host)
        {
            _host = host;
            // Primary route: works with vanilla and any Sirroh Overhaul stamina calculation.
            host.Patch("SafeStamina.UseStamina", AccessTools.Method(typeof(Player), "UseStamina"),
                prefix: AccessTools.Method(typeof(SafeStamina), nameof(BeforeUseStamina)), priority: Priority.Last);
            // Also eliminate up-front stamina requirement checks for building and gathering.
            host.Patch("SafeStamina.Attack", AccessTools.Method(typeof(Attack), "GetAttackStamina"),
                postfix: AccessTools.Method(typeof(SafeStamina), nameof(AfterAttackCost)), priority: Priority.Last);
            host.Patch("SafeStamina.Build", AccessTools.Method(typeof(Player), "GetBuildStamina"),
                postfix: AccessTools.Method(typeof(SafeStamina), nameof(AfterBuildCost)), priority: Priority.Last);
        }

        private static bool BeforeUseStamina(Player __instance)
        {
            return !IsSafe(__instance);
        }

        private static void AfterAttackCost(Humanoid ___m_character, ref float __result)
        {
            if (__result > 0f && ___m_character is Player player && IsSafe(player)) __result = 0f;
        }

        private static void AfterBuildCost(Player __instance, ref float __result)
        {
            if (__result > 0f && IsSafe(__instance)) __result = 0f;
        }

        private static bool IsSafe(Player player)
        {
            if (_host == null || !_host.SafeStaminaEnabled.Value ||
                player == null || player != Player.m_localPlayer || player.IsDead()) return false;
            if (_cachedPlayer == player && Time.time < _nextScan) return _cachedSafe;
            _cachedPlayer = player;
            _nextScan = Time.time + 0.25f;
            _cachedSafe = true;
            string raw = _host.IgnoredCreatures.Value ?? "";
            if (raw != _ignoredRaw)
            {
                _ignoredRaw = raw;
                Ignored.Clear();
                foreach (string name in raw.Split(','))
                    if (!string.IsNullOrWhiteSpace(name)) Ignored.Add(name.Trim());
            }
            Vector3 center = player.transform.position;
            float r = _host.SafeRadius.Value;
            float radiusSqr = r * r;
            foreach (Character other in Character.GetAllCharacters())
            {
                if (other == null || other == player || other.IsDead()) continue;
                Vector3 delta = other.transform.position - center;
                if (delta.sqrMagnitude > radiusSqr) continue;
                if (Ignored.Count != 0 && Ignored.Contains(Utils.GetPrefabName(other.gameObject))) continue;
                if (!BaseAI.IsEnemy(player, other)) continue;
                _cachedSafe = false;
                break;
            }
            return _cachedSafe;
        }
    }
}
