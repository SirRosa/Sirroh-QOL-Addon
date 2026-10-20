using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class FoodFreeze
    {
        private static Plugin _host;
        private static readonly FieldInfo FoodTime = AccessTools.Field(typeof(Player.Food), "m_time");
        private static readonly MethodInfo DrainFilter = AccessTools.Method(typeof(FoodFreeze), nameof(FilterFoodDrain));
        private static readonly FieldInfo NativeNearFireTimer = AccessTools.Field(typeof(Player), "m_nearFireTimer");
        private static readonly Collider[] Nearby = new Collider[128];
        // Native player campfire effect is authoritative; collider search handles modded fireplaces.
        private static readonly int FireMask = LayerMask.GetMask("piece", "piece_nonsolid");
        private static Player _lastPlayer;
        private static float _nextFireScan;
        private static bool _nearFire;
        private static bool _loggedActivation;
        [ThreadStatic] private static bool _pauseFoodDecay;

        internal static void Install(Plugin host)
        {
            _host = host;
            MethodInfo update = AccessTools.Method(typeof(Player), "UpdateFood", new[] { typeof(float), typeof(bool) });
            if (update == null || FoodTime == null || DrainFilter == null)
            {
                host.Log.LogWarning("FoodFreeze: Valheim UpdateFood/Food.m_time API mismatch. Vanilla food logic retained.");
                return;
            }
            host.Patch("FoodFreeze.UpdateFood", update,
                prefix: AccessTools.Method(typeof(FoodFreeze), nameof(BeforeUpdate)),
                transpiler: AccessTools.Method(typeof(FoodFreeze), nameof(TranspileUpdateFood)),
                finalizer: AccessTools.Method(typeof(FoodFreeze), nameof(ClearAfterUpdate)));
        }

        private static void BeforeUpdate(Player __instance)
        {
            _pauseFoodDecay = __instance != null && __instance == Player.m_localPlayer &&
                              _host != null && _host.FoodFreezeEnabled.Value && ShouldFreeze(__instance);
            if (_pauseFoodDecay && !_loggedActivation)
            {
                _loggedActivation = true;
                _host.Log.LogInfo("FoodFreeze active: vanilla food decay is paused; food health/stamina/regen continue.");
            }
        }

        private static Exception ClearAfterUpdate(Exception __exception)
        {
            _pauseFoodDecay = false;
            return __exception;
        }

        private static float FilterFoodDrain(float seconds) => _pauseFoodDecay ? 0f : seconds;

        private static IEnumerable<CodeInstruction> TranspileUpdateFood(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            // Valheim 1.0: Food.m_time = Food.m_time - local_0, where local_0 = 1 second.
            // Interpose a no-allocation scalar filter ONLY on this subtraction;
            // ALL remaining Player.UpdateFood IL executes unmodified (including food regen).
            int found = 0;
            for (int i = 0; i < code.Count - 2; ++i)
            {
                if (code[i].opcode != OpCodes.Ldfld || !(code[i].operand is FieldInfo f) ||
                    f != FoodTime || code[i+2].opcode != OpCodes.Sub)
                    continue;
                OpCode load = code[i+1].opcode;
                if (load != OpCodes.Ldloc_0 && load != OpCodes.Ldloc_S && load != OpCodes.Ldloc)
                    continue;
                code.Insert(i+2, new CodeInstruction(OpCodes.Call, DrainFilter));
                found++;
                i += 3;
            }
            if (found != 1)
            {
                _host?.Log.LogWarning("FoodFreeze: expected 1 food timer subtraction; found " + found +
                                      ". Refusing unsafe patch.");
                throw new InvalidOperationException("Unsupported Player.UpdateFood implementation; cannot safely alter food decay.");
            }
            _host?.Log.LogInfo("FoodFreeze: patched exactly one food timer decrement, leaving other food effects intact.");
            return code;
        }

        private static bool ShouldFreeze(Player player)
        {
            SEMan effects = player.GetSEMan();
            if (_host.FreezeWhileResting.Value && effects != null &&
                effects.HaveStatusEffect(SEMan.s_statusEffectResting)) return true;
            if (!_host.FreezeNearFire.Value) return false;
            // Valheim 1.0 itself tracks proximity to active fires and exposes
            // the Fire effect even when the player is not under a roof.
            if (effects != null && effects.HaveStatusEffect(SEMan.s_statusEffectCampFire)) return true;
            if (NativeNearFireTimer != null)
            {
                float t = (float)NativeNearFireTimer.GetValue(player);
                if (t >= 0f && t < 0.25f) return true;
            }
            // In case a third-party fireplace lacks Valheim's native heat status:
            if (_lastPlayer == player && Time.time < _nextFireScan) return _nearFire;
            _lastPlayer = player;
            _nextFireScan = Time.time + 1f;
            _nearFire = false;
            int found = Physics.OverlapSphereNonAlloc(player.transform.position, _host.FireRadius.Value, Nearby, FireMask);
            for (int i = 0; i < found; ++i)
            {
                Collider c = Nearby[i];
                if (c == null) continue;
                Fireplace fire = c.GetComponentInParent<Fireplace>();
                if (fire != null && fire.IsBurning()) { _nearFire = true; break; }
            }
            return _nearFire;
        }
    }
}
