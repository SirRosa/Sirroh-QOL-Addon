using HarmonyLib;

namespace Sirroh.Qol
{
    internal static class SlopeSafety
    {
        private static Plugin _host;

        internal static void Install(Plugin host)
        {
            _host = host;
            host.Patch("SlopeSafety", AccessTools.Method(typeof(Character), "ApplySlide"),
                prefix: AccessTools.Method(typeof(SlopeSafety), nameof(BeforeSlide)));
        }

        private static bool BeforeSlide(Character __instance)
        {
            if (_host == null || !_host.NoSlopeSlideEnabled.Value ||
                __instance == null || __instance != Player.m_localPlayer ||
                __instance.IsDead() || !__instance.IsOnGround() ||
                __instance.IsSwimming() || __instance.IsAttached() ||
                __instance.IsRiding() || __instance.GetStandingOnShip() != null)
                return true;
            // Cancel only vanilla ground sliding. Do not inject forces or change
            // rigidbody velocity: no physics accumulation and no remote-player edits.
            return false;
        }
    }
}
