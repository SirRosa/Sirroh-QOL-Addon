using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class SlopeSafety
    {
        private static Plugin _host;

        internal static void Install(Plugin host)
        {
            _host = host;
            host.Patch("SlopeSafety.ApplySlide", AccessTools.Method(typeof(Character), "ApplySlide"),
                prefix: AccessTools.Method(typeof(SlopeSafety), nameof(BeforeSlide)), priority: Priority.First);
            host.Patch("SlopeSafety.UpdateMotion", AccessTools.Method(typeof(Character), "UpdateMotion"),
                postfix: AccessTools.Method(typeof(SlopeSafety), nameof(AfterMotion)));
        }

        private static bool Eligible(Character character)
        {
            return _host != null && _host.NoSlopeSlideEnabled.Value &&
                character != null && character == Player.m_localPlayer &&
                !character.IsDead() && !character.IsSwimming() && !character.IsAttached() &&
                !character.IsRiding() && character.GetStandingOnShip() == null;
        }

        private static bool BeforeSlide(Character __instance)
        {
            // On a very steep slope IsOnGround() may be false WHILE ApplySlide runs.
            // Therefore previous code never reached the cancellation.
            if (!Eligible(__instance)) return true;
            __instance.m_slippage = 0f;
            __instance.m_sliding = false;
            return false;
        }

        private static void AfterMotion(Character __instance)
        {
            if (!Eligible(__instance) || __instance.GetMoveDir().sqrMagnitude > 0.0001f)
                return;
            // Cancel the gravitational acceleration projected along the slope
            // only for an idle player with very recent ground contact.
            if (__instance.m_lastGroundCollider == null || __instance.m_lastGroundTouch > 0.15f)
                return;
            Vector3 normal = __instance.m_lastGroundNormal;
            if (normal.sqrMagnitude < 0.5f || normal.y <= 0.02f || normal.y >= 0.99f)
                return;
            Rigidbody body = __instance.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return;
            Vector3 slopeAcceleration = Vector3.ProjectOnPlane(Physics.gravity, normal.normalized);
            body.AddForce(-slopeAcceleration, ForceMode.Acceleration);
        }
    }
}
