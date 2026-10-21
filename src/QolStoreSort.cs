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
    /// <summary>
    /// Precisely positioned, native-64px user icons, using the supplied
    /// transparent 1920x1080 reference sheet. A separate wooden backplate is
    /// drawn BEHIND the player's inventory. Favorite / Store All retain their
    /// original Weight-anchor placement.
    /// </summary>
    internal static class QolStoreSort
    {
        private static GameObject _favoritePanel;
        private static GameObject _overlay;
        private static GameObject _woodRoot;
        private static Button _favoriteButton;
        private static Button _storeButton;
        private static Image _favoriteImage;
        private static TextMeshProUGUI _hint;
        private static RectTransform _overlayRect;
        private static RectTransform _woodRect;
        private static Canvas _canvas;
        private static InventoryGui _gui;
        private static int _screenWidth, _screenHeight;

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const float ActionX = 645f;
        private const float RecycleY = 186.5f;
        private const float SortY = 326.5f;
        private const float QuickY = 384.5f;
        private const float RestockY = 442.5f;

        private static RectTransform _sort, _quick, _restock, _recycleWood, _actionsWood;

        internal static void Install(Plugin host)
        {
            host.Patch("QOL.StoreSort.Show", AccessTools.Method(typeof(InventoryGui), "Show"),
                postfix: AccessTools.Method(typeof(QolStoreSort), nameof(OnShow)));
            host.Patch("QOL.StoreSort.Hide", AccessTools.Method(typeof(InventoryGui), "Hide"),
                postfix: AccessTools.Method(typeof(QolStoreSort), nameof(OnHide)));
        }

        private static void OnShow(InventoryGui __instance)
        {
            Ensure(__instance);
            if (_overlay != null) _overlay.SetActive(true);
            if (_woodRoot != null) _woodRoot.SetActive(true);
            if (_favoritePanel != null) _favoritePanel.SetActive(true);
            Align(__instance);
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(NextFramePosition(__instance));
        }

        private static void OnHide()
        {
            if (_overlay != null) _overlay.SetActive(false);
            if (_woodRoot != null) _woodRoot.SetActive(false);
            if (_favoritePanel != null) _favoritePanel.SetActive(false);
            Unhover();
        }

        internal static RectTransform OverlayParent(InventoryGui gui)
        {
            Ensure(gui);
            return _overlayRect;
        }

        private static void Ensure(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null) return;
            if (_overlay != null && _gui == gui) return;
            // Handle destroyed/rebuilt inventory GUI, including ZenUI swaps.
            Shutdown();
            _gui = gui;
            _canvas = gui.GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas = _canvas.rootCanvas;
            Transform parent = gui.m_player.parent;
            if (parent == null) parent = gui.transform;
            // Background is a sibling BELOW m_player, not a child overlay.
            _woodRoot = new GameObject("SirrohQOLWoodBehindInventory", typeof(RectTransform));
            _woodRoot.transform.SetParent(parent, false);
            _woodRect = _woodRoot.GetComponent<RectTransform>();
            Stretch(_woodRect);
            _woodRoot.transform.SetSiblingIndex(gui.m_player.GetSiblingIndex());

            _overlay = new GameObject("SirrohQOLScreenIcons", typeof(RectTransform));
            _overlay.transform.SetParent(parent, false);
            _overlayRect = _overlay.GetComponent<RectTransform>();
            Stretch(_overlayRect);
            _overlay.transform.SetAsLastSibling();
            // The HUD must never capture clicks outside the icon rects.
            // This overlay has no Image or other raycast-capable component.

            // Wood appears from behind the left edge of the inventory, with
            // separate red-button and three-button sections as on the mockup.
            _recycleWood = MakeWood("ReclaimWoodTab");
            _actionsWood = MakeWood("StackWoodTab");
            _sort = MakeAction("Sort", QolFavorites.SortIcon, SortPlayer);
            _quick = MakeAction("Quickstack (Q)", QolFavorites.QuickIcon,
                () => QolNearbyInventory.StartArea(true));
            _restock = MakeAction("Restock (R)", QolFavorites.RestockIcon,
                () => QolNearbyInventory.StartArea(false));

            // Favorite (and the existing Store All shortcut) remain in their
            // previous place, below the Weight element.
            _favoritePanel = new GameObject("SirrohQOLFavorites", typeof(RectTransform));
            _favoritePanel.transform.SetParent(gui.m_player, false);
            RectTransform fav = _favoritePanel.GetComponent<RectTransform>();
            fav.sizeDelta = new Vector2(145f, 30f);
            _favoriteButton = MakeSmallButton(fav,"Ulubione (Alt + LPM / PPM)",
                QolFavorites.FavoriteIcon,3,ToggleFavorite);
            _favoriteImage = _favoriteButton.transform.Find("Icon").GetComponent<Image>();
            _storeButton = MakeSmallButton(fav,"Store All",QolFavorites.QuickIcon,4,
                QolNearbyInventory.StartStoreCurrent);

            GameObject tooltip = new GameObject("SirrohQOLTooltip",
                typeof(RectTransform),typeof(TextMeshProUGUI));
            tooltip.transform.SetParent(_overlay.transform,false);
            RectTransform tipRect = tooltip.GetComponent<RectTransform>();
            tipRect.anchorMin = tipRect.anchorMax = new Vector2(.5f,.5f);
            tipRect.pivot = new Vector2(0f,0f);
            tipRect.sizeDelta = new Vector2(270f,22f);
            _hint = tooltip.GetComponent<TextMeshProUGUI>();
            _hint.fontSize = 14f;
            _hint.alignment = TextAlignmentOptions.Left;
            _hint.raycastTarget = false;
            if (gui.m_playerName != null) _hint.font = gui.m_playerName.font;
            tooltip.SetActive(false);
            PositionActions(gui);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(.5f,.5f);
        }

        private static RectTransform MakeWood(string name)
        {
            GameObject go = new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(_woodRect,false);
            Image image = go.GetComponent<Image>();
            image.sprite = QolFavorites.WoodPanel;
            image.raycastTarget = false;
            image.preserveAspect = false;
            if (image.sprite != null) image.type = Image.Type.Sliced;
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform MakeAction(string name, Sprite sprite, Action command)
        {
            GameObject go = new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(_overlayRect,false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            Image background = go.GetComponent<Image>();
            background.color = new Color(1f,1f,1f,.012f);
            background.raycastTarget = true;
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(()=>command());
            GameObject icon = new GameObject("Icon",typeof(RectTransform),typeof(Image));
            icon.transform.SetParent(go.transform,false);
            RectTransform iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            Image display = icon.GetComponent<Image>();
            display.sprite = sprite;
            display.preserveAspect = true;
            display.raycastTarget = false;
            go.AddComponent<QolIconHover>().Caption = name;
            return rt;
        }

        private static Button MakeSmallButton(RectTransform panel,string name,
            Sprite sprite,int index,Action command)
        {
            GameObject go = new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(panel,false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            rt.sizeDelta = new Vector2(26f,26f);
            rt.anchoredPosition = new Vector2(index*28f+13f,0f);
            Image hit = go.GetComponent<Image>();
            hit.color = new Color(1f,1f,1f,.012f);
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(()=>command());
            GameObject icon = new GameObject("Icon",typeof(RectTransform),typeof(Image));
            icon.transform.SetParent(rt,false);
            RectTransform ir=icon.GetComponent<RectTransform>();
            ir.anchorMin=ir.anchorMax=new Vector2(.5f,.5f);
            ir.sizeDelta=new Vector2(21f,21f);
            ir.anchoredPosition=Vector2.zero;
            Image image=icon.GetComponent<Image>();
            image.sprite=sprite; image.preserveAspect=true; image.raycastTarget=false;
            go.AddComponent<QolIconHover>().Caption=name;
            return btn;
        }

        private static IEnumerator NextFramePosition(InventoryGui gui)
        {
            yield return null;
            if (gui == _gui && _overlay != null)
            {
                Align(gui);
                QolTrashReclaim.Align(gui);
            }
        }

        internal static void Align(InventoryGui gui)
        {
            if (gui == null || gui != _gui || _overlay == null) return;
            PositionActions(gui);
            if (_favoritePanel != null)
            {
                Transform weight=gui.m_player.Find("Weight");
                RectTransform rt=_favoritePanel.GetComponent<RectTransform>();
                if (weight != null)
                {
                    RectTransform anchor=weight as RectTransform;
                    if(anchor!=null){rt.anchorMin=anchor.anchorMin;rt.anchorMax=anchor.anchorMax;}
                    rt.pivot=new Vector2(0f,.5f);
                    rt.localPosition=weight.localPosition+new Vector3(1f,-52f,0f);
                }
            }
            if(_storeButton != null)
                _storeButton.gameObject.SetActive(QolGameAccess.CurrentContainer(gui)!=null);
            if(_favoriteImage != null)
                _favoriteImage.color=QolFavorites.FavoriteMode
                    ? new Color(1f,1f,.7f,1f) : Color.white;
        }

        // Full-screen reference coordinates from the uploaded 1920x1080 sheet.
        // Convert via the actual Unity canvas / parent rect, not m_player.localPos.
        private static Vector2 ScreenPoint(float designX,float designY)
        {
            float scale=Mathf.Min(Screen.width/DesignWidth, Screen.height/DesignHeight);
            float offsetX=(Screen.width-DesignWidth*scale)*.5f;
            float offsetY=(Screen.height-DesignHeight*scale)*.5f;
            return new Vector2(offsetX+designX*scale,
                offsetY+(DesignHeight-designY)*scale);
        }

        private static RectTransform RootFor(RectTransform rt)=>rt.parent as RectTransform;
        private static Camera EventCamera =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

        internal static void PositionAt(RectTransform rt,float designX,float designY,
            float designWidth,float designHeight)
        {
            if(rt==null)return;
            RectTransform parent=RootFor(rt);
            if(parent==null)return;
            Vector2 local;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                ScreenPoint(designX,designY),EventCamera,out local))
                rt.anchoredPosition=local;
            float scale=Mathf.Min(Screen.width/DesignWidth,Screen.height/DesignHeight);
            float canvasScale=_canvas != null ? Mathf.Max(.01f,_canvas.scaleFactor) : 1f;
            rt.sizeDelta=new Vector2(designWidth*scale/canvasScale,
                designHeight*scale/canvasScale);
        }

        private static void PositionActions(InventoryGui gui)
        {
            _screenWidth=Screen.width; _screenHeight=Screen.height;
            PositionAt(_sort,ActionX,SortY,64f,64f);
            PositionAt(_quick,ActionX,QuickY,64f,64f);
            PositionAt(_restock,ActionX,RestockY,64f,64f);
            // Button rects are 64px; image graphics fill the rect exactly.
            PositionAt(_recycleWood,ActionX,RecycleY,84f,88f);
            PositionAt(_actionsWood,ActionX,(SortY+RestockY)*.5f,84f,210f);
        }

        internal static void PositionRecycle(RectTransform rect) =>
            PositionAt(rect,ActionX,RecycleY,64f,64f);

        internal static void Tick()
        {
            if (_overlay!=null && _overlay.activeInHierarchy &&
                (_screenWidth!=Screen.width || _screenHeight!=Screen.height))
            {
                Align(_gui);
                QolTrashReclaim.Align(_gui);
            }
        }

        private static void ToggleFavorite()
        {
            QolFavorites.ToggleFavoriteMode();
            Align(_gui);
        }

        internal static void Hover(string caption)
        {
            if (_hint == null || _overlayRect == null) return;
            RectTransform rt=_hint.rectTransform;
            Vector2 pt;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRect,
                Input.mousePosition,EventCamera,out pt))
                rt.anchoredPosition=pt+new Vector2(16f,18f);
            _hint.text=caption;
            _hint.gameObject.SetActive(true);
        }
        internal static void Unhover()
        {
            if(_hint!=null)_hint.gameObject.SetActive(false);
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
            if(_favoritePanel!=null)UnityEngine.Object.Destroy(_favoritePanel);
            if(_overlay!=null)UnityEngine.Object.Destroy(_overlay);
            if(_woodRoot!=null)UnityEngine.Object.Destroy(_woodRoot);
            _favoritePanel=null;_overlay=null;_woodRoot=null;
            _overlayRect=null;_woodRect=null;_canvas=null;_gui=null;
            _favoriteButton=null;_favoriteImage=null;_storeButton=null;
            _hint=null;_sort=null;_quick=null;_restock=null;
            _recycleWood=null;_actionsWood=null;
        }
    }

    internal sealed class QolIconHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        internal string Caption;
        public void OnPointerEnter(PointerEventData data){QolStoreSort.Hover(Caption);}
        public void OnPointerExit(PointerEventData data){QolStoreSort.Unhover();}
    }
}
