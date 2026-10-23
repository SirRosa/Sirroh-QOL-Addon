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
        private static RectTransform _buttonRect;
        private static GameObject _buttonObject;
        private static GameObject _confirm;
        private static ItemDrop.ItemData _pendingItem;
        private static Inventory _pendingInventory;
        private static int _pendingAmount;
        private static readonly List<Yield> Yields = new List<Yield>(12);
        private static readonly Dictionary<string,int> FreeReserved = new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly Dictionary<string,int> FinalAmount = new Dictionary<string,int>(StringComparer.Ordinal);

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
            _buttonRect.sizeDelta = new Vector2(64f,64f);
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
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(.5f,.5f);
            iconRt.anchorMin=Vector2.zero;
            iconRt.anchorMax=Vector2.one;
            // 38px Recycle image inside a 54px target.
            iconRt.offsetMin=new Vector2(8f,8f);
            iconRt.offsetMax=new Vector2(-8f,-8f);
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
                _host?.Log.LogWarning("TrashReclaim: no usable recipe for prefab '" +
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
            Recipe recipe = FindRecipe(item);
            if (recipe == null || recipe.m_resources == null || recipe.m_resources.Length == 0 ||
                recipe.m_amount < 1 || count % recipe.m_amount != 0)
                return ReclaimResult.Unsupported;

            Yields.Clear();
            FreeReserved.Clear();
            FinalAmount.Clear();
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
                    int quality = Math.Max(1, Math.Min(item.m_quality, item.m_shared.m_maxQuality));
                    for (int q = 1; q <= quality; q++) perBatch += Math.Max(0, res.GetAmount(q));
                    int qty = checked(perBatch * batches);
                    if (qty <= 0) continue;
                    string name = resource.m_shared.m_name;
                    if (FinalAmount.TryGetValue(name, out int prev)) FinalAmount[name] = checked(prev + qty);
                    else FinalAmount[name] = qty;
                    Yields.Add(new Yield { Prefab=prefab, Data=resource, Name=name, Count=qty });
                }
                if (Yields.Count == 0) return ReclaimResult.Unsupported;

                // Full capacity simulation across all materials, not
                // separate CanAddItem checks that may over-book empty slots.
                int slots = inventory.GetEmptySlots();
                foreach (var entry in Yields)
                {
                    if (entry.Data.m_shared.m_maxStackSize <= 0) return ReclaimResult.Unsupported;
                    if (!FreeReserved.TryGetValue(entry.Name, out int reserved)) reserved = 0;
                    int freeInStacks = Math.Max(0,
                        inventory.FindFreeStackSpace(entry.Name, entry.Data.m_worldLevel) - reserved);
                    int filled = Math.Min(entry.Count, freeInStacks);
                    int remainder = entry.Count - filled;
                    slots -= (remainder + entry.Data.m_shared.m_maxStackSize - 1) /
                        entry.Data.m_shared.m_maxStackSize;
                    if (slots < 0) return ReclaimResult.Full;
                    FreeReserved[entry.Name] = reserved + filled;
                }
                // Independent safety check for weight. Material yield may be
                // heavy despite enough slots.
                float deltaWeight = 0f;
                foreach (var entry in Yields) deltaWeight += entry.Data.GetWeight(-1) * entry.Count;
                float after = inventory.GetTotalWeight() + deltaWeight -
                    (origin == inventory ? item.GetWeight(-1) * count : 0f);
                if (after > player.GetMaxCarryWeight()) return ReclaimResult.Full;

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

        private static void UndoReceived(Inventory inventory, List<Yield> received)
        {
            foreach (var yield in received)
                inventory.RemoveItem(yield.Name, yield.Count, -1, true);
            _host?.Log.LogWarning("TrashReclaim: rolled back resource returns after insertion failure.");
        }

        // Compare prefab identity FIRST. Display names are often duplicated
        // or replaced by armor/enchantment/localization mods.
        private static string PrefabId(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string id = name.Trim();
            const string clone = "(Clone)";
            if (id.EndsWith(clone, StringComparison.Ordinal))
                id = id.Substring(0,id.Length-clone.Length).TrimEnd();
            return id;
        }

        private static bool HasResources(Recipe recipe)
        {
            return recipe != null && recipe.m_item != null &&
                recipe.m_item.m_itemData != null && recipe.m_amount > 0 &&
                recipe.m_resources != null && recipe.m_resources.Length > 0;
        }

        private static Recipe FindRecipe(ItemDrop.ItemData item)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_recipes == null || item == null ||
                item.m_shared == null) return null;
            string targetPrefab = item.m_dropPrefab != null
                ? PrefabId(item.m_dropPrefab.name) : null;
            Recipe prefabMatch = null, sharedNameMatch = null;
            foreach (Recipe recipe in db.m_recipes)
            {
                if (!HasResources(recipe)) continue;
                string candidate = PrefabId(recipe.m_item.gameObject.name);
                bool samePrefab = targetPrefab != null &&
                    string.Equals(candidate,targetPrefab,StringComparison.Ordinal);
                if (samePrefab)
                {
                    if (prefabMatch == null || recipe.m_amount < prefabMatch.m_amount)
                        prefabMatch = recipe;
                    continue;
                }
                // Last resort for items whose dropPrefab is unavailable.
                if (recipe.m_item.m_itemData.m_shared != null &&
                    recipe.m_item.m_itemData.m_shared.m_name == item.m_shared.m_name &&
                    (sharedNameMatch == null || recipe.m_amount < sharedNameMatch.m_amount))
                    sharedNameMatch = recipe;
            }
            if (prefabMatch != null) return prefabMatch;

            // ObjectDB uses its own registered recipe lookup for some modded
            // items that aren't discoverable by prefab name.
            try
            {
                Recipe fromDb = db.GetRecipe(item);
                if (HasResources(fromDb)) return fromDb;
            }
            catch (Exception ex)
            {
                _host?.Log.LogDebug("TrashReclaim: ObjectDB.GetRecipe fallback: "+ex.Message);
            }
            return sharedNameMatch;
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
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (InventoryGui.instance.m_playerName != null) t.font = InventoryGui.instance.m_playerName.font;
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
            ClearDialog();
            if (_buttonObject != null) UnityEngine.Object.Destroy(_buttonObject);
            _buttonObject = null; _buttonRect = null;
            Yields.Clear(); FreeReserved.Clear(); FinalAmount.Clear();
            _host = null;
        }
    }
}
