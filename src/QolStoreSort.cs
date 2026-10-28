using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
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
        private static QolIconHover _favoriteHover;
        private static RectTransform _overlayRect;
        private static RectTransform _woodRect;
        private static Canvas _canvas;
        private static InventoryGui _gui;
        private static bool _inventoryShowing;
        private static int _screenWidth, _screenHeight;

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        // 1920x1080 image evidence only; runtime position uses the actual
        // Weight side tab and player inventory bounds instead of screen X/Y.
        // Reference layout at 1920x1080: match the existing slim right-hand
        // inventory rail beneath its shield and weight controls. Wood size
        // tracks those tabs; artwork stays smaller than its clickable surface.
        private const float RecycleY = 190f;
        // Screenshot fallback. Runtime placement is taken from actual RectTransforms.
         private const float DefaultRailRight = 684f;
        private const float DefaultPanelBottom = 475f;
        private const float DefaultWeightBottom = 300f;
        private const float ActionHitSize = 52f;
        private const float ActionIconInset = 9f;
        private const float RecycleHitSize = ActionHitSize;
        // Shift from the original (partially hidden) 84px board center to
        // the center of the visible right-hand tab in the user's screenshot.
        private const float VisibleRailInset = 6f;
        private const float ArmorShieldX = 643f;
        private const float ArmorShieldY = 80f;

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
            _inventoryShowing = true;
            Align(__instance);
            RefreshVisibility(__instance);
            // Reuse the native shader; do not run a parallel day/night timer.
            QolNativeUiLighting.Sync(__instance, _woodRoot, _overlay, _favoritePanel);
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(NextFramePosition(__instance));
        }

        private static void OnHide()
        {
            _inventoryShowing = false;
            if (_overlay != null) _overlay.SetActive(false);
            if (_woodRoot != null) _woodRoot.SetActive(false);
            if (_favoritePanel != null) _favoritePanel.SetActive(false);
        }

        internal static RectTransform OverlayParent(InventoryGui gui)
        {
            Ensure(gui);
            return _overlayRect;
        }

        internal static bool ActionsVisible => _overlay != null && _overlay.activeInHierarchy;

        // These are the Valheim 1.0 inventory modal screens. They may be
        // drawn in a different canvas or earlier sibling order by UI mods,
        // so z-order alone cannot reliably conceal our free-positioned tabs.
        private static bool HasBlockingDialog(InventoryGui gui)
        {
            return gui.m_skillsDialog != null && gui.m_skillsDialog.gameObject.activeInHierarchy
                || gui.m_textsDialog != null && gui.m_textsDialog.gameObject.activeInHierarchy
                || gui.m_trophiesPanel != null && gui.m_trophiesPanel.activeInHierarchy
                || gui.m_achievementsPanel != null && gui.m_achievementsPanel.gameObject.activeInHierarchy
                || gui.m_variantDialog != null && gui.m_variantDialog.gameObject.activeInHierarchy
                || gui.m_splitDialog != null && gui.m_splitDialog.gameObject.activeInHierarchy;
        }

        private static void RefreshVisibility(InventoryGui gui)
        {
            if (gui == null || gui != _gui || _overlay == null) return;
            // Show/Hide patches provide exact visibility without relying on
            // InventoryGui.IsVisible()'s one-frame grace period after Hide.
            bool show = _inventoryShowing && InventoryGui.IsVisible()
                && gui.m_player != null && gui.m_player.gameObject.activeInHierarchy
                && !HasBlockingDialog(gui)
                && !UnifiedPopup.IsVisible() && !Menu.IsVisible();
            if (_overlay.activeSelf != show) _overlay.SetActive(show);
            if (_woodRoot != null && _woodRoot.activeSelf != show) _woodRoot.SetActive(show);
            if (_favoritePanel != null && _favoritePanel.activeSelf != show)
                _favoritePanel.SetActive(show);
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
            // Place actions directly above the inventory, not above every
            // vanilla/third-party dialog sharing this parent.
            _overlay.transform.SetSiblingIndex(gui.m_player.GetSiblingIndex() + 1);
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

            // The Favorite star belongs in the center of the ARMOR SHIELD,
            // not in the Weight action strip. Store All stays by Weight.
            _favoritePanel = new GameObject("SirrohQOLFavorites", typeof(RectTransform));
            _favoritePanel.transform.SetParent(gui.m_player, false);
            RectTransform fav = _favoritePanel.GetComponent<RectTransform>();
            fav.sizeDelta = new Vector2(145f, 30f);
            _favoriteButton = MakeSmallButton(_overlayRect,"Ulubione (Alt + LPM / PPM)",
                QolFavorites.FavoriteIcon,0,ToggleFavorite);
            _favoriteImage = _favoriteButton.transform.Find("Icon").GetComponent<Image>();
            _favoriteHover = _favoriteButton.GetComponent<QolIconHover>();
            // A 17px star fits inside the 27x33px metal armor-shield icon.
            _favoriteImage.rectTransform.sizeDelta = new Vector2(19f,19f);
            _storeButton = MakeSmallButton(fav,"Store All",QolFavorites.QuickIcon,4,
                QolNearbyInventory.StartStoreCurrent);

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
            // Zero alpha removes the visible gray quad while the Image still
            // participates in the Unity GraphicRaycaster for Button clicks.
            background.color = Color.clear;
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
            var hover = go.AddComponent<QolIconHover>();
            hover.Initialize(display, false);
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
            hit.color = Color.clear;
            hit.raycastTarget = true;
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
            var hover = go.AddComponent<QolIconHover>();
            hover.Initialize(image, sprite == QolFavorites.FavoriteIcon);
            return btn;
        }

        private static IEnumerator NextFramePosition(InventoryGui gui)
        {
            yield return null;
            if (gui == _gui && _overlay != null)
            {
                Align(gui);
                QolTrashReclaim.Align(gui);
                // QolTrashReclaim and vanilla inventory borders can be
                // constructed by different Show/UpdateGui patch priorities.
                QolNativeUiLighting.Sync(gui, _woodRoot, _overlay, _favoritePanel);
            }
        }

        internal static void Align(InventoryGui gui)
        {
            if (gui == null || gui != _gui || _overlay == null) return;
            PositionActions(gui);
            PositionFavorite(gui);
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
            if (_favoriteHover != null) _favoriteHover.Refresh();
        }

        private static RectTransform RootFor(RectTransform rt)=>rt.parent as RectTransform;
        private static Camera EventCamera =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

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

        private static void PositionFavorite(InventoryGui gui)
        {
            if (_favoriteButton == null || gui == null || gui.m_player == null)
                return;
            float scale = Mathf.Min(Screen.width/DesignWidth,
                                    Screen.height/DesignHeight);
            float offsetX = (Screen.width-DesignWidth*scale)*.5f;
            float offsetY = (Screen.height-DesignHeight*scale)*.5f;
            Vector2 target = new Vector2(offsetX+ArmorShieldX*scale,
                offsetY+(DesignHeight-ArmorShieldY)*scale);
            // Prefer the actual shield icon so it follows inventory position
            // and scaling, including UI-overhaul repositioning.
            Transform armor = gui.m_player.Find("Armor");
            RectTransform shield = armor != null
                ? armor.Find("armor_icon") as RectTransform : null;
            if (shield != null && shield.gameObject.activeInHierarchy)
            {
                Rect rect = ScreenBounds(shield);
                if(rect.width >= 12f*scale && rect.width <= 65f*scale &&
                   rect.height >= 12f*scale && rect.height <= 65f*scale)
                    target=rect.center;
            }
            PositionScreen(_favoriteButton.transform as RectTransform,
                target, 24f*scale,24f*scale);
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
            float weightTop=weightBottom+70f*scale;
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
                    weightTop=weightTab.yMax;
                    nativeTabHeight=weightTab.height;
                    dynamic=true;
                }
            }
            if(!dynamic && !_warnedLayoutFallback)
            {
                _warnedLayoutFallback=true;
                // The reference layout is an intentional supported fallback
                // when a UI overhaul replaces the native Weight tab geometry.
                Plugin.Instance?.Log.LogInfo(
                    "QOL icons: native Weight/tab geometry replaced; using reference proportional rail layout.");
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
            // Board image remains where it was; its exposed face has a
            // different optical center because the left end sits behind
            // the default inventory. Move only icons/hitboxes right.
            float iconCenterX=centerX+Mathf.Min(VisibleRailInset*scale,woodWidth*.18f);
            float lower=playerBottom+4f*scale;
            float upper=weightBottom-3f*scale;
            float available=upper-lower;
            // Never allow three wooden buttons to extend beyond the frame.
            // If a replacement GUI leaves too little vertical space, use the
            // conservative reference bounds and still keep the 3 boards inside.
            if(available<105f*scale || available>320f*scale)
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
            PositionScreen(_sort,new Vector2(iconCenterX,top),iconHit,iconHit);
            PositionScreen(_quick,new Vector2(iconCenterX,middle),iconHit,iconHit);
            PositionScreen(_restock,new Vector2(iconCenterX,bottom),iconHit,iconHit);
            PositionScreen(_sortWood,new Vector2(centerX,top),woodWidth,boardHeight);
            PositionScreen(_quickWood,new Vector2(centerX,middle),woodWidth,boardHeight);
            PositionScreen(_restockWood,new Vector2(centerX,bottom),woodWidth,boardHeight);

            // Recycle remains immediately above Weight, like the existing
            // native recycling bar. Preserve its position relative to Weight.
            _recycleCenter=new Vector2(iconCenterX,weightTop+nativeTabHeight*.5f+3f*scale);
            // Match Recycle's wooden tab height exactly to the three
            // lower buttons; keep its anchored position above Weight.
            PositionScreen(_recycleWood,_recycleCenter,woodWidth,boardHeight);
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
            RefreshVisibility(_gui);
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

        private static void SortPlayer()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            Inventory inventory = player.GetInventory();
            if (inventory == null) return;
            int width = inventory.GetWidth();
            if (width < 1 || inventory.GetHeight() < 2) return;
            if (!QolGameAccess.TryGetQuickSortHeight(inventory, out int regularHeight))
            {
                player.Message(MessageHud.MessageType.TopLeft,
                    "Sort: nie można odczytać slotów ExtraSlots; przerwano bez zmian.");
                return;
            }
            if (regularHeight < 2) return;
            var fixedPositions = new HashSet<int>();
            var movable = new List<ItemDrop.ItemData>();
            var slots = new List<Vector2i>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_shared == null ||
                    item.m_gridPos.y >= regularHeight) continue;
                int position = item.m_gridPos.y * width + item.m_gridPos.x;
                if (item.m_gridPos.y == 0 || item.m_equipped ||
                    QolFavorites.IsFavorite(item, inventory))
                    fixedPositions.Add(position);
                else movable.Add(item);
            }
            for (int y=1;y<regularHeight;y++)
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
            _inventoryShowing = false;
            _favoriteButton=null;_favoriteImage=null;_storeButton=null;
            _favoriteHover=null;_sort=null;_quick=null;_restock=null;
            _recycleWood=null;_sortWood=null;_quickWood=null;_restockWood=null;
            _warnedLayoutFallback=false; _recycleCenter=Vector2.zero;
        }
    }

    /// <summary>
    /// The tiny Favorite star never uses Outline: Unity UI's four shifted
    /// silhouette copies looked like several misaligned stars. Instead,
    /// a radial golden halo behind ONE star indicates edit mode. Larger
    /// QOL icons retain their usual hover outline; no Update loop.
    /// </summary>
    internal sealed class QolIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _graphic;
        private Outline _outline;
        private Image _favoriteHalo;
        private Vector3 _normalScale;
        private bool _isFavorite;
        private bool _hovered;

        internal void Initialize(Image graphic, bool isFavorite)
        {
            _graphic = graphic;
            _isFavorite = isFavorite;
            if (_graphic == null) return;
            _normalScale = _graphic.rectTransform.localScale;

            if (_isFavorite)
            {
                // Unity UI Outline duplicates the source vertices four times.
                // On the 19px shield star it appears as four shifted stars.
                // NEVER attach Outline (or Shadow) to this Image.
                Outline stale = _graphic.GetComponent<Outline>();
                if (stale != null) stale.enabled = false;
                if (QolFavorites.FavoriteHaloIcon != null)
                {
                    var haloObject = new GameObject("SirrohFavoriteHalo",
                        typeof(RectTransform), typeof(Image));
                    // A separate circular gradient, not a copy of the star.
                    // Parent to the button BEHIND its Icon child.
                    haloObject.transform.SetParent(transform, false);
                    haloObject.transform.SetAsFirstSibling();
                    var rect = haloObject.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(30f, 30f);
                    _favoriteHalo = haloObject.GetComponent<Image>();
                    _favoriteHalo.sprite = QolFavorites.FavoriteHaloIcon;
                    _favoriteHalo.raycastTarget = false;
                    _favoriteHalo.preserveAspect = true;
                    _favoriteHalo.material = null; // unlit emission-like aura
                    _favoriteHalo.enabled = false;
                }
            }
            else
            {
                // Keep the already working hover outline on larger QOL icons.
                _outline = _graphic.GetComponent<Outline>();
                if (_outline == null)
                    _outline = _graphic.gameObject.AddComponent<Outline>();
                _outline.effectColor = new Color(1f, .80f, .43f, .78f);
                _outline.effectDistance = new Vector2(1.6f, 1.6f);
                _outline.useGraphicAlpha = true;
                _outline.enabled = false;
            }
            Refresh();
        }

        internal void Refresh()
        {
            if (_graphic == null) return;
            bool activeFavorite = _isFavorite && QolFavorites.FavoriteMode;
            if (_isFavorite)
            {
                if (activeFavorite && QolFavorites.FavoriteGoldIcon != null)
                {
                    // Image.color is MULTIPLICATIVE and cannot transform the
                    // dark pixels of the bronze asset to fully gold.
                    if (_graphic.sprite != QolFavorites.FavoriteGoldIcon)
                        _graphic.sprite = QolFavorites.FavoriteGoldIcon;
                    _graphic.color = Color.white;
                    // Unity UI/Default is unlit: emission-like visibility
                    // only while editing favorites, without global changes.
                    _graphic.material = null;
                }
                else
                {
                    if (_graphic.sprite != QolFavorites.FavoriteIcon)
                        _graphic.sprite = QolFavorites.FavoriteIcon;
                    _graphic.color = _hovered
                        ? new Color(1f, .94f, .72f, 1f)
                        : new Color(1f, .78f, .52f, 1f);
                    QolNativeUiLighting.Apply(_graphic);
                }
            }
            else
            {
                _graphic.color = _hovered
                    ? new Color(1f, .96f, .73f, 1f)
                    : new Color(.89f, .89f, .89f, 1f);
            }
            if (_outline != null)
                _outline.enabled = !_isFavorite && _hovered;

            if (_favoriteHalo != null)
            {
                // Gradient stays aligned: ONE star, circular light behind it.
                _favoriteHalo.enabled = activeFavorite || _hovered;
                _favoriteHalo.color = activeFavorite
                    ? new Color(1f, .84f, .37f, .95f)
                    : new Color(1f, .77f, .35f, .38f);
            }
            // Avoid excessive scaling of the already very small shield glyph.
            _graphic.rectTransform.localScale = activeFavorite
                ? _normalScale * 1.06f
                : (_hovered ? _normalScale * 1.04f : _normalScale);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        private void OnDisable()
        {
            _hovered = false;
            Refresh();
        }
    }
}
