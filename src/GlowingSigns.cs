using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Sirroh.Qol
{
    // A dedicated HDR material per sign font/tint. Never mutate shared font assets.
    internal static class GlowingSigns
    {
        private sealed class Entry
        {
            internal Material Material;
            internal Color Tint;
        }

        private static Plugin _host;
        private static readonly Dictionary<ulong, Entry> Materials = new Dictionary<ulong, Entry>();
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffset = Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowInner = Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOuter = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPower = Shader.PropertyToID("_GlowPower");
        private static Shader _legacyShader;
        private static bool _warned;
        private static bool _reportedGlow;

        internal static void Install(Plugin host)
        {
            _host = host;
            host.Patch("GlowingSigns.Awake", AccessTools.Method(typeof(Sign), "Awake"),
                postfix: AccessTools.Method(typeof(GlowingSigns), nameof(Apply)));
            host.Patch("GlowingSigns.Permissions", AccessTools.Method(typeof(Sign), "OnCheckPermissionCompleted"),
                postfix: AccessTools.Method(typeof(GlowingSigns), nameof(Apply)));
        }

        private static void Apply(Sign __instance)
        {
            if (_host == null || !_host.GlowSignsEnabled.Value || __instance == null) return;
            try
            {
                TextMeshProUGUI widget = __instance.m_textWidget;
                if (widget == null || widget.font == null) return;

                if (_legacyShader == null) _legacyShader = Shader.Find("TextMeshPro/Distance Field");
                if (_legacyShader == null)
                {
                    Warn("Legacy TextMeshPro/Distance Field shader unavailable; leaving sign unchanged.");
                    return;
                }
                Material source = widget.font.material;
                if (source == null) return;

                // Same font, different sign colors: never share a color-dependent halo.
                Color tint = GetTextTint(widget);
                Color32 c = tint;
                ulong colorKey = ((ulong)c.r << 24) | ((ulong)c.g << 16) |
                                 ((ulong)c.b << 8) | c.a;
                ulong key = ((ulong)(uint)source.GetInstanceID() << 32) | colorKey;
                if (!Materials.TryGetValue(key, out Entry entry) || entry.Material == null)
                {
                    Material material = new Material(source)
                    {
                        name = source.name + " (Sirroh emissive sign)",
                        hideFlags = HideFlags.HideAndDontSave,
                        shader = _legacyShader
                    };
                    Configure(material, tint);
                    entry = new Entry { Material = material, Tint = tint };
                    Materials[key] = entry;
                }

                if (widget.fontSharedMaterial == entry.Material) return;
                widget.fontSharedMaterial = entry.Material;
                widget.UpdateMeshPadding();
                widget.SetMaterialDirty();
            }
            catch (Exception e) { Warn("Cannot apply sign material: " + e); }
        }

        private static void Configure(Material material, Color tint)
        {
            // Bright face in HDR preserves color tags (TMP vertex colors).
            // Soft glow uses the sign's color; no sharp OUTLINE_ON.
            float intensity = _host.SignEmission.Value;
            if (material.HasProperty(FaceColor))
                material.SetColor(FaceColor, new Color(intensity, intensity, intensity, 1f));
            if (material.HasProperty(OutlineWidth)) material.SetFloat(OutlineWidth, 0f);
            material.DisableKeyword("OUTLINE_ON");

            if (material.HasProperty(GlowColor))
            {
                material.SetColor(GlowColor, new Color(
                    tint.r * intensity, tint.g * intensity, tint.b * intensity,
                    Mathf.Clamp01(_host.SignGlowStrength.Value * tint.a)));
                if (material.HasProperty(GlowOffset)) material.SetFloat(GlowOffset, 0f);
                if (material.HasProperty(GlowInner)) material.SetFloat(GlowInner, 0.045f);
                if (material.HasProperty(GlowOuter))
                    material.SetFloat(GlowOuter, _host.SignGlowRadius.Value);
                if (material.HasProperty(GlowPower)) material.SetFloat(GlowPower, 0.55f);

                // A GlowColor property alone does not select the GLOW_ON shader variant.
                material.EnableKeyword("GLOW_ON");
                if (!_reportedGlow)
                {
                    _reportedGlow = true;
                    _host.Log.LogInfo("GlowingSigns: sign-only emissive HDR face and GLOW_ON enabled.");
                }
            }
            else
            {
                material.DisableKeyword("GLOW_ON");
                Warn("Shader has no _GlowColor; using HDR glyph brightness only.");
            }
        }

        private static Color GetTextTint(TextMeshProUGUI widget)
        {
            Color tint = widget.color;
            // One rich-text color tag is common on Valheim signs. Read once
            // on sign initialization, never each frame.
            string raw = widget.text;
            if (string.IsNullOrEmpty(raw)) return tint;
            int begin = raw.IndexOf("<color=", StringComparison.OrdinalIgnoreCase);
            if (begin < 0) return tint;
            begin += 7;
            int end = raw.IndexOf('>', begin);
            if (end <= begin || end - begin > 32) return tint;
            string name = raw.Substring(begin, end - begin).Trim().Trim('"');
            if (!ColorUtility.TryParseHtmlString(name, out Color parsed)) return tint;
            return new Color(tint.r * parsed.r, tint.g * parsed.g,
                             tint.b * parsed.b, tint.a * parsed.a);
        }

        // Update existing materials in-place on BepInEx config changes.
        internal static void RefreshMaterials()
        {
            if (_host == null) return;
            foreach (Entry entry in Materials.Values)
                if (entry.Material != null) Configure(entry.Material, entry.Tint);
        }

        private static void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            _host?.Log.LogWarning("GlowingSigns: " + message);
        }

        internal static void Shutdown()
        {
            foreach (Entry entry in Materials.Values)
                if (entry.Material != null) UnityEngine.Object.Destroy(entry.Material);
            Materials.Clear();
            _legacyShader = null;
            _warned = false;
            _reportedGlow = false;
            _host = null;
        }
    }
}
