using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    /// <summary>
    /// Compact replacement for QuickStackStore's player Sort / Store All /
    /// Quickstack / Restock buttons. Sorting only touches eligible grid positions.
    /// </summary>
    internal static class QolStoreSort
    {
        private static GameObject _panel;

        internal static void Install(Plugin host)
        {
            host.Patch("QOL.StoreSort.Show", AccessTools.Method(typeof(InventoryGui), "Show"),
                postfix: AccessTools.Method(typeof(QolStoreSort), nameof(OnShow)));
        }

        private static void OnShow(InventoryGui __instance)
        {
            if (__instance == null || __instance.m_player == null) return;
            if (_panel != null) return;
            _panel = new GameObject("SirrohStorageActions", typeof(RectTransform));
            _panel.transform.SetParent(__instance.m_player, false);
            RectTransform panel = _panel.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0f);
            panel.pivot = new Vector2(.5f, 1f);
            panel.sizeDelta = new Vector2(380f, 42f);
            panel.anchoredPosition = new Vector2(0f, -4f);
            AddButton(panel, "Sort", 0, SortPlayer);
            AddButton(panel, "Store All", 1, QolNearbyInventory.StartStoreCurrent);
            AddButton(panel, "Quickstack Q", 2, () => QolNearbyInventory.StartArea(true));
            AddButton(panel, "Restock R", 3, () => QolNearbyInventory.StartArea(false));
        }

        private static void AddButton(RectTransform panel, string caption, int index, Action command)
        {
            GameObject go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f,.5f);
            rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(90f,33f);
            rect.anchoredPosition = new Vector2(50f + 94f * index,0f);
            Image image = go.GetComponent<Image>();
            image.color = new Color(.21f,.22f,.23f,.96f);
            go.GetComponent<Button>().onClick.AddListener(() => command());
            GameObject label = new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform,false);
            RectTransform lrect = label.GetComponent<RectTransform>();
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.offsetMin = lrect.offsetMax = Vector2.zero;
            TextMeshProUGUI t = label.GetComponent<TextMeshProUGUI>();
            t.fontSize = 15f;
            t.text = caption;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            InventoryGui gui = InventoryGui.instance;
            if (gui != null && gui.m_playerName != null) t.font = gui.m_playerName.font;
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
            _panel = null;
        }
    }
}
