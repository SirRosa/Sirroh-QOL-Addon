using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    /// <summary>
    /// Reuses Valheim's own Custom/LitGui shader/material for addon graphics.
    /// EnvMan and the game's shader pipeline already update scene lighting
    /// for day/night and caves. We NEVER calculate a second darkness factor,
    /// poll EnvMan, tint images per frame, or overwrite global shader values.
    /// </summary>
    internal static class QolNativeUiLighting
    {
        private const string LitShaderName = "Custom/LitGui";
        private static Material _nativeLitMaterial;
        private static Material _fallbackMaterial;
        private static bool _warned;
        private static int _boundCount;

        private static Material FindLitMaterial(Transform searchRoot)
        {
            if (searchRoot == null) return null;
            Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null) continue;
                // Do not choose our own addon images: their material may
                // have been bound on a previous InventoryGui.Show.
                if (image.gameObject.name.StartsWith("Sirroh", StringComparison.Ordinal))
                    continue;
                Material candidate = image.material;
                if (candidate == null || candidate.shader == null) continue;
                if (string.Equals(candidate.shader.name, LitShaderName, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static Material Resolve(InventoryGui gui)
        {
            if (_nativeLitMaterial != null) return _nativeLitMaterial;
            if (gui == null || gui.m_player == null) return null;

            // Prefer the EXACT Valheim material assigned to the inventory.
            // Sharing it keeps the shader's lighting/saturation settings
            // synchronized with vanilla and avoids material copies.
            _nativeLitMaterial = FindLitMaterial(gui.m_player);
            if (_nativeLitMaterial == null)
                _nativeLitMaterial = FindLitMaterial(gui.transform);

            if (_nativeLitMaterial != null)
            {
                Plugin.Instance?.Log.LogInfo(
                    "QOL UI: linked to Valheim native Custom/LitGui material (" +
                    _nativeLitMaterial.name + ").");
                return _nativeLitMaterial;
            }

            // Some UI overhauls replace every native panel/material. Still
            // use Valheim's own LitGui shader if Unity loaded it, not a new
            // night-dimming implementation or manually computed color.
            if (_fallbackMaterial == null)
            {
                Shader shader = Shader.Find(LitShaderName);
                if (shader != null && shader.isSupported)
                {
                    _fallbackMaterial = new Material(shader);
                    _fallbackMaterial.name = "Sirroh QOL - native LitGui fallback";
                    _fallbackMaterial.hideFlags = HideFlags.HideAndDontSave;
                    Plugin.Instance?.Log.LogInfo(
                        "QOL UI: native inventory LitGui material not found; " +
                        "using Valheim Custom/LitGui shader fallback.");
                }
            }
            if (_fallbackMaterial == null && !_warned)
            {
                _warned = true;
                Plugin.Instance?.Log.LogWarning(
                    "QOL UI: Custom/LitGui is unavailable. Keeping existing " +
                    "UI materials rather than introducing a fake brightness loop.");
            }
            return _fallbackMaterial;
        }

        internal static void Apply(Image image)
        {
            if (image == null) return;
            Material material = _nativeLitMaterial != null ? _nativeLitMaterial : _fallbackMaterial;
            if (material == null || image.material == material) return;
            image.material = material;
            _boundCount++;
        }

        private static void ApplyImages(GameObject root)
        {
            if (root == null) return;
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                // Transparent hit surfaces remain on UI/Default, preserving
                // their original click behavior without unnecessary shaders.
                if (img == null || img.color.a < .00001f) continue;
                Apply(img);
            }
        }

        internal static void Sync(InventoryGui gui, GameObject wood,
            GameObject actions, GameObject favorites)
        {
            if (Resolve(gui) == null) return;
            int before = _boundCount;
            ApplyImages(wood);
            ApplyImages(actions);
            ApplyImages(favorites);
            // The player's gold/blue favorite slot borders are also ours,
            // although their parents belong to vanilla InventoryGrid.
            if (gui != null && gui.m_playerGrid != null)
            {
                foreach (Image image in gui.m_playerGrid.GetComponentsInChildren<Image>(true))
                    if (image != null && image.gameObject.name == "SirrohFavoriteBorder")
                        Apply(image);
            }
            if (_boundCount != before)
                Plugin.Instance?.Log.LogDebug("QOL UI: native lighting applied to " +
                    (_boundCount - before) + " new sprite Images.");
        }

        internal static void Shutdown()
        {
            _nativeLitMaterial = null;
            _warned = false;
            _boundCount = 0;
            if (_fallbackMaterial != null)
                UnityEngine.Object.Destroy(_fallbackMaterial);
            _fallbackMaterial = null;
        }
    }
}
