using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    /// <summary>Bounded pickup of already loaded world drops near the build camera.</summary>
    internal static class RemoteCameraPickup
    {
        private static readonly FieldInfo InstancesField = AccessTools.Field(typeof(ItemDrop), "s_instances");
        private static float _nextScan;
        private static int _cursor;
        private static readonly HashSet<ZDOID> Cooldowns = new HashSet<ZDOID>();
        private static readonly Dictionary<ZDOID,float> RetryAfter = new Dictionary<ZDOID,float>();
        private static float _nextWarn;

        internal static void Tick(Player player, Vector3 camera, float range)
        {
            if (player == null || Time.time < _nextScan) return;
            _nextScan = Time.time + 0.25f;
            try
            {
                if (!(InstancesField?.GetValue(null) is List<ItemDrop> drops) || drops.Count == 0) return;
                Inventory inventory = player.GetInventory();
                if (inventory == null) return;
                int checkedCount = Math.Min(32, drops.Count);
                float rangeSq = range * range;
                for (int n=0; n<checkedCount; n++)
                {
                    if (_cursor >= drops.Count) _cursor = 0;
                    ItemDrop drop = drops[_cursor++];
                    if (drop == null || !drop.isActiveAndEnabled || !drop.gameObject.activeInHierarchy) continue;
                    if ((drop.transform.position - camera).sqrMagnitude > rangeSq) continue;
                    ZNetView view = drop.GetComponent<ZNetView>();
                    if (view == null || !view.IsValid() || view.GetZDO() == null) continue;
                    ZDOID id = view.GetZDO().m_uid;
                    if (id.IsNone()) continue;
                    if (RetryAfter.TryGetValue(id, out float next) && Time.time < next) continue;
                    RetryAfter[id] = Time.time + 2f;
                    // Respect Valheim ownership, item cooldowns, weight and
                    // inventory capacity via its own pickup path.
                    drop.Load();
                    if (drop.m_itemData == null || drop.m_itemData.m_shared == null ||
                        !drop.m_autoPickup || drop.IsPiece() || drop.InTar()) continue;
                    if (!inventory.CanAddItem(drop.m_itemData, -1)) continue;
                    if (inventory.GetTotalWeight() + drop.m_itemData.GetWeight(-1) > player.GetMaxCarryWeight()) continue;
                    drop.RequestOwn();
                    if (!drop.CanPickup(true)) continue;
                    player.Pickup(drop.gameObject, true, true);
                }
                if (RetryAfter.Count > 256)
                {
                    RetryAfter.Clear();
                    // Clearing the cooldown cache cannot duplicate items; the
                    // normal game pickup path handles ownership/deduplication.
                }
            }
            catch (Exception e)
            {
                if (Time.time >= _nextWarn)
                {
                    _nextWarn = Time.time + 10f;
                    Plugin.Instance?.Log.LogWarning("BuildCamera pickup: " + e);
                }
            }
        }

        internal static void Reset()
        {
            _nextScan = 0f;
            _cursor = 0;
            Cooldowns.Clear();
            RetryAfter.Clear();
        }
    }
}
