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
        private static readonly Dictionary<ZDOID,float> RetryAfter = new Dictionary<ZDOID,float>();
        private static float _nextWarn;
        private static int _pickedFromCamera;
        private static bool _loggedCameraOrigin;
        private static readonly FieldInfo WardsField = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
        private static readonly Func<PrivateArea, bool> WardEnabled =
            AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "IsEnabled"));
        private static readonly Func<PrivateArea, Vector3, float, bool> WardInside =
            AccessTools.MethodDelegate<Func<PrivateArea, Vector3, float, bool>>(AccessTools.Method(typeof(PrivateArea), "IsInside"));
        private static readonly Func<PrivateArea, bool> WardLocalAccess =
            AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "HaveLocalAccess"));

        internal static void Tick(Player player, Vector3 camera, float range)
        {
            if (player == null || Time.time < _nextScan) return;
            _nextScan = Time.time + 0.25f;
            try
            {
                if (!(InstancesField?.GetValue(null) is List<ItemDrop> drops) || drops.Count == 0) return;
                Inventory inventory = player.GetInventory();
                if (inventory == null) return;
                int checkedCount = Math.Min(64, drops.Count);
                float rangeSq = range * range;
                for (int n=0; n<checkedCount; n++)
                {
                    if (_cursor >= drops.Count) _cursor = 0;
                    ItemDrop drop = drops[_cursor++];
                    if (drop == null || !drop.isActiveAndEnabled || !drop.gameObject.activeInHierarchy) continue;
                    // Strictly camera-relative. Never use player's transform or
                    // the vanilla auto-pickup radius as the reach center.
                    Vector3 offsetFromCamera = drop.transform.position - camera;
                    if (offsetFromCamera.sqrMagnitude > rangeSq) continue;
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
                    if (drop.GetComponent<Fish>() != null || !WardAllows(drop.transform.position)) continue;
                    if (!inventory.CanAddItem(drop.m_itemData, -1)) continue;
                    if (inventory.GetTotalWeight() + drop.m_itemData.GetWeight(-1) > player.GetMaxCarryWeight()) continue;
                    drop.RequestOwn();
                    if (!drop.CanPickup(true)) continue;
                    if (player.Pickup(drop.gameObject, true, true))
                    {
                        _pickedFromCamera++;
                        if (!_loggedCameraOrigin)
                        {
                            _loggedCameraOrigin = true;
                            Plugin.Instance?.Log.LogInfo("BuildCamera pickup: camera-centered collection confirmed; first pickup #" +
                                _pickedFromCamera + ", camera radius " + range.ToString("0.0") + "m.");
                        }
                    }
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

        private static bool WardAllows(Vector3 target)
        {
            if (!(WardsField?.GetValue(null) is List<PrivateArea> wards)) return false;
            foreach (PrivateArea ward in wards)
            {
                if (ward == null) continue;
                Piece piece = ward.GetComponent<Piece>();
                if (piece != null && piece.IsPlacedByPlayer() &&
                    WardEnabled(ward) && WardInside(ward, target, 0f) &&
                    !WardLocalAccess(ward))
                    return false;
            }
            return true;
        }

        internal static void Reset()
        {
            _nextScan = 0f;
            _cursor = 0;
            RetryAfter.Clear();
            _pickedFromCamera = 0;
            _loggedCameraOrigin = false;
        }
    }
}
