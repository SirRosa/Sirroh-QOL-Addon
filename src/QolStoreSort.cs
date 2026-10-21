using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    // Positions modeled on QuickStackStore mini buttons: anchored to the
    // player's Weight control, 52px below it. No large labeled rectangles.
    internal static class QolStoreSort
    {
        private static GameObject _panel;
        private static Button _favoriteButton;
        private static Button _storeButton;
        private static Image _favoriteImage;
        private static TextMeshProUGUI _hint;
        private const float IconSpacing = 28f;

        internal static void Install(Plugin host)
        {
            host.Patch("QOL.StoreSort.Show", AccessTools.Method(typeof(InventoryGui), "Show"),
                postfix: AccessTools.Method(typeof(QolStoreSort), nameof(OnShow)));
        }

        private static void OnShow(InventoryGui __instance)
        {
            InventoryGui gui = __instance;
            if (gui == null || gui.m_player == null) return;
            if (_panel == null)
            {
                _panel = new GameObject("SirrohQOLMiniActions", typeof(RectTransform));
                _panel.transform.SetParent(gui.m_player, false);
                RectTransform panel = _panel.GetComponent<RectTransform>();
                panel.sizeDelta = new Vector2(145f, 30f);
                MakeButton(panel, "Sort", QolFavorites.SortIcon, 0, SortPlayer);
                MakeButton(panel, "Quickstack (Q)", QolFavorites.QuickIcon, 1,
                    () => QolNearbyInventory.StartArea(true));
                MakeButton(panel, "Restock (R)", QolFavorites.RestockIcon, 2,
                    () => QolNearbyInventory.StartArea(false));
                _favoriteButton = MakeButton(panel, "Ulubione (Alt + LPM / PPM)",
                    QolFavorites.FavoriteIcon, 3, ToggleFavorite);
                _favoriteImage = _favoriteButton.transform.Find("Icon").GetComponent<Image>();
                _storeButton = MakeButton(panel, "Store All", QolFavorites.QuickIcon, 4,
                    QolNearbyInventory.StartStoreCurrent);
                GameObject marker = new GameObject("A",typeof(RectTransform),typeof(TextMeshProUGUI));
                marker.transform.SetParent(_storeButton.transform,false);
                RectTransform markRect = marker.GetComponent<RectTransform>();
                markRect.anchorMin = markRect.anchorMax = new Vector2(1f,0f);
                markRect.sizeDelta = new Vector2(12f,12f);
                markRect.anchoredPosition = new Vector2(-2f,2f);
                TextMeshProUGUI letter = marker.GetComponent<TextMeshProUGUI>();
                letter.text = "A";
                letter.fontSize = 11f;
                letter.alignment = TextAlignmentOptions.Center;
                letter.color = new Color(1f,.87f,.42f);
                letter.raycastTarget = false;
                if (gui.m_playerName != null) letter.font = gui.m_playerName.font;
                GameObject hintObj = new GameObject("QOLTooltip",typeof(RectTransform),typeof(TextMeshProUGUI));
                hintObj.transform.SetParent(panel,false);
                RectTransform hintRect = hintObj.GetComponent<RectTransform>();
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(.0f,1f);
                hintRect.pivot = new Vector2(0f,0f);
                hintRect.sizeDelta = new Vector2(270f,22f);
                hintRect.anchoredPosition = new Vector2(0f,7f);
                _hint = hintObj.GetComponent<TextMeshProUGUI>();
                _hint.fontSize = 14f;
                _hint.alignment = TextAlignmentOptions.Left;
                _hint.raycastTarget = false;
                if (gui.m_playerName != null) _hint.font = gui.m_playerName.font;
                hintObj.SetActive(false);
            }
            UpdateStates(gui);
            // Vanilla and UI mods may reposition Weight a frame after Show.
            Plugin.Instance.StartCoroutine(NextFramePosition(gui));
        }

        private static IEnumerator NextFramePosition(InventoryGui gui)
        {
            yield return null;
            if (gui != null && _panel != null)
            {
                Align(gui);
                QolTrashReclaim.Align(gui);
            }
        }

        internal static void Align(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null || _panel == null) return;
            Transform weight = gui.m_player.Find("Weight");
            RectTransform rt = _panel.GetComponent<RectTransform>();
            if (weight != null)
            {
                RectTransform anchor = weight as RectTransform;
                if (anchor != null) { rt.anchorMin = anchor.anchorMin; rt.anchorMax = anchor.anchorMax; }
                rt.pivot = new Vector2(0f,.5f);
                rt.localPosition = weight.localPosition + new Vector3(1f,-52f,0f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f,0f);
                rt.anchoredPosition = new Vector2(16f,34f);
            }
            UpdateStates(gui);
        }

        internal static Vector3 RecyclePosition(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null) return Vector3.zero;
            Transform weight = gui.m_player.Find("Weight");
            return weight != null ? weight.localPosition + new Vector3(5f*IconSpacing+30f,-52f,0f)
                : new Vector3(180f,34f,0f);
        }

        private static Button MakeButton(RectTransform panel, string label,
            Sprite sprite, int index, Action handler)
        {
            GameObject go = new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(panel,false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            rt.sizeDelta = new Vector2(26f,26f);
            rt.anchoredPosition = new Vector2(index * IconSpacing+13f,0f);
            Image target = go.GetComponent<Image>();
            target.color = new Color(1f,1f,1f,.012f);
            target.raycastTarget = true;
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => handler());
            GameObject icon = new GameObject("Icon",typeof(RectTransform),typeof(Image));
            icon.transform.SetParent(go.transform,false);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f,.5f);
            iconRect.sizeDelta = new Vector2(21f,21f);
            iconRect.anchoredPosition = Vector2.zero;
            Image graphic = icon.GetComponent<Image>();
            graphic.sprite = sprite;
            graphic.preserveAspect = true;
            graphic.raycastTarget = false;
            QolIconHover hover = go.AddComponent<QolIconHover>();
            hover.Caption = label;
            return btn;
        }

        private static void ToggleFavorite()
        {
            QolFavorites.ToggleFavoriteMode();
            if (InventoryGui.instance != null) UpdateStates(InventoryGui.instance);
        }

        private static void UpdateStates(InventoryGui gui)
        {
            if (_storeButton != null)
                _storeButton.gameObject.SetActive(QolGameAccess.CurrentContainer(gui) != null);
            if (_favoriteImage != null)
                _favoriteImage.color = QolFavorites.FavoriteMode
                    ? new Color(1f,1f,.7f,1f) : new Color(1f,1f,1f,.87f);
        }

        internal static void Hover(string label)
        {
            if (_hint == null) return;
            _hint.gameObject.SetActive(true);
            _hint.text = label;
        }
        internal static void Unhover()
        {
            if (_hint != null) _hint.gameObject.SetActive(false);
        }

        private static void SortPlayer()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            Inventory inventory = player.GetInventory();
            if (inventory == null) return;
            int width = inventory.GetWidth(), height = inventory.GetHeight();
            if (width < 1 || height < 2) return;
            var fixedPositions = new HashSet<int>();
            var movable = new List<ItemDrop.ItemData>();
            var slots = new List<Vector2i>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_shared == null) continue;
                int position = item.m_gridPos.y * width + item.m_gridPos.x;
                if (item.m_gridPos.y == 0 || item.m_equipped ||
                    QolFavorites.IsFavorite(item, inventory))
                    fixedPositions.Add(position);
                else movable.Add(item);
            }
            for (int y=1;y<height;y++)
                for(int x=0;x<width;x++)
                {
                    var pos = new Vector2i(x,y);
                    if (fixedPositions.Contains(y*width+x) || QolFavorites.IsFavoriteSlot(pos, inventory))
                        continue;
                    slots.Add(pos);
                }
            if (slots.Count < movable.Count)
            {
                player.Message(MessageHud.MessageType.TopLeft,
                    "Sort: nie udało się znaleźć bezpiecznych miejsc.");
                return;
            }
            movable.Sort((a,b)=>
            {
                int cat = ((int)a.m_shared.m_itemType).CompareTo((int)b.m_shared.m_itemType);
                if (cat != 0) return cat;
                int name = string.CompareOrdinal(a.m_shared.m_name,b.m_shared.m_name);
                return name != 0 ? name : b.m_quality.CompareTo(a.m_quality);
            });
            for (int i=0;i<movable.Count;i++) movable[i].m_gridPos = slots[i];
            QolGameAccess.NotifyChanged(inventory);
            player.Message(MessageHud.MessageType.TopLeft,"Sort: uporządkowano ekwipunek.");
        }


        internal static void Shutdown()
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            _panel = null; _favoriteButton = null; _favoriteImage = null;
            _storeButton = null; _hint = null;
        }
    }

    internal sealed class QolIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal string Caption;
        public void OnPointerEnter(PointerEventData data) { QolStoreSort.Hover(Caption); }
        public void OnPointerExit(PointerEventData data) { QolStoreSort.Unhover(); }
    }
}
