using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.Qol
{
    /// <summary>
    /// Uses Valheim's Custom/LitGui shader and its game-managed environment
    /// lighting. The native material's _Brightness setting is calibrated for
    /// native dark background panels, not small amber QOL glyphs. Keep an
    /// independent, single cloned material per UI artwork category with only
    /// this built-in scalar boosted. The game's _Lighting, saturation and
    /// global day/night/indoor lighting remain fully intact.
    ///
    /// Never mutate Valheim's own shared material, create one per sprite,
    /// calculate darkness on CPU, or poll environment values per frame.
    /// </summary>
    internal static class QolNativeUiLighting
    {
        private const string LitShaderName = "Custom/LitGui";
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        // Calibrated to distinguish the new dark rail from the brighter
        // vanilla armor/weight labels at nighttime (reference screenshot).
        // Background should remain wooden/dark; golden glyphs must stay legible.
        private const float WoodBrightnessFactor = 1.25f;
        private const float GlyphBrightnessFactor = 2.40f;

        private static Material _nativeLitMaterial;
        private static Material _woodLitMaterial;
        private static Material _glyphLitMaterial;
        private static bool _warned;
        private static int _boundCount;

        private static Material FindLitMaterial(Transform searchRoot)
        {
            if (searchRoot == null) return null;
            Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null) continue;
                bool ourImage = false;
                Transform node = image.transform;
                while (node != null && node != searchRoot)
                {
                    if (node.gameObject.name.StartsWith("Sirroh", StringComparison.Ordinal))
                    {
                        ourImage = true;
                        break;
                    }
                    node = node.parent;
                }
                if (ourImage) continue;

                Material candidate = image.material;
                if (candidate == null || candidate.shader == null) continue;
                if (string.Equals(candidate.shader.name, LitShaderName,
                    StringComparison.Ordinal)) return candidate;
            }
            return null;
        }

        private static Material MakeVariant(Material reference, string label,
            float brightnessMultiplier)
        {
            Material variant = new Material(reference);
            variant.name = "Sirroh QOL - LitGui " + label;
            variant.hideFlags = HideFlags.HideAndDontSave;
            if (variant.HasProperty(BrightnessId))
            {
                float original = reference.GetFloat(BrightnessId);
                // Shader's documented neutral brightness is 1. Values from a
                // dark-panel preset may be lower. Never reduce an existing
                // native brightness; increase it within a safe, finite range.
                float adjusted = Mathf.Clamp(Mathf.Max(original, 1f) *
                    brightnessMultiplier, 1f, 4f);
                variant.SetFloat(BrightnessId, adjusted);
                Plugin.Instance?.Log.LogInfo(
                    "QOL LitGui " + label + ": native _Brightness=" +
                    original.ToString("F2") + ", QOL _Brightness=" +
                    adjusted.ToString("F2") + "; native _Lighting unchanged.");
            }
            else if (!_warned)
            {
                _warned = true;
                Plugin.Instance?.Log.LogWarning(
                    "QOL LitGui: shader has no _Brightness property. " +
                    "Keeping native lighting unchanged.");
            }
            return variant;
        }

        private static void DestroyVariants()
        {
            if (_woodLitMaterial != null)
                UnityEngine.Object.Destroy(_woodLitMaterial);
            if (_glyphLitMaterial != null)
                UnityEngine.Object.Destroy(_glyphLitMaterial);
            _woodLitMaterial = null;
            _glyphLitMaterial = null;
        }

        private static bool Resolve(InventoryGui gui)
        {
            if (_woodLitMaterial != null && _glyphLitMaterial != null)
                return true;
            if (gui == null || gui.m_player == null) return false;

            Material source = FindLitMaterial(gui.m_player);
            if (source == null) source = FindLitMaterial(gui.transform);
            if (source == null)
            {
                // Vanilla shader still participates in the game's global
                // lighting even if a UI-overhaul replaced inventory materials.
                Shader shader = Shader.Find(LitShaderName);
                if (shader == null || !shader.isSupported)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Plugin.Instance?.Log.LogWarning(
                            "QOL UI: native Custom/LitGui unavailable; " +
                            "keeping existing sprite materials.");
                    }
                    return false;
                }
                source = new Material(shader);
                source.hideFlags = HideFlags.HideAndDontSave;
                source.name = "Sirroh QOL - LitGui source fallback";
                _ownsSource = true;
                Plugin.Instance?.Log.LogWarning(
                    "QOL UI: no native inventory LitGui preset found; " +
                    "using the game's LitGui shader with default properties.");
            }
            else _ownsSource = false;

            DestroyVariants();
            if (_nativeLitMaterial != null && _ownsOldSource)
                UnityEngine.Object.Destroy(_nativeLitMaterial);
            _nativeLitMaterial = source;
            _ownsOldSource = _ownsSource;
            _woodLitMaterial = MakeVariant(source, "wood", WoodBrightnessFactor);
            _glyphLitMaterial = MakeVariant(source, "glyphs", GlyphBrightnessFactor);
            return true;
        }

        private static bool _ownsSource;
        private static bool _ownsOldSource;

        internal static void Apply(Image image)
        {
            Apply(image, false);
        }

        private static void Apply(Image image, bool wood)
        {
            if (image == null) return;
            Material material = wood ? _woodLitMaterial : _glyphLitMaterial;
            if (material == null || image.material == material) return;
            image.material = material;
            _boundCount++;
        }

        private static void ApplyImages(GameObject root, bool wood)
        {
            if (root == null) return;
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                // Transparent raycast hitboxes must remain on UI/Default.
                if (img == null || img.color.a < .00001f) continue;
                Apply(img, wood);
            }
        }

        internal static void Sync(InventoryGui gui, GameObject wood,
            GameObject actions, GameObject favorites)
        {
            if (!Resolve(gui)) return;
            int before = _boundCount;
            ApplyImages(wood, true);
            ApplyImages(actions, false);
            ApplyImages(favorites, false);
            if (gui != null && gui.m_playerGrid != null)
            {
                foreach (Image image in gui.m_playerGrid.GetComponentsInChildren<Image>(true))
                    if (image != null &&
                        image.gameObject.name == "SirrohFavoriteBorder")
                        Apply(image);
            }
            if (_boundCount != before)
                Plugin.Instance?.Log.LogDebug(
                    "QOL UI LitGui: assigned brightness variants to " +
                    (_boundCount - before) + " new images.");
        }

        internal static void Shutdown()
        {
            DestroyVariants();
            if (_nativeLitMaterial != null && _ownsOldSource)
                UnityEngine.Object.Destroy(_nativeLitMaterial);
            _nativeLitMaterial = null;
            _ownsOldSource = false;
            _ownsSource = false;
            _warned = false;
            _boundCount = 0;
        }
    }
}
