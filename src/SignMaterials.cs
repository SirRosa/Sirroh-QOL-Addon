using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Sirroh.Qol
{
    /// <summary>
    /// Visual-only recoloring of the *physical* sign mesh (not the TextMeshPro
    /// glyph material). The original sign text, prefab and ZDO are never changed.
    /// </summary>
    internal static class SignMaterials
    {
        private static readonly char[] Variants = { 'B', 'S', 'G', 'R', 'Y' };
        private static readonly Dictionary<char, Texture2D> Textures = new Dictionary<char, Texture2D>();
        // Cache one cloned PBR material per original surface and requested variant.
        private static readonly Dictionary<ulong, Material> MaterialCache = new Dictionary<ulong, Material>();
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static Plugin _host;
        private static bool _loggedMissing;
        private static bool _loggedFailure;

        internal static void Install(Plugin host)
        {
            _host = host;
            // Awake runs before ZDO permissions and may not yet have the final text.
            // The permission callback has already written censored/allowed text.
            host.Patch("SignMaterials.Permission", AccessTools.Method(typeof(Sign), "OnCheckPermissionCompleted"),
                postfix: AccessTools.Method(typeof(SignMaterials), nameof(AfterPermission)), priority: Priority.Last);
            host.Patch("SignMaterials.Awake", AccessTools.Method(typeof(Sign), "Awake"),
                postfix: AccessTools.Method(typeof(SignMaterials), nameof(AfterAwake)));
        }

        private static void AfterAwake(Sign __instance)
        {
            // Do not parse m_currentText here: access control must run first.
            // The existing GlowingSigns module owns the text shader.
        }

        private static void AfterPermission(Sign __instance)
        {
            if (_host == null || __instance == null || __instance.m_textWidget == null) return;
            try
            {
                TextMeshProUGUI widget = __instance.m_textWidget;
                // Parse only the text that vanilla has allowed the user to see.
                // This prevents showing the underlying data to a denied viewer.
                string raw = widget.text;
                string visible = SignMaterialMarkup.Strip(raw, out char variant);
                if (!ReferenceEquals(raw, visible)) widget.text = visible;

                SignMaterialHolder holder = __instance.GetComponent<SignMaterialHolder>();
                if (!_host.GlowSignsEnabled.Value || variant == '\0')
                {
                    if (holder != null) holder.Restore();
                    return;
                }

                if (holder == null) holder = __instance.gameObject.AddComponent<SignMaterialHolder>();
                holder.Apply(variant);
            }
            catch (Exception e)
            {
                if (_loggedFailure) return;
                _loggedFailure = true;
                _host.Log.LogWarning("SignMaterials: one sign could not be updated; original material retained: " + e);
            }
        }

        internal static Material GetVariant(Material original, char variant)
        {
            if (original == null) return null;
            Texture2D texture = GetTexture(variant);
            if (texture == null) return null; // Fail closed rather than replacing with flat color.
            ulong key = ((ulong)(uint)original.GetInstanceID() << 8) | variant;
            if (MaterialCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            // Keep Valheim's original game shader, UVs, shading and geometry.
            // Only this sign's local sharedMaterial reference is replaced.
            Material result = new Material(original)
            {
                name = original.name + " [Sirroh Sign " + variant + "]",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (result.HasProperty(MainTex)) result.SetTexture(MainTex, texture);
            if (result.HasProperty(BaseMap)) result.SetTexture(BaseMap, texture);
            if (!result.HasProperty(MainTex) && !result.HasProperty(BaseMap))
            {
                UnityEngine.Object.Destroy(result);
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    _host.Log.LogWarning("SignMaterials: sign shader has no albedo sampler. Original material retained.");
                }
                return null;
            }
            if (result.HasProperty(ColorProp)) result.SetColor(ColorProp, Color.white);
            if (result.HasProperty(BaseColor)) result.SetColor(BaseColor, Color.white);

            // PBR metallicity and roughness per surface type. Never affect text.
            float metal, gloss;
            switch (variant)
            {
                case 'B': metal = 0.82f; gloss = 0.48f; break; // black metal
                case 'S': metal = 0.92f; gloss = 0.67f; break; // brushed silver
                case 'G': metal = 0.85f; gloss = 0.78f; break; // polished gold
                case 'R': metal = 0.18f; gloss = 0.65f; break; // red enamel
                default:  metal = 0.16f; gloss = 0.58f; break; // yellow paint
            }
            if (result.HasProperty(Metallic)) result.SetFloat(Metallic, metal);
            if (result.HasProperty(Glossiness)) result.SetFloat(Glossiness, gloss);
            if (result.HasProperty(Smoothness)) result.SetFloat(Smoothness, gloss);
            // Do not inherit a metallic map or wood normal map that would override
            // the replacement albedo/smoothness in some standard shader variants.
            result.DisableKeyword("_METALLICGLOSSMAP");
            result.DisableKeyword("_NORMALMAP");
            result.DisableKeyword("_EMISSION");
            if (result.HasProperty(EmissionColor)) result.SetColor(EmissionColor, Color.black);
            MaterialCache[key] = result;
            return result;
        }

        private static Texture2D GetTexture(char variant)
        {
            if (Textures.TryGetValue(variant, out Texture2D cached)) return cached;
            if (Array.IndexOf(Variants, variant) < 0) return null;
            try
            {
                string name = "Sirroh.Qol.Signs." + variant + ".rgba";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                {
                    if (stream == null)
                    {
                        if (!_loggedMissing)
                        {
                            _loggedMissing = true;
                            _host.Log.LogWarning("SignMaterials: embedded surface missing: " + name);
                        }
                        return null;
                    }
                    byte[] data = new byte[stream.Length];
                    int count = 0;
                    while (count < data.Length)
                    {
                        int n = stream.Read(data, count, data.Length - count);
                        if (n <= 0) throw new EndOfStreamException(name);
                        count += n;
                    }
                    if (data.Length != 256 * 256 * 4) throw new InvalidDataException("Invalid 256x256 RGBA sign surface: " + name);
                    Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, true)
                    {
                        name = "Sirroh Sign Albedo " + variant,
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 2,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    texture.LoadRawTextureData(data);
                    texture.Apply(true, true); // GPU-backed texture with mipmaps; discard CPU pixels.
                    Textures[variant] = texture;
                    return texture;
                }
            }
            catch (Exception e)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    _host.Log.LogWarning("SignMaterials: texture loading failed: " + e);
                }
                return null;
            }
        }

        internal static void Shutdown()
        {
            // Restore original renderer material references before disposing cached clones.
            foreach (SignMaterialHolder holder in UnityEngine.Object.FindObjectsByType<SignMaterialHolder>(FindObjectsSortMode.None))
                if (holder != null) holder.Restore();
            foreach (Material mat in MaterialCache.Values)
                if (mat != null) UnityEngine.Object.Destroy(mat);
            MaterialCache.Clear();
            foreach (Texture2D tex in Textures.Values)
                if (tex != null) UnityEngine.Object.Destroy(tex);
            Textures.Clear();
            _host = null;
            _loggedMissing = false;
            _loggedFailure = false;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class SignMaterialHolder : MonoBehaviour
    {
        private sealed class Binding
        {
            internal MeshRenderer Renderer;
            internal Material[] Originals;
        }

        private readonly List<Binding> _bindings = new List<Binding>(4);
        private char _current;

        internal void Apply(char variant)
        {
            if (_current == variant) return;
            if (_bindings.Count == 0)
            {
                // MeshRenderer only; UI sign lettering uses CanvasRenderer/TMP.
                MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length && i < 16; ++i)
                {
                    MeshRenderer renderer = renderers[i];
                    if (renderer == null || renderer.GetComponent<TextMeshPro>() != null) continue;
                    Material[] originals = renderer.sharedMaterials;
                    if (originals == null || originals.Length == 0) continue;
                    _bindings.Add(new Binding { Renderer = renderer, Originals = originals });
                }
            }
            bool changed = false;
            foreach (Binding b in _bindings)
            {
                if (b.Renderer == null) continue;
                Material[] set = (Material[])b.Originals.Clone();
                for (int i = 0; i < set.Length; ++i)
                {
                    Material replacement = SignMaterials.GetVariant(set[i], variant);
                    if (replacement == null) continue;
                    set[i] = replacement;
                    changed = true;
                }
                if (changed) b.Renderer.sharedMaterials = set;
            }
            _current = changed ? variant : '\0';
        }

        internal void Restore()
        {
            if (_current == '\0') return;
            foreach (Binding b in _bindings)
                if (b.Renderer != null) b.Renderer.sharedMaterials = b.Originals;
            _current = '\0';
        }

        private void OnDestroy() { _bindings.Clear(); }
    }
}
