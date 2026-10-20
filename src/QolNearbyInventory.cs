using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private static readonly FieldInfo ContainerOpenEffects = AccessTools.Field(typeof(Container), "m_openEffects");
        private static bool _busy;
        private static float _nextKey;

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
            if (QolGameAccess.DragItem(InventoryGui.instance) != null) return;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.GetComponentInParent<TMP_InputField>() != null) return;
            bool quick = Input.GetKeyDown(KeyCode.Q);
            bool restock = !quick && Input.GetKeyDown(KeyCode.R);
            if (!quick && !restock) return;
            _nextKey = Time.unscaledTime + .25f;
            _host.StartCoroutine(Execute(player, quick));
        }

        internal static void StartArea(bool quick)
        {
            if (_host == null || _busy || Player.m_localPlayer == null) return;
            _host.StartCoroutine(Execute(Player.m_localPlayer, quick));
        }

        internal static void StartStoreCurrent()
        {
            if (_host == null || _busy || Player.m_localPlayer == null) return;
            _host.StartCoroutine(StoreCurrent(Player.m_localPlayer));
        }

        private static IEnumerator StoreCurrent(Player player)
        {
            _busy = true;
            try
            {
                InventoryGui gui = InventoryGui.instance;
                Container chest = gui == null ? null : QolGameAccess.CurrentContainer(gui);
                if (chest == null || !CanAccess(chest, player))
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Otwórz dostępną skrzynię.");
                    yield break;
                }
                ZNetView view = QolGameAccess.View(chest);
                if (!view.IsOwner())
                {
                    view.ClaimOwnership();
                    float deadline = Time.realtimeSinceStartup + .9f;
                    while (view != null && !view.IsOwner() && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                if (view == null || !view.IsOwner() || !CanAccess(chest, player))
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Brak uprawnień do zapisu w skrzyni.");
                    yield break;
                }
                Inventory playerInv = player.GetInventory();
                Inventory chestInv = QolGameAccess.Inventory(chest);
                if (playerInv == null || chestInv == null) yield break;
                var snapshot = new List<ItemDrop.ItemData>(playerInv.GetAllItems());
                int moved = 0;
                foreach (ItemDrop.ItemData item in snapshot)
                {
                    if (item == null || !playerInv.ContainsItem(item) ||
                        item.m_gridPos.y == 0 || item.m_equipped ||
                        QolFavorites.IsFavorite(item, playerInv)) continue;
                    int before = item.m_stack;
                    chestInv.MoveItemToThis(playerInv, item);
                    moved += playerInv.ContainsItem(item) ? Math.Max(0, before - item.m_stack) : before;
                }
                if (moved > 0)
                {
                    QolGameAccess.Save(chest);
                    PlayFeedback(chest, player);
                }
                player.Message(MessageHud.MessageType.TopLeft, "Store All: przeniesiono " + moved + " szt.");
            }
            finally
            {
                _busy = false;
            }
        }

        private static IEnumerator Execute(Player player, bool quick)
        {
            if (_busy) yield break;
            _busy = true;
            int moved = 0, touched = 0, skipped = 0;
            Container firstModified = null;
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
                    if (item == null || item.m_shared == null) continue;
                    // Quickstack never removes from hotbar. Restock only
                    // ADDS to existing hotbar stacks and does not remove them.
                    if (quick && item.m_gridPos.y == 0) continue;
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
                    ZNetView view = QolGameAccess.View(chest);
                    if (!view.IsOwner())
                    {
                        view.ClaimOwnership();
                        float deadline = Time.realtimeSinceStartup + .9f;
                        while (view != null && !view.IsOwner() && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        if (view == null || !view.IsOwner()) { skipped++; continue; }
                    }
                    if (!CanAccess(chest, player)) { skipped++; continue; }
                    Inventory inventory = QolGameAccess.Inventory(chest);
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
                        if (firstModified == null) firstModified = chest;
                        // Save only once after the complete batch. The vanilla
                        // inventoryChanged callback also synchronizes ownership.
                        QolGameAccess.Save(chest);
                    }
                    if (ci % 4 == 3) yield return null; // keep the game responsive
                }
                player.Message(MessageHud.MessageType.TopLeft,
                    (quick ? "Quickstack: " : "Restock: ") + moved +
                    " szt. / " + touched + " skrzyń" + (skipped > 0 ? " (pominięto: " + skipped + ")" : ""));
                if (moved > 0) PlayFeedback(firstModified, player);
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
                if (chest == null || QolGameAccess.View(chest) == null || QolGameAccess.Inventory(chest) == null ||
                    !QolGameAccess.View(chest).IsValid() || !chest.gameObject.activeInHierarchy) continue;
                if ((chest.transform.position - origin).sqrMagnitude > RangeSq) continue;
                Nearby.Add(chest);
            }
            Nearby.Sort((a,b) => (a.transform.position-origin).sqrMagnitude
                .CompareTo((b.transform.position-origin).sqrMagnitude));
        }

        private static bool CanAccess(Container chest, Player player)
        {
            if (chest == null || player == null || QolGameAccess.View(chest) == null || !QolGameAccess.View(chest).IsValid() ||
                QolGameAccess.Inventory(chest) == null || QolGameAccess.View(chest).GetZDO() == null) return false;
            if (!QolGameAccess.CheckAccess(chest, player.GetPlayerID())) return false;
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, true, false))
                return false;
            if (chest.IsInUse() || QolGameAccess.View(chest).GetZDO().GetInt("InUse", 0) == 1) return false;
            if (QolGameAccess.Piece(chest) != null && !QolGameAccess.Piece(chest).IsPlacedByPlayer()) return false;
            // On shared worlds, skip ship containers: their ownership and
            // interpolation can change while a remote batch is underway.
            if (chest.GetComponentInParent<Ship>() != null) return false;
            return true;
        }

        private static void PlayFeedback(Container source, Player player)
        {
            // Reuse the game's native container opening SFX/VFX, as in the
            // referenced mod, but only ONCE after a successful hotkey batch.
            // Never open and close hundreds of remote chests just for effects.
            if (source == null || player == null || ContainerOpenEffects == null) return;
            try
            {
                object effects = ContainerOpenEffects.GetValue(source);
                if (effects == null) return;
                MethodInfo create = null;
                foreach (MethodInfo method in effects.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.Name != "Create") continue;
                    ParameterInfo[] args = method.GetParameters();
                    if (args.Length >= 2 && args[0].ParameterType == typeof(Vector3) &&
                        args[1].ParameterType == typeof(Quaternion)) { create = method; break; }
                }
                if (create == null) return;
                ParameterInfo[] parameters = create.GetParameters();
                object[] values = new object[parameters.Length];
                values[0] = player.transform.position;
                values[1] = Quaternion.identity;
                for (int i=2; i<values.Length; i++)
                {
                    Type type = parameters[i].ParameterType;
                    values[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue :
                        type.IsValueType ? Activator.CreateInstance(type) : null;
                }
                create.Invoke(effects, values);
            }
            catch (Exception e)
            {
                _host?.Log.LogDebug("QOL transfer effect unavailable: " + e.Message);
            }
        }

        internal static void Shutdown()
        {
            _host = null; _busy = false;
            Known.Clear(); Nearby.Clear(); Existing.Clear(); PlayerItems.Clear();
        }
    }
}
