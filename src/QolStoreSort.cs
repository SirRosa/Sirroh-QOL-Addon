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
    /// Compact attached side tabs. Their three rows fill the REAL gap between
    /// the game's Weight tab and the bottom edge of the player inventory.
    /// Separate wooden backplates are siblings behind m_player, with the same
    /// rightmost edge as the vanilla shield/weight boards. Favorite and Store
    /// All remain in their existing Weight-relative location.
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
        // Reference layout at 1920x1080: match the existing slim right-hand
        // inventory rail beneath its shield and weight controls. Wood size
        // tracks those tabs; artwork stays smaller than its clickable surface.
        private const float RecycleY = 190f;
        // Screenshot fallback. Runtime placement is taken from actual RectTransforms.
        private const float SortY = 328f;
        private const float QuickY = 385f;
        private const float RestockY = 442f;
        private const float DefaultRailRight = 684f;
        private const float DefaultPanelBottom = 475f;
        private const float DefaultWeightBottom = 300f;
        private const float ActionHitSize = 52f;
        private const float ActionIconInset = 9f;
        private const float RecycleHitSize = 54f;

        private static RectTransform _sort, _quick, _restock;
        private static RectTransform _recycleWood, _sortWood, _quickWood, _restockWood;

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

            // Each board is independent. The previous 200px high combined
            // lower board hung below the player panel and did not match the
            // native shield/weight tab widths.
            _recycleWood = MakeWood("ReclaimWoodTab");
            _sortWood = MakeWood("SortWoodTab");
            _quickWood = MakeWood("QuickstackWoodTab");
            _restockWood = MakeWood("RestockWoodTab");
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
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            return rt;
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
            // 34px icon inside a 52px hit target, no resizing of source PNG.
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(ActionIconInset, ActionIconInset);
            iconRt.offsetMax = new Vector2(-ActionIconInset, -ActionIconInset);
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

        private static readonly Vector3[] Corners = new Vector3[4];
        private static bool _warnedLayoutFallback;
        private static Vector2 _recycleCenter;

        private static Rect ScreenBounds(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(EventCamera,Corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(EventCamera,Corners[2]);
            float xMin=Mathf.Min(lower.x,upper.x), xMax=Mathf.Max(lower.x,upper.x);
            float yMin=Mathf.Min(lower.y,upper.y), yMax=Mathf.Max(lower.y,upper.y);
            return Rect.MinMaxRect(xMin,yMin,xMax,yMax);
        }

        private static void PositionScreen(RectTransform rect, Vector2 point,
            float screenWidth, float screenHeight)
        {
            if(rect==null)return;
            RectTransform parent=RootFor(rect);
            if(parent==null)return;
            // Anchors must be fixed to center, otherwise anchoredPosition
            // can be offset by the parent's size and misplace the wood.
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.pivot=new Vector2(.5f,.5f);
            Vector2 local;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,point,EventCamera,out local))
                rect.anchoredPosition=local;
            float canvasScale=_canvas != null ? Mathf.Max(.01f,_canvas.scaleFactor) : 1f;
            rect.sizeDelta=new Vector2(screenWidth/canvasScale,screenHeight/canvasScale);
        }

        private static void PositionActions(InventoryGui gui)
        {
            if(gui==null || gui.m_player==null)return;
            _screenWidth=Screen.width; _screenHeight=Screen.height;
            float scale=Mathf.Min(Screen.width/DesignWidth,Screen.height/DesignHeight);
            float offsetX=(Screen.width-DesignWidth*scale)*.5f;
            float offsetY=(Screen.height-DesignHeight*scale)*.5f;
            float rightEdge=offsetX+DefaultRailRight*scale;
            float playerBottom=offsetY+(DesignHeight-DefaultPanelBottom)*scale;
            float weightBottom=offsetY+(DesignHeight-DefaultWeightBottom)*scale;
            float nativeTabHeight=70f*scale;
            bool dynamic=false;

            RectTransform panel=gui.m_player as RectTransform;
            RectTransform weight=gui.m_player.Find("Weight") as RectTransform;
            if(panel!=null && weight!=null)
            {
                Rect inventory=ScreenBounds(panel);
                Rect weightTab=ScreenBounds(weight);
                // Some third-party UI mods use Weight as an icon instead of
                // the backing bar. Reject that invalid measurement.
                if(inventory.width>250f*scale &&
                    inventory.height>250f*scale &&
                    weightTab.width>35f*scale && weightTab.width<145f*scale &&
                    weightTab.height>35f*scale && weightTab.height<145f*scale &&
                    weightTab.xMax>inventory.xMax+12f*scale &&
                    weightTab.xMax<inventory.xMax+160f*scale &&
                    weightTab.yMin>inventory.yMin+95f*scale)
                {
                    rightEdge=weightTab.xMax;
                    playerBottom=inventory.yMin;
                    weightBottom=weightTab.yMin;
                    nativeTabHeight=weightTab.height;
                    dynamic=true;
                }
            }
            if(!dynamic && !_warnedLayoutFallback)
            {
                _warnedLayoutFallback=true;
                Plugin.Instance?.Log.LogWarning(
                    "QOL icons: Weight/tab bounds not usable; using 1920x1080 proportional rail layout.");
            }

            // Native exposed right edge is the authority. Extend wood LEFT
            // under the inventory (16px hidden), not a smaller tab floating
            // in front of it. The reference screenshot rail is about 68px
            // visible from the player frame to the end of the Weight tab.
            float woodWidth=84f*scale;
            if(dynamic && panel!=null)
            {
                Rect inventory=ScreenBounds(panel);
                float exposed=rightEdge-inventory.xMax;
                woodWidth=Mathf.Clamp(exposed+16f*scale,72f*scale,105f*scale);
            }
            float centerX=rightEdge-woodWidth*.5f;
            float lower=playerBottom+4f*scale;
            float upper=weightBottom-3f*scale;
            float available=upper-lower;
            // Never allow three wooden buttons to extend beyond the frame.
            // If a replacement GUI leaves too little vertical space, use the
            // conservative reference bounds and still keep the 3 boards inside.
            if(available<105f*scale)
            {
                lower=offsetY+(DesignHeight-472f)*scale;
                upper=offsetY+(DesignHeight-300f)*scale;
                available=upper-lower;
            }
            float slot=available/3f;
            float boardHeight=slot-1f*scale;
            float iconHit=Mathf.Min(ActionHitSize*scale,slot-4f*scale);
            float top=upper-slot*.5f;
            float middle=upper-slot*1.5f;
            float bottom=upper-slot*2.5f;
            PositionScreen(_sort,new Vector2(centerX,top),iconHit,iconHit);
            PositionScreen(_quick,new Vector2(centerX,middle),iconHit,iconHit);
            PositionScreen(_restock,new Vector2(centerX,bottom),iconHit,iconHit);
            PositionScreen(_sortWood,new Vector2(centerX,top),woodWidth,boardHeight);
            PositionScreen(_quickWood,new Vector2(centerX,middle),woodWidth,boardHeight);
            PositionScreen(_restockWood,new Vector2(centerX,bottom),woodWidth,boardHeight);

            // Recycle remains immediately above Weight, like the existing
            // native recycling bar. Preserve its position relative to Weight.
            _recycleCenter=new Vector2(centerX,weightBottom+nativeTabHeight*.5f+3f*scale);
            PositionScreen(_recycleWood,_recycleCenter,woodWidth,
                Mathf.Min(70f*scale,nativeTabHeight));
        }

        internal static void PositionRecycle(RectTransform rect)
        {
            if(rect==null)return;
            float scale=Mathf.Min(Screen.width/DesignWidth,Screen.height/DesignHeight);
            float hit=RecycleHitSize*scale;
            PositionScreen(rect,_recycleCenter,hit,hit);
        }

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
            _recycleWood=null;_sortWood=null;_quickWood=null;_restockWood=null;
            _warnedLayoutFallback=false; _recycleCenter=Vector2.zero;
        }
    }

    internal sealed class QolIconHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        internal string Caption;
        public void OnPointerEnter(PointerEventData data){QolStoreSort.Hover(Caption);}
        public void OnPointerExit(PointerEventData data){QolStoreSort.Unhover();}
    }
}
