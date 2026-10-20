using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class FoodFreeze
    {
        private static Plugin _host;
        private static readonly FieldInfo Foods = AccessTools.Field(typeof(Player), "m_foods");
        private static readonly FieldInfo FoodTime = typeof(Player).GetNestedType("Food",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetField("m_time",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Collider[] Nearby = new Collider[128];
        private static readonly int FireMask = LayerMask.GetMask("piece", "piece_nonsolid");
        private static readonly List<object> Entries = new List<object>(4);
        private static readonly List<float> Times = new List<float>(4);
        private static Player _lastPlayer;
        private static float _nextFireScan;
        private static bool _nearFire;
        private static bool _warning;

        internal static void Install(Plugin host)
        {
            _host = host;
            if (Foods == null || FoodTime == null)
            {
                host.Logger.LogWarning("FoodFreeze: food timer layout changed; safe fallback is to leave food untouched.");
                return;
            }
            int count = 0;
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Player)))
            {
                if (method.Name != "UpdateFood" || method.ReturnType != typeof(void)) continue;
                host.Patch("FoodFreeze", method,
                    prefix: AccessTools.Method(typeof(FoodFreeze), nameof(BeforeUpdate)),
                    postfix: AccessTools.Method(typeof(FoodFreeze), nameof(AfterUpdate)));
                count++;
            }
            if (count == 0) host.Logger.LogWarning("FoodFreeze: Player.UpdateFood not found.");
        }

        private static void BeforeUpdate(Player __instance, out bool __state)
        {
            __state = false;
            if (_host == null || !_host.FoodFreezeEnabled.Value ||
                __instance == null || __instance != Player.m_localPlayer || !ShouldFreeze(__instance))
                return;
            try
            {
                IList foods = Foods.GetValue(__instance) as IList;
                if (foods == null) return;
                Entries.Clear();
                Times.Clear();
                for (int i = 0; i < foods.Count; ++i)
                {
                    object food = foods[i];
                    if (food == null) continue;
                    float original = (float)FoodTime.GetValue(food);
                    Entries.Add(food);
                    Times.Add(original);
                    // Keep the rest of UpdateFood active. Add a temporary safety margin
                    // to avoid removing food at expiry while a timer is frozen.
                    FoodTime.SetValue(food, original + 120f);
                }
                __state = true;
            }
            catch (Exception e) { WarnOnce(e); Entries.Clear(); Times.Clear(); }
        }

        private static void AfterUpdate(bool __state)
        {
            if (!__state) return;
            try
            {
                for (int i = 0; i < Entries.Count; ++i)
                    FoodTime.SetValue(Entries[i], Times[i]);
            }
            catch (Exception e) { WarnOnce(e); }
            finally { Entries.Clear(); Times.Clear(); }
        }

        private static bool ShouldFreeze(Player player)
        {
            if (_host.FreezeWhileResting.Value)
            {
                SEMan status = player.GetSEMan();
                if (status != null && status.HaveStatusEffect(SEMan.s_statusEffectResting)) return true;
            }
            if (!_host.FreezeNearFire.Value) return false;
            if (_lastPlayer == player && Time.time < _nextFireScan) return _nearFire;
            _lastPlayer = player;
            _nextFireScan = Time.time + 0.75f;
            _nearFire = false;
            float radius = _host.FireRadius.Value;
            int count = Physics.OverlapSphereNonAlloc(player.transform.position, radius, Nearby, FireMask);
            for (int i = 0; i < count; ++i)
            {
                Collider collider = Nearby[i];
                if (collider == null) continue;
                Fireplace fire = collider.GetComponentInParent<Fireplace>();
                if (fire != null && fire.IsBurning()) { _nearFire = true; break; }
            }
            return _nearFire;
        }

        private static void WarnOnce(Exception e)
        {
            if (_warning) return;
            _warning = true;
            _host.Logger.LogWarning("FoodFreeze failed safely; vanilla UpdateFood remains active: " + e);
        }
    }
}
