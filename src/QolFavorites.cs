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
        private static Texture2D[] _textures = new Texture2D[4];

        internal static Sprite TrashIcon => _trash;
        internal static Sprite TrashMask => _trashMask;

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
        }

        private static bool Modifier => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private static bool Valid(InventoryGrid grid)
        {
            return grid != null && InventoryGui.instance != null &&
                grid == InventoryGui.instance.m_playerGrid &&
                Player.m_localPlayer != null && Modifier &&
                InventoryGui.instance.m_dragItem == null;
        }

        private static bool Left(InventoryGrid __instance, UIInputHandler clickHandler)
        {
            if (!Valid(__instance) || clickHandler == null) return true;
            Vector2i pos = __instance.GetButtonPos(clickHandler.gameObject);
            if (pos.x < 0 || pos.y < 0) return true;
            ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(pos.x, pos.y);
            if (item == null || item.m_shared == null) return true;
            Ensure();
            if (!Names.Add(item.m_shared.m_name)) Names.Remove(item.m_shared.m_name);
            Save();
            return false;
        }

        private static bool Right(InventoryGrid __instance, UIInputHandler element)
        {
            if (!Valid(__instance) || element == null) return true;
            Vector2i pos = __instance.GetButtonPos(element.gameObject);
            if (pos.x < 0 || pos.y < 0) return true;
            Ensure();
            int key = pos.y * __instance.m_inventory.GetWidth() + pos.x;
            if (!Slots.Add(key)) Slots.Remove(key);
            Save();
            return false;
        }

        internal static bool IsFavorite(ItemDrop.ItemData item, Inventory inv)
        {
            if (item == null || item.m_shared == null) return false;
            Ensure();
            return Names.Contains(item.m_shared.m_name) ||
                (inv != null && Slots.Contains(item.m_gridPos.y * inv.GetWidth() + item.m_gridPos.x));
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
                    overlay.color = new Color(1f, 0.75f, 0.13f, 0.95f);
                }
                ItemDrop.ItemData item = ___m_inventory.GetItemAt(index % width, index / width);
                bool slot = Slots.Contains(index);
                bool name = item != null && item.m_shared != null && Names.Contains(item.m_shared.m_name);
                overlay.enabled = slot || name;
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
                        default: _textures[3] = texture; break;
                    }
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
            Names.Clear(); Slots.Clear(); _id = long.MinValue;
            foreach (Texture2D texture in _textures)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            _border = _trash = _trashMask = _reclaimBorder = null;
        }
    }
}
