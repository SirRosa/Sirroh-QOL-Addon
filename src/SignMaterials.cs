using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Sirroh.Qol
{
    // Runs on clients. Markup is stored as vanilla sign text; no ZDO mutation.
    internal static class SignMaterials
    {
        private static readonly Dictionary<char, Texture2D> Textures = new Dictionary<char, Texture2D>(5);
        private static readonly Dictionary<ulong, Material> Materials = new Dictionary<ulong, Material>(64);
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static Plugin _host;
        private static bool _textureErrorLogged;

        internal static void Install(Plugin host)
        {
            _host = host;
            // Sign.UpdateText is called when an edit arrives and every 2 s, even when
            // an existing permission callback was already satisfied.
            host.Patch("SignMaterials.UpdateText", AccessTools.Method(typeof(Sign), "UpdateText"),
                postfix: AccessTools.Method(typeof(SignMaterials), nameof(AfterUpdateText)));
            host.Patch("SignMaterials.Permission", AccessTools.Method(typeof(Sign), "OnCheckPermissionCompleted"),
                postfix: AccessTools.Method(typeof(SignMaterials), nameof(AfterPermission)),
                priority: Priority.Last);
        }

        private static void AfterUpdateText(Sign __instance)
        {
            if (_host == null || __instance == null) return;
            // For an asynchronous access check, the callback below is authoritative.
            // An already-granted sign may refresh synchronously as well.
            if (__instance.m_isViewable) Refresh(__instance);
        }

        private static void AfterPermission(Sign __instance)
        {
            Refresh(__instance);
        }

        private static void Refresh(Sign sign)
        {
            if (_host == null || sign == null || sign.m_textWidget == null) return;
            try
            {
                TextMeshProUGUI text = sign.m_textWidget;
                // Always parse the original persisted text, not a previously
                // stripped TMP string. Only display permitted content.
                string displayed = text.text ?? "";
                SignMaterialHolder holder = sign.GetComponent<SignMaterialHolder>();
                if (!_host.SignSurfaceEnabled.Value || !sign.m_isViewable)
                {
                    if (holder != null) holder.Restore();
                    return;
                }

                string raw = sign.m_currentText ?? "";
                string clean = SignMaterialMarkup.Strip(raw, out char variant);
                if (variant != '\0')
                {
                    // Do not bypass Valheim's filtered/censored text branch.
                    // If vanilla display doesn't correspond to raw, parse what
                    // vanilla granted instead; never reveal hidden content.
                    if (displayed != raw && displayed != clean)
                        clean = SignMaterialMarkup.Strip(displayed, out variant);
                }
                if (variant == '\0')
                {
                    if (holder != null) holder.Restore();
                    return;
                }
                if (displayed != clean) text.text = clean;

                if (holder == null) holder = sign.gameObject.AddComponent<SignMaterialHolder>();
                holder.Apply(sign, variant);
            }
            catch (Exception e)
            {
                _host.Log.LogWarning("SignMaterials: sign update failed on " + sign.name + ": " + e);
            }
        }

        internal static Material VariantMaterial(Material original, char variant)
        {
            if (original == null) return null;
            Texture2D texture = Texture(variant);
            if (texture == null) return null;
            ulong key = ((ulong)(uint)original.GetInstanceID() << 8) | (byte)variant;
            if (Materials.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material result = new Material(original)
            {
                name = "Sirroh/" + variant + "/" + original.name,
                hideFlags = HideFlags.HideAndDontSave
            };

            // Some Valheim sign surface shaders don't expose an albedo slot.
            // Rather than silently leaving the original wood texture in place,
            // switch this *renderer material only* to the standard PBR shader.
            if (!result.HasProperty(MainTex) && !result.HasProperty(BaseMap))
            {
                Shader pbr = Shader.Find("Standard");
                if (pbr != null) result.shader = pbr;
            }
            bool hasTexture = false;
            if (result.HasProperty(MainTex)) { result.SetTexture(MainTex, texture); hasTexture = true; }
            if (result.HasProperty(BaseMap)) { result.SetTexture(BaseMap, texture); hasTexture = true; }
            if (!hasTexture)
            {
                _host.Log.LogWarning("SignMaterials: material " + original.name +
                    " shader " + original.shader?.name + " has no texture slot; cannot override.");
                UnityEngine.Object.Destroy(result);
                return null;
            }
            if (result.HasProperty(ColorId)) result.SetColor(ColorId, Color.white);
            if (result.HasProperty(BaseColorId)) result.SetColor(BaseColorId, Color.white);
            // Ensure the original wood mask/normal/metallic maps don't dominate.
            result.DisableKeyword("_METALLICGLOSSMAP");
            result.DisableKeyword("_NORMALMAP");
            result.DisableKeyword("_EMISSION");
            if (result.HasProperty(EmissionId)) result.SetColor(EmissionId, Color.black);
            float metal, gloss;
            switch (variant)
            {
                case 'B': metal = 0.85f; gloss = 0.51f; break;
                case 'S': metal = 0.94f; gloss = 0.68f; break;
                case 'G': metal = 0.89f; gloss = 0.78f; break;
                case 'R': metal = 0.12f; gloss = 0.68f; break;
                default:  metal = 0.10f; gloss = 0.60f; break;
            }
            if (result.HasProperty(MetallicId)) result.SetFloat(MetallicId, metal);
            if (result.HasProperty(GlossinessId)) result.SetFloat(GlossinessId, gloss);
            if (result.HasProperty(SmoothnessId)) result.SetFloat(SmoothnessId, gloss);
            Materials[key] = result;
            return result;
        }

        private static Texture2D Texture(char variant)
        {
            if (variant != 'B' && variant != 'S' && variant != 'G' && variant != 'R' && variant != 'Y')
                return null;
            if (Textures.TryGetValue(variant, out Texture2D cached) && cached != null) return cached;
            try
            {
                string path = "Sirroh.Qol.Signs." + variant + ".rgba";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path))
                {
                    if (stream == null) throw new FileNotFoundException("Embedded texture: " + path);
                    byte[] bytes = new byte[256 * 256 * 4];
                    if (stream.Length != bytes.Length)
                        throw new InvalidDataException(path + " has " + stream.Length + " bytes, expected " + bytes.Length);
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int n = stream.Read(bytes, offset, bytes.Length - offset);
                        if (n == 0) throw new EndOfStreamException(path);
                        offset += n;
                    }
                    Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, true)
                    {
                        name = "Sirroh Sign " + variant,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Repeat,
                        anisoLevel = 2,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    tex.LoadRawTextureData(bytes);
                    tex.Apply(true, true);
                    Textures[variant] = tex;
                    _host.Log.LogInfo("SignMaterials: decoded embedded RGBA surface " + variant +
                        " (" + bytes.Length + " bytes).");
                    return tex;
                }
            }
            catch (Exception e)
            {
                if (!_textureErrorLogged)
                {
                    _textureErrorLogged = true;
                    _host.Log.LogWarning("SignMaterials: cannot load material textures: " + e);
                }
                return null;
            }
        }

        internal static void Shutdown()
        {
            foreach (SignMaterialHolder holder in UnityEngine.Object.FindObjectsByType<SignMaterialHolder>(FindObjectsSortMode.None))
                if (holder != null) holder.Restore();
            foreach (Material mat in Materials.Values)
                if (mat != null) UnityEngine.Object.Destroy(mat);
            Materials.Clear();
            foreach (Texture2D tex in Textures.Values)
                if (tex != null) UnityEngine.Object.Destroy(tex);
            Textures.Clear();
            _host = null;
            _textureErrorLogged = false;
        }

        internal static void Log(string message)
        {
            _host?.Log.LogInfo("SignMaterials: " + message);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class SignMaterialHolder : MonoBehaviour
    {
        private sealed class Binding
        {
            internal Renderer Renderer;
            internal Material[] Original;
        }
        private readonly List<Binding> _bindings = new List<Binding>(8);
        private char _current;
        private bool _reportedNoMesh;

        internal void Apply(Sign sign, char variant)
        {
            if (_current == variant) return;
            if (_bindings.Count == 0) Capture(sign);
            if (_bindings.Count == 0)
            {
                if (!_reportedNoMesh)
                {
                    _reportedNoMesh = true;
                    SignMaterials.Log("NO MESH on sign " + sign.name +
                        " (Sign=" + sign.transform.name + "). Retrying on next text update.");
                }
                return;
            }
            int replaced = 0;
            foreach (Binding binding in _bindings)
            {
                if (binding.Renderer == null) continue;
                Material[] newSet = (Material[])binding.Original.Clone();
                bool any = false;
                for (int i = 0; i < newSet.Length; i++)
                {
                    Material mat = SignMaterials.VariantMaterial(newSet[i], variant);
                    if (mat == null) continue;
                    newSet[i] = mat;
                    replaced++;
                    any = true;
                }
                if (any) binding.Renderer.sharedMaterials = newSet;
            }
            _current = replaced == 0 ? '\0' : variant;
            SignMaterials.Log("sign=" + sign.name + ", variant=" + variant +
                ", foundRenderers=" + _bindings.Count + ", replacedMaterials=" + replaced +
                (_current == '\0' ? " [NO REPLACEMENT]" : " [OK]"));
        }

        private void Capture(Sign sign)
        {
            // Sign is frequently a child of the prefab (e.g. interaction/canvas
            // object). The physical mesh may be a SIBLING, not Sign's descendant.
            ZNetView networkRoot = sign.GetComponentInParent<ZNetView>();
            Transform root = networkRoot != null ? networkRoot.transform :
                (sign.GetComponentInParent<Piece>() != null
                    ? sign.GetComponentInParent<Piece>().transform : sign.transform);
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in all)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    continue;
                if (renderer.GetComponent<TMP_Text>() != null) continue;
                Material[] originals = renderer.sharedMaterials;
                if (originals == null || originals.Length == 0) continue;
                _bindings.Add(new Binding { Renderer = renderer, Original = originals });
                if (_bindings.Count == 32) break;
            }
            SignMaterials.Log("scanned prefab root=" + root.name +
                " for Sign=" + sign.name + ", totalRendererComponents=" + all.Length +
                ", eligibleMeshes=" + _bindings.Count);
        }

        internal void Restore()
        {
            if (_current == '\0') return;
            foreach (Binding b in _bindings)
                if (b.Renderer != null) b.Renderer.sharedMaterials = b.Original;
            _current = '\0';
        }

        private void OnDestroy() { _bindings.Clear(); }
    }
}
