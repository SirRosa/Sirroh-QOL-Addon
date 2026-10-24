using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    // Cached soft API links; no optional mod is a compile-time dependency.
    internal static class QolReclaimExtensions
    {
        private delegate bool RarityProbe(ItemDrop.ItemData item, ref int rarity);
        private static bool _epicBound, _gemBound;
        private static Func<ItemDrop.ItemData, bool> _isMagic;
        private static RarityProbe _tryRarity;
        private static Func<ItemDrop.ItemData, int, string> _getEnchantCosts;
        private static MethodInfo _getGems;
        private static FieldInfo _gemPrefab;
        private static readonly DataContractJsonSerializer AmountsReader =
            new DataContractJsonSerializer(typeof(List<Amount>));

        [DataContract]
        private sealed class Amount
        {
            [DataMember(Name = "Item")] public string Item;
            [DataMember(Name = "Amount")] public int Count;
        }

        private static void BindEpic()
        {
            if (_epicBound || !Chainloader.PluginInfos.ContainsKey("randyknapp.mods.epicloot")) return;
            Type api = Type.GetType("EpicLoot.API, EpicLoot", false);
            if (api == null) return;
            _epicBound = true;
            try
            {
                MethodInfo magic = AccessTools.Method(api, "IsMagicItem", new[] { typeof(ItemDrop.ItemData) });
                MethodInfo rarity = AccessTools.Method(api, "TryGetRarity",
                    new[] { typeof(ItemDrop.ItemData), typeof(int).MakeByRefType() });
                MethodInfo costs = AccessTools.Method(api, "GetEnchantCostsJson",
                    new[] { typeof(ItemDrop.ItemData), typeof(int) });
                if (magic != null && rarity != null && costs != null)
                {
                    _isMagic = (Func<ItemDrop.ItemData, bool>)
                        Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), magic);
                    _tryRarity = (RarityProbe)Delegate.CreateDelegate(typeof(RarityProbe), rarity);
                    _getEnchantCosts = (Func<ItemDrop.ItemData, int, string>)
                        Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, int, string>), costs);
                }
                else Plugin.Instance?.Log.LogWarning("Epic Loot: supported enchant cost API not available.");
            }
            catch (Exception ex) { Plugin.Instance?.Log.LogWarning("Epic Loot API binding: " + ex); }
        }

        private static void BindGems()
        {
            if (_gemBound || !Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.jewelcrafting")) return;
            Type api = Type.GetType("Jewelcrafting.API, Jewelcrafting", false);
            if (api == null) return;
            _gemBound = true;
            try
            {
                MethodInfo active = AccessTools.Method(api, "IsLoaded", Type.EmptyTypes);
                if (active != null && active.ReturnType == typeof(bool) &&
                    !(bool)active.Invoke(null, null)) return;
                _getGems = AccessTools.Method(api, "GetGems", new[] { typeof(ItemDrop.ItemData) });
                Type result = _getGems?.ReturnType;
                Type entry = result != null && result.IsGenericType
                    ? result.GetGenericArguments()[0] : null;
                _gemPrefab = entry == null ? null :
                    entry.GetField("gemPrefab", BindingFlags.Instance | BindingFlags.Public);
                if (_getGems == null || _gemPrefab == null)
                {
                    _getGems = null;
                    Plugin.Instance?.Log.LogWarning("Jewelcrafting: GetGems API not available.");
                }
            }
            catch (Exception ex)
            {
                _getGems = null;
                Plugin.Instance?.Log.LogWarning("Jewelcrafting API binding: " + ex);
            }
        }

        // Only scan custom metadata when a provider is missing or broken.
        private static bool HasForeignMetadata(ItemDrop.ItemData item, string a, string b)
        {
            if (item.m_customData == null) return false;
            foreach (string key in item.m_customData.Keys)
                if (key.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    key.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static bool AddByName(string prefabName, int amount,
            Action<GameObject, ItemDrop.ItemData, int> append)
        {
            if (amount <= 0) return true;
            if (string.IsNullOrEmpty(prefabName) || ObjectDB.instance == null) return false;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            ItemDrop drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return false;
            append(prefab, drop.m_itemData, amount);
            return true;
        }

        internal static bool Collect(ItemDrop.ItemData item, int count,
            Action<GameObject, ItemDrop.ItemData, int> append)
        {
            BindEpic();
            BindGems();
            try
            {
                if (Chainloader.PluginInfos.ContainsKey("randyknapp.mods.epicloot"))
                {
                    if (_isMagic == null || _tryRarity == null || _getEnchantCosts == null)
                    {
                        if (HasForeignMetadata(item, "epicloot", "magicitem")) return false;
                    }
                    else if (_isMagic(item))
                    {
                        if (count != 1) return false;
                        int rarity = 0;
                        if (!_tryRarity(item, ref rarity)) return false;
                        string json = _getEnchantCosts(item, rarity);
                        if (string.IsNullOrWhiteSpace(json)) return false;
                        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                        {
                            var amounts = (List<Amount>)AmountsReader.ReadObject(stream);
                            if (amounts != null)
                                for (int i = 0; i < amounts.Count; i++)
                                    if (!AddByName(amounts[i].Item, amounts[i].Count, append))
                                        return false;
                        }
                    }
                }
                if (Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.jewelcrafting"))
                {
                    if (_getGems == null || _gemPrefab == null)
                    {
                        if (HasForeignMetadata(item, "jewelcraft", "socket")) return false;
                    }
                    else
                    {
                        IEnumerable gems = _getGems.Invoke(null, new object[] { item }) as IEnumerable;
                        if (gems != null)
                            foreach (object gem in gems)
                            {
                                if (gem == null) continue;
                                if (count != 1 || !AddByName(_gemPrefab.GetValue(gem) as string, 1, append))
                                    return false;
                            }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log.LogWarning("Reclaim external material resolution failed: " + ex);
                return false; // Abort without modifying the inventory.
            }
        }

        internal static void Shutdown()
        {
            _epicBound = false; _gemBound = false;
            _isMagic = null; _tryRarity = null; _getEnchantCosts = null;
            _getGems = null; _gemPrefab = null;
        }
    }
}
