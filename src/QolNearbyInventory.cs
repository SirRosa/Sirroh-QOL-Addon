using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sirroh.Qol
{
    /// <summary>
    /// Key-activated only: no per-frame chest scans, per-frame network requests or
    /// item-name cross-products. Ownership acquisition is awaited before mutation.
    /// </summary>
    internal static class QolNearbyInventory
    {
        private const float RangeSq = 100f * 100f;
        private static Plugin _host;
        private static readonly HashSet<Container> Known = new HashSet<Container>();
        private static readonly List<Container> Nearby = new List<Container>(128);
        private static readonly HashSet<string> Existing = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<ItemDrop.ItemData> PlayerItems = new List<ItemDrop.ItemData>(64);
        private static bool _busy;
        private static float _nextKey;
        private static bool _reportedNoChests;

        internal static void Install(Plugin host)
        {
            _host = host;
            host.Patch("QOL.Containers.Awake", AccessTools.Method(typeof(Container), "Awake"),
                postfix: AccessTools.Method(typeof(QolNearbyInventory), nameof(OnContainerAwake)));
        }

        private static void OnContainerAwake(Container __instance)
        {
            if (__instance != null) Known.Add(__instance);
        }

        internal static void Tick()
        {
            if (_host == null || _busy || Time.unscaledTime < _nextKey || !Application.isFocused ||
                Console.IsVisible()) return;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting() || InventoryGui.instance == null)
                return;
            if (InventoryGui.instance.m_dragItem != null) return;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.GetComponentInParent<TMP_InputField>() != null) return;
            bool quick = Input.GetKeyDown(KeyCode.Q);
            bool restock = !quick && Input.GetKeyDown(KeyCode.R);
            if (!quick && !restock) return;
            _nextKey = Time.unscaledTime + .25f;
            _host.StartCoroutine(Execute(player, quick));
        }

        private static IEnumerator Execute(Player player, bool quick)
        {
            if (_busy) yield break;
            _busy = true;
            int moved = 0, touched = 0, skipped = 0;
            try
            {
                Inventory src = player.GetInventory();
                if (src == null) yield break;
                GetContainers(player);
                if (Nearby.Count == 0)
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Brak dostępnych skrzyń w zasięgu 100 m.");
                    yield break;
                }
                // Inventory item enumeration happens once per request.
                PlayerItems.Clear();
                foreach (ItemDrop.ItemData item in src.GetAllItems())
                {
                    if (item == null || item.m_shared == null || item.m_gridPos.y == 0) continue;
                    if (item.m_equipped) continue;
                    if (item.m_shared.m_maxStackSize <= 1) continue;
                    if (quick && QolFavorites.IsFavorite(item, src)) continue;
                    if (!quick && item.m_stack >= item.m_shared.m_maxStackSize) continue;
                    PlayerItems.Add(item);
                }
                if (PlayerItems.Count == 0)
                {
                    player.Message(MessageHud.MessageType.TopLeft, quick
                        ? "Quickstack: brak przedmiotów do przeniesienia."
                        : "Restock: brak niepełnych stosów.");
                    yield break;
                }

                // Operate on at most 128 loaded containers per press; each
                // container is acquired once and processed as a single batch.
                for (int ci = 0; ci < Nearby.Count && ci < 128; ++ci)
                {
                    Container chest = Nearby[ci];
                    if (chest == null || !CanAccess(chest, player)) { skipped++; continue; }
                    ZNetView view = chest.m_nview;
                    if (!view.IsOwner())
                    {
                        view.ClaimOwnership();
                        float deadline = Time.realtimeSinceStartup + .9f;
                        while (view != null && !view.IsOwner() && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        if (view == null || !view.IsOwner()) { skipped++; continue; }
                    }
                    if (!CanAccess(chest, player)) { skipped++; continue; }
                    Inventory inventory = chest.m_inventory;
                    if (inventory == null || inventory == src) continue;
                    Existing.Clear();
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                        if (item?.m_shared != null) Existing.Add(item.m_shared.m_name);
                    if (Existing.Count == 0) continue;

                    int movedChest = quick ? QuickStack(src, inventory) : Restock(src, inventory);
                    if (movedChest > 0)
                    {
                        moved += movedChest;
                        touched++;
                        // Save only once after the complete batch. The vanilla
                        // inventoryChanged callback also synchronizes ownership.
                        chest.Save();
                    }
                    if (ci % 4 == 3) yield return null; // keep the game responsive
                }
                player.Message(MessageHud.MessageType.TopLeft,
                    (quick ? "Quickstack: " : "Restock: ") + moved +
                    " szt. / " + touched + " skrzyń" + (skipped > 0 ? " (pominięto: " + skipped + ")" : ""));
                if (moved > 0) PlayFeedback(player);
            }
            finally
            {
                _busy = false;
                Nearby.Clear();
                Existing.Clear();
                PlayerItems.Clear();
            }
        }

        private static int QuickStack(Inventory playerInv, Inventory chestInv)
        {
            int amount = 0;
            foreach (ItemDrop.ItemData item in PlayerItems)
            {
                if (item == null || !playerInv.ContainsItem(item)) continue;
                if (!Existing.Contains(item.m_shared.m_name)) continue;
                if (QolFavorites.IsFavorite(item, playerInv)) continue;
                int before = item.m_stack;
                chestInv.MoveItemToThis(playerInv, item);
                int taken = playerInv.ContainsItem(item) ? Math.Max(0, before - item.m_stack) : before;
                amount += taken;
            }
            return amount;
        }

        private static int Restock(Inventory playerInv, Inventory chestInv)
        {
            int moved = 0;
            foreach (ItemDrop.ItemData playerItem in PlayerItems)
            {
                if (playerItem == null || !playerInv.ContainsItem(playerItem)) continue;
                int missing = playerItem.m_shared.m_maxStackSize - playerItem.m_stack;
                if (missing <= 0) continue;
                if (!Existing.Contains(playerItem.m_shared.m_name)) continue;
                // Never restock custom-data items into plain stacks.
                if (playerItem.m_customData != null && playerItem.m_customData.Count > 0) continue;
                List<ItemDrop.ItemData> chestItems = chestInv.GetAllItems();
                for (int i = chestItems.Count - 1; i >= 0 && missing > 0; i--)
                {
                    ItemDrop.ItemData item = chestItems[i];
                    if (item == null || item.m_shared == null || item.m_shared.m_name != playerItem.m_shared.m_name ||
                        item.m_quality != playerItem.m_quality ||
                        item.m_customData != null && item.m_customData.Count > 0) continue;
                    int count = Math.Min(missing, item.m_stack);
                    if (count <= 0) continue;
                    if (playerInv.MoveItemToThis(chestInv, item, count,
                        playerItem.m_gridPos.x, playerItem.m_gridPos.y))
                    {
                        moved += count;
                        missing -= count;
                    }
                }
            }
            return moved;
        }

        private static void GetContainers(Player player)
        {
            Nearby.Clear();
            Vector3 origin = player.transform.position;
            Known.RemoveWhere(c => c == null);
            foreach (Container chest in Known)
            {
                if (chest == null || chest.m_nview == null || chest.m_inventory == null ||
                    !chest.m_nview.IsValid() || !chest.gameObject.activeInHierarchy) continue;
                if ((chest.transform.position - origin).sqrMagnitude > RangeSq) continue;
                Nearby.Add(chest);
            }
            Nearby.Sort((a,b) => (a.transform.position-origin).sqrMagnitude
                .CompareTo((b.transform.position-origin).sqrMagnitude));
        }

        private static bool CanAccess(Container chest, Player player)
        {
            if (chest == null || player == null || chest.m_nview == null || !chest.m_nview.IsValid() ||
                chest.m_inventory == null || chest.m_nview.GetZDO() == null) return false;
            if (!chest.CheckAccess(player.GetPlayerID())) return false;
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, true, false))
                return false;
            if (chest.IsInUse() || chest.m_nview.GetZDO().GetInt("InUse", 0) == 1) return false;
            if (chest.m_piece != null && !chest.m_piece.IsPlacedByPlayer()) return false;
            // On shared worlds, skip ship containers: their ownership and
            // interpolation can change while a remote batch is underway.
            if (chest.GetComponentInParent<Ship>() != null) return false;
            return true;
        }

        private static void PlayFeedback(Player player)
        {
            // Feedback is throttled to one game message per hotkey press;
            // never play one effect per chest (can be hundreds in big bases).
            // Vanilla item transfer handles any configured UI sound effects.
        }

        internal static void Shutdown()
        {
            _host = null; _busy = false;
            Known.Clear(); Nearby.Clear(); Existing.Clear(); PlayerItems.Clear();
        }
    }
}
