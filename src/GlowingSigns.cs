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
        private static readonly Dictionary<int, Material> MaterialCache = new Dictionary<int, Material>();
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOuter = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPower = Shader.PropertyToID("_GlowPower");
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
                Material source = widget.font.material;
                if (source == null) return;
                int id = source.GetInstanceID();
                if (!MaterialCache.TryGetValue(id, out Material m) || m == null)
                {
                    m = new Material(source)
                    {
                        name = source.name + " (Sirroh QOL Signs)",
                        hideFlags = HideFlags.DontSave
                    };
                    Shader legacy = Shader.Find("TextMeshPro/Distance Field");
                    if (legacy != null) m.shader = legacy;
                    // Preserve the font atlas and per-sign vertex color tint.
                    if (m.HasProperty(FaceColor)) m.SetColor(FaceColor, Color.white);
                    if (m.HasProperty(OutlineColor)) m.SetColor(OutlineColor, new Color(0f, 0f, 0f, 0.85f));
                    if (m.HasProperty(OutlineWidth)) m.SetFloat(OutlineWidth, 0.12f);
                    if (m.HasProperty(GlowColor)) m.SetColor(GlowColor, new Color(1f, 1f, 1f, 0.8f));
                    if (m.HasProperty(GlowOuter)) m.SetFloat(GlowOuter, 0.15f);
                    if (m.HasProperty(GlowPower)) m.SetFloat(GlowPower, 0.5f);
                    MaterialCache[id] = m;
                }
                if (widget.fontSharedMaterial == m) return;
                widget.fontSharedMaterial = m;
                widget.UpdateMeshPadding();
                widget.SetMaterialDirty();
            }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                _host.Log.LogWarning("GlowingSigns skipped one sign: " + e);
            }
        }

        internal static void Shutdown()
        {
            foreach (Material m in MaterialCache.Values)
                if (m != null) UnityEngine.Object.Destroy(m);
            MaterialCache.Clear();
        }
    }
}
