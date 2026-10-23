using System;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    // Optional soft integration: absolutely no assembly reference or dependency on AzuAutoStore.
    internal enum QolQuickStackEngine { Sirroh, AzuAutoStore }

    internal static class QolAzuAutoStoreCompatibility
    {
        private const string AzuGuid = "Azumatt.AzuAutoStore";
        private static Plugin _host;
        private static bool _detected;
        private static bool _bound;
        private static float _nextDetection;
        private static Action _azuManualStore;
        private static Func<long, object> _getPlayerConfig;
        private static Func<object, ItemDrop.ItemData, bool> _azuFavorite;
        private static long _cachedPlayerId = long.MinValue;
        private static object _cachedConfig;
        private static bool _callingFromSirroh;
        private static int _lastSirrohDispatchFrame = -1;

        internal static bool UseAzuQuickStack =>
            _detected && _azuManualStore != null && _host != null &&
            _host.QuickStackEngine.Value == QolQuickStackEngine.AzuAutoStore;

        internal static void Install(Plugin host)
        {
            _host = host;
            TryBind();
        }

        internal static void Tick()
        {
            // Other plugins may finish loading after our Awake; retry only every five seconds.
            if (_bound || _host == null || Time.unscaledTime < _nextDetection) return;
            _nextDetection = Time.unscaledTime + 5f;
            TryBind();
        }

        private static void TryBind()
        {
            if (_bound || _host == null || !Chainloader.PluginInfos.ContainsKey(AzuGuid)) return;
            _detected = true;
            _bound = true;
            try
            {
                Type functions = AccessTools.TypeByName("AzuAutoStore.Util.Functions");
                MethodInfo store = functions == null ? null :
                    AccessTools.Method(functions, "TryStore", Type.EmptyTypes);
                if (store != null && store.IsStatic && store.ReturnType == typeof(void))
                {
                    // The parameterless manual Store shortcut is not the background
                    // auto-store, floor pickup or special-inventory storage path.
                    _azuManualStore = (Action)Delegate.CreateDelegate(typeof(Action), store);
                    _host.Patch("AzuAutoStore.ManualStore", store,
                        prefix: AccessTools.Method(typeof(QolAzuAutoStoreCompatibility),
                            nameof(BeforeAzuManualStore)), priority: Priority.First);
                }
                else _host.Log.LogWarning("AzuAutoStore: manual TryStore() API unavailable; Sirroh backend remains active.");

                Type favorites = AccessTools.TypeByName("AzuAutoStore.Patches.Favoriting.UserConfig");
                MethodInfo getter = favorites == null ? null :
                    AccessTools.Method(favorites, "GetPlayerConfig", new[] { typeof(long) });
                MethodInfo check = favorites == null ? null :
                    AccessTools.Method(favorites, "IsItemNameOrSlotFavorited", new[] { typeof(ItemDrop.ItemData) });
                if (getter != null && check != null && getter.IsStatic &&
                    check.ReturnType == typeof(bool))
                {
                    // Compile strongly-typed delegates once; no per-frame MethodInfo.Invoke allocations.
                    ParameterExpression uid = Expression.Parameter(typeof(long), "uid");
                    _getPlayerConfig = Expression.Lambda<Func<long, object>>(
                        Expression.Convert(Expression.Call(getter, uid), typeof(object)), uid).Compile();
                    ParameterExpression config = Expression.Parameter(typeof(object), "config");
                    ParameterExpression item = Expression.Parameter(typeof(ItemDrop.ItemData), "item");
                    _azuFavorite = Expression.Lambda<Func<object, ItemDrop.ItemData, bool>>(
                        Expression.Call(Expression.Convert(config, favorites), check, item),
                        config, item).Compile();
                    _host.Patch("AzuAutoStore.Favorites", check,
                        postfix: AccessTools.Method(typeof(QolAzuAutoStoreCompatibility),
                            nameof(AfterAzuFavoriteCheck)), priority: Priority.Last);
                }
                else _host.Log.LogWarning("AzuAutoStore: favorite API unavailable; favorite integration skipped.");

                _host.Log.LogInfo("AzuAutoStore detected: QuickStack backend=" +
                    _host.QuickStackEngine.Value + ", manual store and favorite compatibility hooks ready.");
            }
            catch (Exception error)
            {
                _host.Log.LogWarning("AzuAutoStore compatibility initialization failed; Sirroh remains usable: " + error);
                _azuManualStore = null;
                _getPlayerConfig = null;
                _azuFavorite = null;
            }
        }

        // Called by Harmony on Azu's parameterless manual Functions.TryStore() only.
        private static bool BeforeAzuManualStore()
        {
            if (_callingFromSirroh) return true;
            if (!UseAzuQuickStack) return false; // Sirroh selected: suppress overlapping Azu hotkey.
            // If the user mapped Azu's manual shortcut to Q, Sirroh Q owns the
            // request. Preserve Azu's independent shortcut on other keys.
            return !Input.GetKeyDown(KeyCode.Q) && _lastSirrohDispatchFrame != Time.frameCount;
        }

        internal static bool DispatchQuickStack()
        {
            if (!UseAzuQuickStack) return false;
            if (_lastSirrohDispatchFrame == Time.frameCount) return true;
            _lastSirrohDispatchFrame = Time.frameCount;
            _callingFromSirroh = true;
            try
            {
                _azuManualStore(); // Azu owns authorization, networking, effects and messages.
            }
            catch (Exception error)
            {
                // Never fall back after a potentially partially completed external transfer.
                _host.Log.LogError("AzuAutoStore QuickStack failed (no duplicate fallback): " + error);
                Player player = Player.m_localPlayer;
                if (player != null)
                    player.Message(MessageHud.MessageType.TopLeft, "AzuAutoStore: błąd QuickStack (sprawdź log).");
            }
            finally { _callingFromSirroh = false; }
            return true;
        }

        private static void AfterAzuFavoriteCheck(ItemDrop.ItemData __0, ref bool __result)
        {
            if (!__result && __0 != null && QolFavorites.IsSirrohFavoriteForAzu(__0))
                __result = true;
        }

        internal static bool IsAzuFavorite(ItemDrop.ItemData item, Inventory inventory)
        {
            if (_azuFavorite == null || _getPlayerConfig == null || item == null ||
                inventory == null || Player.m_localPlayer == null ||
                inventory != Player.m_localPlayer.GetInventory()) return false;
            long uid = Player.m_localPlayer.GetPlayerID();
            try
            {
                if (_cachedConfig == null || _cachedPlayerId != uid)
                {
                    _cachedPlayerId = uid;
                    _cachedConfig = _getPlayerConfig(uid);
                }
                return _cachedConfig != null && _azuFavorite(_cachedConfig, item);
            }
            catch (Exception error)
            {
                // An incompatible Azu version must not break normal favorites/QuickStack.
                _host?.Log.LogWarning("Azu favorite check unavailable: " + error.Message);
                _azuFavorite = null;
                return false;
            }
        }

        internal static void Shutdown()
        {
            _host = null;
            _detected = false;
            _bound = false;
            _azuManualStore = null;
            _azuFavorite = null;
            _getPlayerConfig = null;
            _cachedConfig = null;
            _cachedPlayerId = long.MinValue;
            _callingFromSirroh = false;
            _lastSirrohDispatchFrame = -1;
        }
    }
}
