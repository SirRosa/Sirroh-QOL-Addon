using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Sirroh.Qol
{
    [BepInPlugin(Guid, "Sirroh QOL Addon", Version)]
    [BepInDependency("sirro.valheim.pracafizyczna", "0.22.65")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sirro.valheim.qoladdon";
        public const string Version = "0.1.16";
        internal static Plugin Instance;
        internal ManualLogSource Log => Logger;
        private Harmony _harmony;

        internal ConfigEntry<bool> AutoRepairEnabled;
        internal ConfigEntry<bool> SafeStaminaEnabled;
        internal ConfigEntry<float> SafeRadius;
        internal ConfigEntry<string> IgnoredCreatures;
        internal ConfigEntry<bool> FoodFreezeEnabled;
        internal ConfigEntry<bool> FreezeWhileResting;
        internal ConfigEntry<bool> FreezeNearFire;
        internal ConfigEntry<float> FireRadius;
        internal ConfigEntry<bool> GlowSignsEnabled;
        internal ConfigEntry<float> SignEmission;
        internal ConfigEntry<float> SignGlowStrength;
        internal ConfigEntry<float> SignGlowRadius;
        internal ConfigEntry<bool> NoSlopeSlideEnabled;

        private void Awake()
        {
            Instance = this;
            AutoRepairEnabled = Config.Bind("01 Auto Repair", "Enabled", true, "Automatically repair items after opening a matching crafting station.");
            SafeStaminaEnabled = Config.Bind("02 Safe Stamina", "Enabled", true, "Do not spend stamina when no hostile creature is nearby (local player only).");
            SafeRadius = Config.Bind("02 Safe Stamina", "HostileRadius", 25f,
                new ConfigDescription("Hostile detection radius in meters.", new AcceptableValueRange<float>(5f, 100f)));
            IgnoredCreatures = Config.Bind("02 Safe Stamina", "IgnoredCreatures", "",
                "Comma-separated prefab names to ignore during nearby-hostile checks.");
            FoodFreezeEnabled = Config.Bind("03 Food Freeze", "Enabled", true, "Preserve food remaining duration while resting or near a lit fire.");
            FreezeWhileResting = Config.Bind("03 Food Freeze", "WhileResting", true, "Freeze while the Resting status is active (not the long-lasting Rested buff).");
            FreezeNearFire = Config.Bind("03 Food Freeze", "NearLitFire", true, "Freeze when a lit fireplace is nearby.");
            FireRadius = Config.Bind("03 Food Freeze", "FireRadius", 12f,
                new ConfigDescription("Distance to check for burning fireplaces.", new AcceptableValueRange<float>(2f, 35f)));
            GlowSignsEnabled = Config.Bind("04 Glowing Signs", "Enabled", true, "Sign-only emissive HDR text with soft, color-matched glow.");
            SignEmission = Config.Bind("04 Glowing Signs", "EmissionIntensity", 1.75f,
                new ConfigDescription("HDR brightness of sign glyphs (bloom depends on graphics settings).", new AcceptableValueRange<float>(1f, 3f)));
            SignGlowStrength = Config.Bind("04 Glowing Signs", "GlowStrength", 0.68f,
                new ConfigDescription("Soft halo strength, 0 disables the halo.", new AcceptableValueRange<float>(0f, 1f)));
            SignGlowRadius = Config.Bind("04 Glowing Signs", "GlowRadius", 0.13f,
                new ConfigDescription("Soft halo spread without a hard outline.", new AcceptableValueRange<float>(0f, 0.35f)));
            SignEmission.SettingChanged += (_, __) => GlowingSigns.RefreshMaterials();
            SignGlowStrength.SettingChanged += (_, __) => GlowingSigns.RefreshMaterials();
            SignGlowRadius.SettingChanged += (_, __) => GlowingSigns.RefreshMaterials();
            NoSlopeSlideEnabled = Config.Bind("05 Slopes", "Enabled", true, "Prevent sliding on steep ground; correct downhill gravity only while idle.");

            _harmony = new Harmony(Guid);
            AutoRepair.Install(this);
            SafeStamina.Install(this);
            FoodFreeze.Install(this);
            GlowingSigns.Install(this);
            SlopeSafety.Install(this);
            SwimmingEquipment.Install(this);
            ConstructionCamera.Install(this);
            QolRecipeListSafety.Install(this);
            QolFavorites.Install(this);
            QolNearbyInventory.Install(this);
            QolTrashReclaim.Install(this);
            QolStoreSort.Install(this);
            Logger.LogInfo("Sirroh QOL Addon " + Version + " initialized (Sirroh Overhaul >=0.22.65 required).");
        }

        internal void Patch(string module, MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, int priority = Priority.Normal,
            MethodInfo transpiler = null, MethodInfo finalizer = null)
        {
            if (original == null)
            {
                Logger.LogWarning(module + ": target API not available; module hook disabled.");
                return;
            }
            try
            {
                HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(prefix) { priority = priority };
                HarmonyMethod post = postfix == null ? null : new HarmonyMethod(postfix) { priority = priority };
                HarmonyMethod trans = transpiler == null ? null : new HarmonyMethod(transpiler);
                HarmonyMethod fin = finalizer == null ? null : new HarmonyMethod(finalizer);
                _harmony.Patch(original, pre, post, trans, fin, null);
            }
            catch (Exception e) { Logger.LogWarning(module + ": hook unavailable: " + e); }
        }

        private void Update()
        {
            ConstructionCamera.Tick();
            QolNearbyInventory.Tick();
            QolStoreSort.Tick();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            try { QolStoreSort.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            try { QolNearbyInventory.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            try { QolTrashReclaim.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            try { QolFavorites.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            try { ConstructionCamera.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            try { _harmony?.UnpatchSelf(); } catch (Exception e) { Logger.LogWarning(e); }
            try { GlowingSigns.Shutdown(); } catch (Exception e) { Logger.LogWarning(e); }
            Instance = null;
        }
    }
}
