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
        private static Func<object, Vector2i, bool> _azuSlotFavorite;
        private static MethodInfo _azuNameFavorite;
        private static MethodInfo _azuToggleName;
        private static MethodInfo _azuToggleSlot;
        private static long _cachedPlayerId = long.MinValue;
        private static object _cachedConfig;
        private static bool _callingFromSirroh;
        private static int _lastSirrohDispatchFrame = -1;
        private static int _lastNativeDispatchFrame = -1;

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
                    // Suppress only Azu's redundant FAVORITE BORDER renderer.
                    // The native favorite toggles, search, and persistence remain usable.
                    Type borderType = AccessTools.TypeByName("AzuAutoStore.Patches.Favoriting.BorderRenderer");
                    MethodInfo borderDraw = borderType == null ? null :
                        AccessTools.Method(borderType, "UpdateGui");
                    if (borderDraw != null)
                        _host.Patch("AzuAutoStore.DuplicateFavoriteBorders", borderDraw,
                            prefix: AccessTools.Method(typeof(QolAzuAutoStoreCompatibility),
                                nameof(BeforeAzuBorderDraw)), priority: Priority.First);
                    MethodInfo slotCheck = AccessTools.Method(favorites,
                        "IsSlotFavorited", new[] { typeof(Vector2i) });
                    _azuNameFavorite = AccessTools.Method(favorites, "IsItemNameFavorited");
                    _azuToggleName = AccessTools.Method(favorites, "ToggleItemNameFavoriting");
                    _azuToggleSlot = AccessTools.Method(favorites,
                        "ToggleSlotFavoriting", new[] { typeof(Vector2i) });
                    if (slotCheck != null)
                    {
                        ParameterExpression slot = Expression.Parameter(typeof(Vector2i), "slot");
                        _azuSlotFavorite = Expression.Lambda<Func<object, Vector2i, bool>>(
                            Expression.Call(Expression.Convert(config, favorites), slotCheck, slot),
                            config, slot).Compile();
                    }
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
            if (Input.GetKeyDown(KeyCode.Q) || _lastSirrohDispatchFrame == Time.frameCount)
                return false;
            _lastNativeDispatchFrame = Time.frameCount;
            return true;
        }

        internal static bool DispatchQuickStack()
        {
            if (!UseAzuQuickStack) return false;
            if (_lastSirrohDispatchFrame == Time.frameCount ||
                _lastNativeDispatchFrame == Time.frameCount) return true;
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

        internal static bool IsDetected => _detected;

        private static bool BeforeAzuBorderDraw(InventoryGrid __instance)
        {
            // Sirroh draws the combined favorites overlay, including empty Azu slots.
            return InventoryGui.instance == null ||
                   __instance != InventoryGui.instance.m_playerGrid;
        }

        private static void AfterAzuFavoriteCheck(ItemDrop.ItemData __0, ref bool __result)
        {
            if (!__result && __0 != null && QolFavorites.IsSirrohFavoriteForAzu(__0))
                __result = true;
        }


        private static object PlayerFavorites(Inventory inventory)
        {
            if (_getPlayerConfig == null || Player.m_localPlayer == null ||
                inventory == null || inventory != Player.m_localPlayer.GetInventory()) return null;
            long uid = Player.m_localPlayer.GetPlayerID();
            if (_cachedConfig == null || _cachedPlayerId != uid)
            {
                _cachedConfig = _getPlayerConfig(uid);
                _cachedPlayerId = uid;
            }
            return _cachedConfig;
        }

        internal static bool IsAzuSlotFavorite(Vector2i slot, Inventory inventory)
        {
            if (_azuSlotFavorite == null) return false;
            try
            {
                object config = PlayerFavorites(inventory);
                return config != null && _azuSlotFavorite(config, slot);
            }
            catch (Exception error)
            {
                _host?.Log.LogWarning("Azu slot favorite check unavailable: " + error.Message);
                _azuSlotFavorite = null;
                return false;
            }
        }

        internal static bool IsAzuNameFavorite(ItemDrop.ItemData item, Inventory inventory)
        {
            if (_azuNameFavorite == null || item == null) return false;
            try
            {
                object config = PlayerFavorites(inventory);
                return config != null && (bool)_azuNameFavorite.Invoke(config, new object[] { item.m_shared });
            }
            catch (Exception error)
            {
                _host?.Log.LogWarning("Azu name favorite check unavailable: " + error.Message);
                _azuNameFavorite = null;
                return false;
            }
        }

        // Use Azu's own public API on explicit user clicks, not its binary save format.
        internal static void SetAzuNameFavorite(ItemDrop.ItemData item, Inventory inventory, bool selected)
        {
            if (_azuToggleName == null || item == null) return;
            try
            {
                object config = PlayerFavorites(inventory);
                if (config != null && IsAzuNameFavorite(item, inventory) != selected)
                    _azuToggleName.Invoke(config, new object[] { item.m_shared });
            }
            catch (Exception error)
            {
                _host?.Log.LogWarning("Azu name favorite update unavailable: " + error.Message);
            }
        }

        internal static void SetAzuSlotFavorite(Vector2i slot, Inventory inventory, bool selected)
        {
            if (_azuToggleSlot == null || _azuSlotFavorite == null) return;
            try
            {
                object config = PlayerFavorites(inventory);
                if (config != null && _azuSlotFavorite(config, slot) != selected)
                    _azuToggleSlot.Invoke(config, new object[] { slot });
            }
            catch (Exception error)
            {
                _host?.Log.LogWarning("Azu slot favorite update unavailable: " + error.Message);
            }
        }

        internal static bool IsAzuFavorite(ItemDrop.ItemData item, Inventory inventory)
        {
            if (_azuFavorite == null || _getPlayerConfig == null || item == null ||
                inventory == null || Player.m_localPlayer == null ||
                inventory != Player.m_localPlayer.GetInventory()) return false;
            try
            {
                object config = PlayerFavorites(inventory);
                return config != null && _azuFavorite(config, item);
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
            _azuSlotFavorite = null;
            _azuNameFavorite = null;
            _azuToggleName = null;
            _azuToggleSlot = null;
            _cachedConfig = null;
            _cachedPlayerId = long.MinValue;
            _callingFromSirroh = false;
            _lastSirrohDispatchFrame = -1;
            _lastNativeDispatchFrame = -1;
        }
    }
}
