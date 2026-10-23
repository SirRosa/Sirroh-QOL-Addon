using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    internal static class QolFavorites
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> Slots = new HashSet<int>();
        private static long _id = long.MinValue;
        private static Sprite _border;
        private static Sprite _trash;
        private static Sprite _trashMask;
        private static Sprite _reclaimBorder;
        private static Sprite _recyclePanel;
        private static readonly Texture2D[] _textures = new Texture2D[11];
        private static Sprite _sortIcon, _favoriteIcon, _quickIcon, _restockIcon, _recycleIcon;
        private static bool _favoriteMode;
        internal static Sprite SortIcon => _sortIcon;
        internal static Sprite FavoriteIcon => _favoriteIcon;
        internal static Sprite QuickIcon => _quickIcon;
        internal static Sprite RestockIcon => _restockIcon;
        internal static Sprite RecycleIcon => _recycleIcon;
        private static Sprite _woodPanel;
        internal static Sprite WoodPanel => _woodPanel;
        internal static bool FavoriteMode => _favoriteMode;
        internal static void ToggleFavoriteMode() { _favoriteMode = !_favoriteMode; }

        internal static Sprite TrashIcon => _trash;
        internal static Sprite TrashMask => _trashMask;
        internal static Sprite ReclaimBorder => _reclaimBorder;
        internal static Sprite RecyclePanel => _recyclePanel;

        internal static void Install(Plugin host)
        {
            host.Patch("Favorites.Left", AccessTools.Method(typeof(InventoryGrid), "OnLeftDown"),
                prefix: AccessTools.Method(typeof(QolFavorites), nameof(Left)));
            host.Patch("Favorites.Right", AccessTools.Method(typeof(InventoryGrid), "OnRightDown"),
                prefix: AccessTools.Method(typeof(QolFavorites), nameof(Right)));
            host.Patch("Favorites.Borders", AccessTools.Method(typeof(InventoryGrid), "UpdateGui"),
                postfix: AccessTools.Method(typeof(QolFavorites), nameof(Draw)));
            _border = ReadSprite("border");
            _trash = ReadSprite("trash");
            _trashMask = ReadSprite("trashmask");
            _reclaimBorder = ReadSprite("trashingborder");
            _recyclePanel = ReadSprite("RecyclingPanel");
            _sortIcon = ReadSprite("ui_sort");
            _favoriteIcon = ReadSprite("ui_favorite");
            _quickIcon = ReadSprite("ui_quickstack");
            _restockIcon = ReadSprite("ui_restock");
            _recycleIcon = ReadSprite("ui_recycle");
            _woodPanel = ReadSprite("ui_wood");
        }

        private static bool Modifier => _favoriteMode || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private static bool Valid(InventoryGrid grid)
        {
            return grid != null && InventoryGui.instance != null &&
                grid == InventoryGui.instance.m_playerGrid &&
                Player.m_localPlayer != null && Modifier &&
                QolGameAccess.DragItem(InventoryGui.instance) == null;
        }

        private static bool Left(InventoryGrid __instance, object clickHandler)
        {
            if (!Valid(__instance) || clickHandler == null) return true;
            Vector2i pos = QolGameAccess.ButtonPosition(__instance, ((Component)clickHandler).gameObject);
            if (pos.x < 0 || pos.y < 0) return true;
            ItemDrop.ItemData item = QolGameAccess.Inventory(__instance).GetItemAt(pos.x, pos.y);
            if (item == null || item.m_shared == null) return true;
            Ensure();
            if (!Names.Add(item.m_shared.m_name)) Names.Remove(item.m_shared.m_name);
            Save();
            return false;
        }

        private static bool Right(InventoryGrid __instance, object element)
        {
            if (!Valid(__instance) || element == null) return true;
            Vector2i pos = QolGameAccess.ButtonPosition(__instance, ((Component)element).gameObject);
            if (pos.x < 0 || pos.y < 0) return true;
            Ensure();
            int key = pos.y * QolGameAccess.Inventory(__instance).GetWidth() + pos.x;
            if (!Slots.Add(key)) Slots.Remove(key);
            Save();
            return false;
        }

        internal static bool IsFavorite(ItemDrop.ItemData item, Inventory inv)
        {
            if (item == null || item.m_shared == null) return false;
            Ensure();
            return IsSirrohFavorite(item, inv) ||
                QolAzuAutoStoreCompatibility.IsAzuFavorite(item, inv);
        }

        // Raw Sirroh state only; Harmony's Azu postfix MUST NOT call a combined check.
        private static bool IsSirrohFavorite(ItemDrop.ItemData item, Inventory inv)
        {
            return item != null && item.m_shared != null &&
                (Names.Contains(item.m_shared.m_name) ||
                 (inv != null && Slots.Contains(item.m_gridPos.y * inv.GetWidth() + item.m_gridPos.x)));
        }

        internal static bool IsSirrohFavoriteForAzu(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null || Player.m_localPlayer == null) return false;
            Ensure();
            if (Names.Contains(item.m_shared.m_name)) return true;
            Inventory inv = Player.m_localPlayer.GetInventory();
            return inv != null && inv.ContainsItem(item) &&
                Slots.Contains(item.m_gridPos.y * inv.GetWidth() + item.m_gridPos.x);
        }

        internal static bool IsFavoriteSlot(Vector2i pos, Inventory inv)
        {
            Ensure();
            return inv != null && Slots.Contains(pos.y * inv.GetWidth() + pos.x);
        }

        internal static bool IsNameFavorite(ItemDrop.ItemData item)
        {
            Ensure();
            return item != null && item.m_shared != null && Names.Contains(item.m_shared.m_name);
        }

        private static void Draw(InventoryGrid __instance, Player player,
            Inventory ___m_inventory, List<InventoryElement> ___m_elements)
        {
            if (player == null || player != Player.m_localPlayer ||
                player.GetInventory() != ___m_inventory || ___m_elements == null)
                return;
            Ensure();
            int width = ___m_inventory.GetWidth();
            int height = ___m_inventory.GetHeight();
            int count = Math.Min(___m_elements.Count, width * height);
            for (int index = 0; index < count; index++)
            {
                Image baseImage = ___m_elements[index].m_queued;
                if (baseImage == null) continue;
                Transform found = baseImage.transform.Find("SirrohFavoriteBorder");
                Image overlay = found == null ? null : found.GetComponent<Image>();
                if (overlay == null)
                {
                    GameObject go = new GameObject("SirrohFavoriteBorder", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(baseImage.transform, false);
                    RectTransform rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    overlay = go.GetComponent<Image>();
                    overlay.sprite = _border;
                    overlay.raycastTarget = false;
                    QolNativeUiLighting.Apply(overlay);
                    overlay.color = new Color(1f, 0.75f, 0.13f, 0.95f);
                }
                ItemDrop.ItemData item = ___m_inventory.GetItemAt(index % width, index / width);
                bool slot = Slots.Contains(index);
                bool name = item != null && item.m_shared != null && Names.Contains(item.m_shared.m_name);
                // Display favorites from either provider using the same Sirroh border.
                bool azu = item != null && QolAzuAutoStoreCompatibility.IsAzuFavorite(item, ___m_inventory);
                if (azu && !slot) name = true;
                overlay.enabled = slot || name || azu;
                if (slot && name) overlay.color = new Color(1f, .85f, .25f, 1f);
                else if (slot) overlay.color = new Color(.45f, .85f, 1f, .92f);
                else overlay.color = new Color(1f, .72f, .15f, .92f);
            }
        }

        private static void Ensure()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            long id = p.GetPlayerID();
            if (_id == id) return;
            _id = id;
            Names.Clear();
            Slots.Clear();
            string path = FileName();
            try
            {
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("N:", StringComparison.Ordinal)) Names.Add(line.Substring(2));
                    else if (line.StartsWith("S:", StringComparison.Ordinal) &&
                        int.TryParse(line.Substring(2), out int index)) Slots.Add(index);
                }
            }
            catch (Exception e) { Plugin.Instance?.Log.LogWarning("Favorites load: " + e.Message); }
        }

        private static string FileName()
        {
            return Path.Combine(Paths.ConfigPath, "SirrohQOLFavorites_" + _id + ".txt");
        }

        private static void Save()
        {
            try
            {
                var data = new List<string>(Names.Count + Slots.Count);
                foreach (string n in Names) data.Add("N:" + n);
                foreach (int slot in Slots) data.Add("S:" + slot);
                File.WriteAllLines(FileName(), data.ToArray());
            }
            catch (Exception e) { Plugin.Instance?.Log.LogWarning("Favorites save: " + e.Message); }
        }

        private static Sprite ReadSprite(string name)
        {
            try
            {
                string res = "Sirroh.Qol.Assets." + name + ".rgba";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
                {
                    if (stream == null) throw new FileNotFoundException(res);
                    byte[] data = new byte[stream.Length];
                    int off = 0;
                    while (off < data.Length)
                    {
                        int len = stream.Read(data, off, data.Length - off);
                        if (len <= 0) throw new EndOfStreamException(res);
                        off += len;
                    }
                    int w = BitConverter.ToInt32(data, 0);
                    int h = BitConverter.ToInt32(data, 4);
                    if (w < 1 || h < 1 || w > 2048 || h > 2048 ||
                        data.Length != 8 + w * h * 4) throw new InvalidDataException(res);
                    byte[] rgba = new byte[data.Length - 8];
                    Buffer.BlockCopy(data, 8, rgba, 0, rgba.Length);
                    Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    texture.LoadRawTextureData(rgba);
                    texture.Apply(false, true);
                    texture.name = "Sirroh-" + name;
                    switch (name)
                    {
                        case "border": _textures[0] = texture; break;
                        case "trash": _textures[1] = texture; break;
                        case "trashmask": _textures[2] = texture; break;
                        case "trashingborder": _textures[3] = texture; break;
                        case "RecyclingPanel": _textures[4] = texture; break;
                        case "ui_sort": _textures[5] = texture; break;
                        case "ui_favorite": _textures[6] = texture; break;
                        case "ui_quickstack": _textures[7] = texture; break;
                        case "ui_restock": _textures[8] = texture; break;
                        case "ui_recycle": _textures[9] = texture; break;
                        case "ui_wood": _textures[10] = texture; break;
                    }
                    // 9-sliced backplate keeps the user's carved wood corners
                    // sharp even when the underlying plank is stretched.
                    if (name == "ui_wood")
                        return Sprite.Create(texture, new Rect(0,0,w,h),
                            new Vector2(.5f,.5f), 100f, 0,
                            SpriteMeshType.FullRect, new Vector4(10f,10f,10f,10f));
                    return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
                }
            }
            catch (Exception e)
            {
                Plugin.Instance?.Log.LogWarning("Cannot load original QOL sprite " + name + ": " + e.Message);
                return null;
            }
        }

        internal static void Shutdown()
        {
            Names.Clear(); Slots.Clear(); _id = long.MinValue; _favoriteMode = false;
            foreach (Texture2D texture in _textures)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            _border = _trash = _trashMask = _reclaimBorder = _recyclePanel = null;
            _sortIcon = _favoriteIcon = _quickIcon = _restockIcon = _recycleIcon = _woodPanel = null;
        }
    }
}
