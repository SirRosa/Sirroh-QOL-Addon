using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    internal static class SlopeSafety
    {
        private static Plugin _host;
        // Private Character fields in Valheim 1.0: cache fast typed Harmony field refs.
        private static readonly AccessTools.FieldRef<Character, float> Slippage =
            AccessTools.FieldRefAccess<Character, float>("m_slippage");
        private static readonly AccessTools.FieldRef<Character, bool> Sliding =
            AccessTools.FieldRefAccess<Character, bool>("m_sliding");
        private static readonly AccessTools.FieldRef<Character, Collider> GroundCollider =
            AccessTools.FieldRefAccess<Character, Collider>("m_lastGroundCollider");
        private static readonly AccessTools.FieldRef<Character, float> LastGroundTouch =
            AccessTools.FieldRefAccess<Character, float>("m_lastGroundTouch");
        private static readonly AccessTools.FieldRef<Character, Vector3> GroundNormal =
            AccessTools.FieldRefAccess<Character, Vector3>("m_lastGroundNormal");

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
            Slippage(__instance) = 0f;
            Sliding(__instance) = false;
            return false;
        }

        private static void AfterMotion(Character __instance)
        {
            if (!Eligible(__instance) || __instance.GetMoveDir().sqrMagnitude > 0.0001f)
                return;
            // Cancel the gravitational acceleration projected along the slope
            // only for an idle player with very recent ground contact.
            if (GroundCollider(__instance) == null || LastGroundTouch(__instance) > 0.15f)
                return;
            Vector3 normal = GroundNormal(__instance);
            if (normal.sqrMagnitude < 0.5f || normal.y <= 0.02f || normal.y >= 0.99f)
                return;
            Rigidbody body = __instance.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return;
            Vector3 slopeAcceleration = Vector3.ProjectOnPlane(Physics.gravity, normal.normalized);
            body.AddForce(-slopeAcceleration, ForceMode.Acceleration);
        }
    }
}
