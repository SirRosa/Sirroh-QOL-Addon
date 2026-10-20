using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class GlowingSigns
    {
        private static Plugin _host;
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private static Shader _legacyShader;
        private static bool _warned;

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
                // Use the original reference's legacy distance-field shader.
                // The previous QOL version forced outline and glow values onto
                // every glyph, washing out colors and reducing readability.
                if (_legacyShader == null) _legacyShader = Shader.Find("TextMeshPro/Distance Field");
                if (_legacyShader == null)
                {
                    Warn("TextMeshPro/Distance Field shader unavailable; leaving existing sign material.");
                    return;
                }
                Material source = widget.font.material;
                if (source == null) return;
                int key = source.GetInstanceID();
                if (!Materials.TryGetValue(key, out Material material) || material == null)
                {
                    material = new Material(source)
                    {
                        name = source.name + " (Sirroh legacy sign text)",
                        hideFlags = HideFlags.HideAndDontSave,
                        shader = _legacyShader
                    };
                    if (material.HasProperty(FaceColor)) material.SetColor(FaceColor, Color.white);
                    // No custom outline, glow, or emissive tint: legacy shader behavior
                    // preserves the color supplied by sign text styling.
                    Materials[key] = material;
                }
                if (widget.fontSharedMaterial == material) return;
                widget.fontSharedMaterial = material;
                widget.UpdateMeshPadding();
                widget.SetMaterialDirty();
            }
            catch (Exception e) { Warn("Could not apply legacy material: " + e); }
        }

        private static void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            _host.Log.LogWarning("GlowingSigns: " + message);
        }

        internal static void Shutdown()
        {
            foreach (Material m in Materials.Values)
                if (m != null) UnityEngine.Object.Destroy(m);
            Materials.Clear();
            _legacyShader = null;
        }
    }
}
