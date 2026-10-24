using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    internal static class QolTrashReclaim
    {
        private static Plugin _host;
        private static readonly Action<ItemDrop> SaveDroppedItem =
            AccessTools.MethodDelegate<Action<ItemDrop>>(AccessTools.Method(typeof(ItemDrop), "Save"));
        private static RectTransform _buttonRect;
        private static GameObject _buttonObject;
        private static GameObject _confirm;
        private static ItemDrop.ItemData _pendingItem;
        private static Inventory _pendingInventory;
        private static int _pendingAmount;
        private static readonly List<Yield> Yields = new List<Yield>(12);
        private static readonly Dictionary<string,int> FreeReserved = new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly Dictionary<string,int> YieldIndices = new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly Action<GameObject, ItemDrop.ItemData, int> AppendYield = AddYield;

        private struct Yield
        {
            internal GameObject Prefab;
            internal ItemDrop.ItemData Data;
            internal string Name;
            internal int Count;
        }

        private enum ReclaimResult { Unsupported, Full, Reclaimed, Error }

        internal static void Install(Plugin host)
        {
            _host = host;
            QolRecipeCache.Install(host);
            host.Patch("TrashReclaim.GUI.Show", AccessTools.Method(typeof(InventoryGui), "Show"),
                postfix: AccessTools.Method(typeof(QolTrashReclaim), nameof(Show)));
            host.Patch("TrashReclaim.GUI.Hide", AccessTools.Method(typeof(InventoryGui), "Hide"),
                postfix: AccessTools.Method(typeof(QolTrashReclaim), nameof(Hide)));
            host.Patch("TrashReclaim.GUI.Drag", AccessTools.Method(typeof(InventoryGui), "UpdateItemDrag"),
                prefix: AccessTools.Method(typeof(QolTrashReclaim), nameof(Dragging)));
        }

        private static void Show(InventoryGui __instance)
        {
            if (__instance != null) EnsureUI(__instance);
        }

        private static void Hide()
        {
            ClearDialog();
        }

        private static void EnsureUI(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null) return;
            RectTransform overlay = QolStoreSort.OverlayParent(gui);
            if (overlay == null) return;
            if (_buttonObject != null) { _buttonObject.SetActive(true); Align(gui); return; }
            _buttonObject = new GameObject("SirrohTrashReclaim",typeof(RectTransform),
                typeof(Image),typeof(Button));
            _buttonObject.transform.SetParent(overlay,false);
            _buttonRect = _buttonObject.GetComponent<RectTransform>();
            _buttonRect.sizeDelta = new Vector2(52f,52f);
            _buttonRect.anchorMin = _buttonRect.anchorMax = new Vector2(.5f,.5f);
            Image hit = _buttonObject.GetComponent<Image>();
            // The hitbox stays interactive but must not tint the wooden tab.
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = _buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnButtonClicked);
            GameObject icon = new GameObject("RecycleIcon",typeof(RectTransform),typeof(Image));
            icon.transform.SetParent(_buttonRect,false);
            RectTransform iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin=Vector2.zero;
            iconRt.anchorMax=Vector2.one;
            // Exact match for Sort/Quickstack/Restock: 34px icon inside
            // the same 52px clickable region with symmetric 9px insets.
            iconRt.offsetMin=new Vector2(9f,9f);
            iconRt.offsetMax=new Vector2(-9f,-9f);
            iconRt.anchoredPosition = Vector2.zero;
            Image image = icon.GetComponent<Image>();
            image.sprite = QolFavorites.RecycleIcon;
            image.preserveAspect = true;
            QolNativeUiLighting.Apply(image);
            image.raycastTarget = false;
            QolIconHover hover = _buttonObject.AddComponent<QolIconHover>();
            hover.Initialize(image, false);
            Align(gui);
        }

        internal static void Align(InventoryGui gui)
        {
            if (gui == null || _buttonRect == null) return;
            QolStoreSort.PositionRecycle(_buttonRect);
        }

        private static bool Dragging(InventoryGui __instance)
        {
            if (_buttonRect == null || _confirm != null || __instance == null ||
                QolGameAccess.DragItem(__instance) == null || QolGameAccess.DragSource(__instance) == null)
                return true;
            if (!Input.GetMouseButtonUp(0) || !RectTransformUtility.RectangleContainsScreenPoint(
                _buttonRect, Input.mousePosition, null)) return true;
            OnButtonClicked();
            return false;
        }

        private static void OnButtonClicked()
        {
            if (_confirm != null || InventoryGui.instance == null || Player.m_localPlayer == null) return;
            InventoryGui gui = InventoryGui.instance;
            ItemDrop.ItemData item = QolGameAccess.DragItem(gui);
            Inventory from = QolGameAccess.DragSource(gui);
            int count = Math.Min(QolGameAccess.DragAmount(gui), item?.m_stack ?? 0);
            if (!CanOperate(item, from, count)) return;
            // Detach UI drag only after taking stable references.
            QolGameAccess.ClearDrag(gui);
            ReclaimResult result = TryReclaim(item, from, count);
            if (result == ReclaimResult.Unsupported)
            {
                _host?.Log.LogDebug("TrashReclaim: no usable recipe for prefab '" +
                    (item.m_dropPrefab != null ? item.m_dropPrefab.name : "(null)") +
                    "', shared '" + item.m_shared.m_name + "', quality " + item.m_quality +
                    ", custom fields " + (item.m_customData?.Count ?? 0) +
                    ". Only the explicit delete confirmation is offered.");
                ShowConfirmation(item, from, count);
            }
            else if (result == ReclaimResult.Full)
                Message("Brak miejsca w ekwipunku na odzyskane materiały.");
            else if (result == ReclaimResult.Error)
                Message("Odzyskiwanie przerwane. Przedmiot nie został celowo zniszczony.");
        }

        private static bool CanOperate(ItemDrop.ItemData item, Inventory origin, int count)
        {
            Player player = Player.m_localPlayer;
            if (player == null || item == null || origin == null || count <= 0 ||
                !origin.ContainsItem(item) || item.m_shared == null) return false;
            Inventory inventory = player.GetInventory();
            bool fromPlayer = origin == inventory;
            bool fromCurrentChest = InventoryGui.instance != null &&
                QolGameAccess.CurrentContainer(InventoryGui.instance) != null &&
                origin == QolGameAccess.Inventory(QolGameAccess.CurrentContainer(InventoryGui.instance));
            if (!fromPlayer && !fromCurrentChest) return false;
            if (item.m_equipped || fromPlayer && item.m_gridPos.y == 0)
            {
                Message("Przedmioty wyposażone i z paska szybkiego dostępu są chronione.");
                return false;
            }
            if (QolFavorites.IsNameFavorite(item) ||
                fromPlayer && QolFavorites.IsFavorite(item, inventory))
            {
                Message("Ulubiony przedmiot jest chroniony.");
                return false;
            }
            return true;
        }

        private static ReclaimResult TryReclaim(ItemDrop.ItemData item, Inventory origin, int count)
        {
            Player player = Player.m_localPlayer;
            Inventory inventory = player.GetInventory();
            // Modded armor (including leggings) often has m_customData even
            // when made by an ordinary recipe. It MUST NOT be mistaken for
            // an item without a crafting recipe. Base ingredients can still
            // be recovered. Mod-specific enchantment costs are separate.
            Recipe recipe = QolRecipeCache.Find(item);
            if (recipe == null || recipe.m_resources == null || recipe.m_resources.Length == 0 ||
                recipe.m_amount < 1 || count % recipe.m_amount != 0)
                return ReclaimResult.Unsupported;

            Yields.Clear();
            FreeReserved.Clear();
            YieldIndices.Clear();
            int batches = count / recipe.m_amount;
            var received = new List<Yield>();
            try
            {
                foreach (Piece.Requirement res in recipe.m_resources)
                {
                    if (res == null || res.m_resItem == null || res.m_resItem.m_itemData == null)
                        return ReclaimResult.Unsupported;
                    ItemDrop.ItemData resource = res.m_resItem.m_itemData;
                    GameObject prefab = resource.m_dropPrefab != null ? resource.m_dropPrefab :
                        ObjectDB.instance.GetItemPrefab(resource.m_shared);
                    if (prefab == null) return ReclaimResult.Unsupported;
                    int perBatch = 0;
                    int highest = Math.Max(1, item.m_shared.m_maxQuality);
                    int from = res.m_upgraderResource ? highest + 1 : 1;
                    int to = res.m_upgraderResource ? item.m_quality :
                        Math.Min(item.m_quality, highest);
                    for (int q = from; q <= to; q++)
                        perBatch = checked(perBatch + Math.Max(0, res.GetAmount(q)));
                    int qty = checked(perBatch * batches);
                    if (qty > 0) AddYield(prefab, resource, qty);
                }
                // Gather additional resources only once, on explicit Reclaim.
                // A missing required extension material aborts the operation.
                if (!QolReclaimExtensions.Collect(item, count, AppendYield))
                    return ReclaimResult.Error;
                if (Yields.Count == 0) return ReclaimResult.Unsupported;

                // Count actual slots, independent from carry weight. Valheim
                // permits overweight inventory; treating weight as "no slots"
                // incorrectly rejected expensive modded Overhaul gear.
                // If yields truly exceed inventory slots, issue physical
                // ingredient stacks only on this rare reclaim overflow.
                int slots = inventory.GetEmptySlots();
                bool overflow = false;
                foreach (var entry in Yields)
                {
                    if (entry.Data.m_shared.m_maxStackSize <= 0) return ReclaimResult.Unsupported;
                    if (!FreeReserved.TryGetValue(entry.Name, out int reserved)) reserved = 0;
                    int freeInStacks = Math.Max(0,
                        inventory.FindFreeStackSpace(entry.Name, entry.Data.m_worldLevel) - reserved);
                    int filled = Math.Min(entry.Count, freeInStacks);
                    int remainder = entry.Count - filled;
                    slots -= (int)(((long)remainder + entry.Data.m_shared.m_maxStackSize - 1) /
                        entry.Data.m_shared.m_maxStackSize);
                    if (slots < 0) { overflow = true; break; }
                    FreeReserved[entry.Name] = reserved + filled;
                }
                // Preserve all materials when the returned recipe cannot fit
                // (e.g. modded high-cost gear). No scene-spawn work on the
                // normal inventory-fit path.
                if (overflow) return DropOverflowYields(item, origin, count, player);

                // Place resources first; source is removed only once all
                // resources were actually inserted. Roll back counts on
                // unexpected insertion failure.
                foreach (var entry in Yields)
                {
                    ItemDrop.ItemData added = inventory.AddItem(entry.Prefab.name, entry.Count,
                        entry.Data.m_quality, entry.Data.m_variant, 0L, "", false, false);
                    if (added == null)
                    {
                        UndoReceived(inventory, received);
                        return ReclaimResult.Error;
                    }
                    received.Add(entry);
                }
                bool removed = count == item.m_stack
                    ? origin.RemoveItem(item) : origin.RemoveItem(item, count);
                if (!removed)
                {
                    UndoReceived(inventory, received);
                    return ReclaimResult.Error;
                }
                Message("Odzyskano 100% materiałów z receptury przedmiotu.");
                return ReclaimResult.Reclaimed;
            }
            catch (Exception e)
            {
                if (received.Count > 0) UndoReceived(inventory, received);
                _host?.Log.LogWarning("TrashReclaim: transaction error: " + e);
                return ReclaimResult.Error;
            }
        }

        private static ReclaimResult DropOverflowYields(ItemDrop.ItemData source, Inventory origin,
            int count, Player player)
        {
            // Bounded emergency fallback, never part of the normal fast path.
            // Stage all drops, verify the source removal, then finish.
            // If the source cannot be removed, destroy staged drops.
            const int MaxDrops = 64;
            int totalDrops = 0;
            foreach (Yield entry in Yields)
            {
                int max = entry.Data.m_shared.m_maxStackSize;
                if (max < 1) return ReclaimResult.Unsupported;
                long need = ((long)entry.Count + max - 1) / max;
                if (need > MaxDrops - totalDrops)
                {
                    Message("Reclaim: ponad 64 stosy materiałów. Zwolnij miejsce w ekwipunku.");
                    return ReclaimResult.Full;
                }
                totalDrops += (int)need;
                if (totalDrops > MaxDrops)
                {
                    Message("Reclaim: ponad 64 stosy materiałów. Zwolnij miejsce w ekwipunku.");
                    return ReclaimResult.Full;
                }
            }

            List<GameObject> spawned = new List<GameObject>(totalDrops);
            bool committed = false;
            try
            {
                int index = 0;
                foreach (Yield entry in Yields)
                {
                    int remaining = entry.Count;
                    int max = entry.Data.m_shared.m_maxStackSize;
                    while (remaining > 0)
                    {
                        int chunk = Math.Min(max, remaining);
                        Vector3 offset = new Vector3((index % 5 - 2) * .45f, .8f,
                            ((index / 5) % 5 - 2) * .45f);
                        GameObject dropObject = UnityEngine.Object.Instantiate(entry.Prefab,
                            player.transform.position + offset, Quaternion.identity);
                        if (dropObject == null) return ReclaimResult.Error;
                        spawned.Add(dropObject);
                        ItemDrop drop = dropObject.GetComponent<ItemDrop>();
                        if (drop == null || drop.m_itemData == null) return ReclaimResult.Error;
                        drop.m_itemData.m_stack = chunk;
                        drop.m_itemData.m_quality = entry.Data.m_quality;
                        drop.m_itemData.m_variant = entry.Data.m_variant;
                        SaveDroppedItem(drop);
                        remaining -= chunk;
                        index++;
                    }
                }
                bool removed = count == source.m_stack
                    ? origin.RemoveItem(source) : origin.RemoveItem(source, count);
                if (!removed) return ReclaimResult.Error;
                committed = true;
                Message("Odzyskano materiały na ziemię (" + totalDrops +
                    " stosów; brak wystarczającej liczby slotów).");
                return ReclaimResult.Reclaimed;
            }
            catch (Exception ex)
            {
                _host?.Log.LogWarning("Reclaim overflow staging failed: " + ex);
                return ReclaimResult.Error;
            }
            finally
            {
                if (!committed)
                    for (int i = 0; i < spawned.Count; i++)
                        if (spawned[i] != null)
                        {
                            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(spawned[i]);
                            else UnityEngine.Object.Destroy(spawned[i]);
                        }
            }
        }

        private static void UndoReceived(Inventory inventory, List<Yield> received)
        {
            foreach (var yield in received)
                inventory.RemoveItem(yield.Name, yield.Count, -1, true);
            _host?.Log.LogWarning("TrashReclaim: rolled back resource returns after insertion failure.");
        }

        private static void AddYield(GameObject prefab, ItemDrop.ItemData data, int count)
        {
            // Merge duplicate resources from crafting, enchantment and sockets.
            // One reservation and one insertion per unique item prefab.
            string key = prefab.name;
            if (YieldIndices.TryGetValue(key, out int index))
            {
                Yield current = Yields[index];
                current.Count = checked(current.Count + count);
                Yields[index] = current;
            }
            else
            {
                YieldIndices[key] = Yields.Count;
                Yields.Add(new Yield { Prefab = prefab, Data = data, Name = data.m_shared.m_name, Count = count });
            }
        }

        private static void ShowConfirmation(ItemDrop.ItemData item, Inventory source, int amount)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null) return;
            _pendingItem = item; _pendingInventory = source; _pendingAmount = amount;
            Transform canvas = gui.transform;
            _confirm = new GameObject("SirrohDestroyConfirm", typeof(RectTransform), typeof(Image));
            _confirm.transform.SetParent(canvas, false);
            RectTransform root = _confirm.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(510f, 200f);
            root.anchoredPosition = Vector2.zero;
            _confirm.transform.SetAsLastSibling();
            Image bg = _confirm.GetComponent<Image>();
            if (QolFavorites.RecyclePanel != null) bg.sprite = QolFavorites.RecyclePanel;
            bg.color = new Color(.075f, .07f, .065f, .98f);
            AddText("ConfirmationText", root, "Czy na pewno chcesz zniszczyć ten przedmiot?",
                new Vector2(0f, 44f), new Vector2(490f, 88f), 22f);
            AddButton(root, "Tak", new Vector2(-110f, -58f), ConfirmDelete);
            AddButton(root, "Nie", new Vector2(110f, -58f), ClearDialog);
        }

        private static void AddText(string name, RectTransform parent, string text,
            Vector2 at, Vector2 size, float fontSize)
        {
            // Delay TMP OnEnable until a valid in-game font has been assigned.
            // Otherwise TMP attempts to load missing LiberationSans fallback.
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            InventoryGui gui = InventoryGui.instance;
            TMP_FontAsset font = gui?.m_playerName != null ? gui.m_playerName.font : null;
            if (font == null && gui?.m_recipeName != null) font = gui.m_recipeName.font;
            if (font == null && gui?.m_craftingStationName != null)
                font = gui.m_craftingStationName.font;
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            go.SetActive(true);
        }

        private static void AddButton(RectTransform parent, string label, Vector2 at, Action action)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(170f, 58f);
            go.GetComponent<Image>().color = new Color(.35f, .30f, .24f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => action());
            AddText("Label", rt, label, Vector2.zero, rt.sizeDelta, 24f);
        }

        private static void ConfirmDelete()
        {
            ItemDrop.ItemData item = _pendingItem;
            Inventory origin = _pendingInventory;
            int count = _pendingAmount;
            ClearDialog();
            if (!CanOperate(item, origin, count)) return;
            bool removed = count == item.m_stack ? origin.RemoveItem(item) : origin.RemoveItem(item, count);
            Message(removed ? "Przedmiot został zniszczony." : "Nie udało się zniszczyć przedmiotu.");
        }

        private static void ClearDialog()
        {
            if (_confirm != null) UnityEngine.Object.Destroy(_confirm);
            _confirm = null; _pendingItem = null; _pendingInventory = null; _pendingAmount = 0;
        }

        private static void Message(string message)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
        }

        internal static void Shutdown()
        {
            QolRecipeCache.Shutdown();
            QolReclaimExtensions.Shutdown();
            ClearDialog();
            if (_buttonObject != null) UnityEngine.Object.Destroy(_buttonObject);
            _buttonObject = null; _buttonRect = null;
            Yields.Clear(); FreeReserved.Clear(); YieldIndices.Clear();
            _host = null;
        }
    }
}
