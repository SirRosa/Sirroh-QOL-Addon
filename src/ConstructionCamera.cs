using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    /// <summary>
    /// Local-only free build camera. The Player object is NEVER teleported,
    /// reassigned or replaced. Equipping/hotbar changes do not end the session.
    /// </summary>
    internal static class ConstructionCamera
    {
        private static Plugin _host;
        private static Player _owner;
        private static int _ownerId;
        private static Vector3 _position;
        private static float _yaw, _pitch;
        private static bool _active;
        private static float _nextSkillRead;
        private static float _level = 1f;
        private static FieldInfo _constructionField;
        private static bool _reportedMissingSkill;
        private static HarmonyLib.AccessTools.FieldRef<Player,float> _placeDistance;
        [ThreadStatic] private static int _rangeDepth;
        [ThreadStatic] private static float _stationRange;

        private struct RangeLease
        {
            internal Player Player;
            internal float OldRange;
            internal bool Active;
        }

        internal static bool Active => _active && ValidOwner();
        internal static Vector3 Position => _position;
        internal static float CameraRange => 50f + (Mathf.Clamp(_level, 1f, 100f) - 1f) / 99f * 200f;
        internal static float PickupRange => 10f + (Mathf.Clamp(_level, 1f, 100f) - 1f) / 99f * 90f;

        internal static void Install(Plugin host)
        {
            _host = host;
            try { _placeDistance = AccessTools.FieldRefAccess<Player, float>("m_maxPlaceDistance"); }
            catch (Exception e) { host.Log.LogWarning("BuildCamera: placement range field unavailable: " + e.Message); }
            Hook("GameCamera.UpdateCamera", typeof(GameCamera), "UpdateCamera", prefix: nameof(CameraPrefix));
            Hook("Player.SetControls", typeof(Player), "SetControls", prefix: nameof(ControlsPrefix));
            Hook("Player.SetMouseLook", typeof(Player), "SetMouseLook", prefix: nameof(MousePrefix));
            Hook("Player.UpdatePlacement", typeof(Player), "UpdatePlacement", prefix: nameof(PlacementPrefix), finalizer: nameof(RangeFinalizer));
            Hook("Player.UpdatePlacementGhost", typeof(Player), "UpdatePlacementGhost", prefix: nameof(GhostPrefix), finalizer: nameof(RangeFinalizer));
            Hook("CraftingStation.GetStationBuildRange", typeof(CraftingStation), "GetStationBuildRange", postfix: nameof(StationRangePostfix));
            Hook("Player.PieceRayTest", typeof(Player), "PieceRayTest", postfix: nameof(PieceRayPostfix));
            Hook("Player.SetLocalPlayer", typeof(Player), "SetLocalPlayer", postfix: nameof(LocalPlayerPostfix));
            _host.Log.LogInfo("BuildCamera: installed; B toggles independently of equipped tool.");
        }

        private static void Hook(string label, Type type, string name, string prefix = null, string postfix = null, string finalizer = null)
        {
            MethodInfo method = AccessTools.Method(type, name);
            _host.Patch("BuildCamera." + label, method,
                prefix: prefix == null ? null : AccessTools.Method(typeof(ConstructionCamera), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(ConstructionCamera), postfix),
                finalizer: finalizer == null ? null : AccessTools.Method(typeof(ConstructionCamera), finalizer));
        }

        internal static void Tick()
        {
            if (_host == null) return;
            if (_active && (!Application.isFocused || !ValidOwner())) { Stop(); return; }
            Player player = Player.m_localPlayer;
            if (player == null || !Application.isFocused || Console.IsVisible()) return;
            if (Input.GetKeyDown(KeyCode.B) && (Hud.instance == null || !Hud.IsPieceSelectionVisible()))
            {
                if (_active) Stop();
                else Start(player);
            }
            if (!_active) return;
            RefreshSkill(player);
            RemoteCameraPickup.Tick(player, _position, PickupRange);
        }

        private static bool ValidOwner()
        {
            return _owner != null && Player.m_localPlayer == _owner &&
                _owner.GetInstanceID() == _ownerId && !_owner.IsDead() && !_owner.IsTeleporting();
        }

        private static void Start(Player player)
        {
            if (GameCamera.instance == null || player.IsDead() || player.IsTeleporting()) return;
            // No hammer/hoe/shovel/rake check: camera stays active during swaps.
            _owner = player;
            _ownerId = player.GetInstanceID();
            _active = true;
            _position = GameCamera.instance.transform.position;
            Vector3 angles = GameCamera.instance.transform.rotation.eulerAngles;
            _yaw = angles.y;
            _pitch = Mathf.DeltaAngle(0f, angles.x);
            RefreshSkill(player, true);
            _position = ClampCamera(_position);
            _host.Log.LogInfo("BuildCamera ON; Konstrukcja=" + _level.ToString("0.0") +
                " camera=" + CameraRange.ToString("0.0") + "m pickup=" + PickupRange.ToString("0.0") + "m");
        }

        internal static void Stop()
        {
            if (_active) _host?.Log.LogInfo("BuildCamera OFF");
            _active = false;
            _owner = null;
            _ownerId = 0;
            _rangeDepth = 0;
            _stationRange = 0f;
            RemoteCameraPickup.Reset();
        }

        internal static void Shutdown()
        {
            Stop();
            _host = null;
        }

        private static Vector3 ClampCamera(Vector3 point)
        {
            if (_owner == null) return point;
            Vector3 root = _owner.transform.position;
            Vector3 delta = point - root;
            float max = CameraRange;
            return delta.sqrMagnitude > max * max ? root + delta.normalized * max : point;
        }

        private static void RefreshSkill(Player player, bool force = false)
        {
            if (!force && Time.time < _nextSkillRead) return;
            _nextSkillRead = Time.time + 0.5f;
            if (_constructionField == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType("Sirro.PracaFizyczna.AdditionalSkills", false);
                    if (type == null) continue;
                    _constructionField = AccessTools.Field(type, "Construction");
                    if (_constructionField != null) break;
                }
            }
            if (_constructionField == null)
            {
                if (!_reportedMissingSkill)
                {
                    _reportedMissingSkill = true;
                    _host?.Log.LogWarning("BuildCamera: Konstrukcja skill field not available yet; using level 1.");
                }
                _level = 1f;
                return;
            }
            try
            {
                object value = _constructionField.GetValue(null);
                var skill = (Skills.SkillType)value;
                _level = Mathf.Clamp(player.GetSkills().GetSkillLevel(skill), 1f, 100f);
            }
            catch (Exception e)
            {
                _level = 1f;
                if (!_reportedMissingSkill)
                {
                    _reportedMissingSkill = true;
                    _host?.Log.LogWarning("BuildCamera: skill read failed, fallback to 1: " + e.Message);
                }
            }
        }

        private static bool CameraPrefix(GameCamera __instance, float dt)
        {
            if (!Active || __instance == null) return true;
            if (!Console.IsVisible() && (Hud.instance == null || !Hud.IsPieceSelectionVisible()))
            {
                float time = Mathf.Clamp(dt, 0f, 0.1f);
                float yawDelta = Input.GetAxis("Mouse X") * PlayerController.m_mouseSens;
                float pitchDelta = Input.GetAxis("Mouse Y") * PlayerController.m_mouseSens;
                if (PlayerController.m_invertMouse) pitchDelta = -pitchDelta;
                _yaw += yawDelta + ZInput.GetJoyRightStickX() * 110f * time;
                _pitch = Mathf.Clamp(_pitch - pitchDelta + ZInput.GetJoyRightStickY() * 110f * time, -89f, 89f);
                Vector3 input = new Vector3(
                    Axis("Right", "Left") + ZInput.GetJoyLeftStickX(),
                    Axis("Jump", "Crouch"),
                    Axis("Forward", "Backward") - ZInput.GetJoyLeftStickY());
                if (input.sqrMagnitude > 1f) input.Normalize();
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 velocity = rotation * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y;
                float speed = ZInput.GetButton("Run") ? 30f : 10f;
                _position = ClampCamera(_position + velocity * speed * time);
                float ground = 0f;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(_position, ref ground) && _position.y < ground + 0.2f)
                {
                    _position.y = ground + 0.2f;
                    _position = ClampCamera(_position);
                }
            }
            __instance.transform.SetPositionAndRotation(_position, Quaternion.Euler(_pitch, _yaw, 0f));
            return false;
        }

        private static float Axis(string positive, string negative)
            => (ZInput.GetButton(positive) ? 1f : 0f) - (ZInput.GetButton(negative) ? 1f : 0f);

        private static void ControlsPrefix(Player __instance,
            ref Vector3 movedir, ref bool attack, ref bool attackHold,
            ref bool secondaryAttack, ref bool secondaryAttackHold,
            ref bool block, ref bool blockHold, ref bool jump, ref bool crouch,
            ref bool run, ref bool autoRun, ref bool dodge)
        {
            if (__instance != _owner || !Active) return;
            movedir = Vector3.zero;
            attack = attackHold = secondaryAttack = secondaryAttackHold = false;
            block = blockHold = jump = crouch = run = autoRun = dodge = false;
        }

        private static void MousePrefix(Player __instance, ref Vector2 mouseLook)
        {
            if (__instance == _owner && Active) mouseLook = Vector2.zero;
        }

        private static RangeLease BeginRange(Player player)
        {
            RangeLease lease = default;
            if (player != _owner || !Active || _placeDistance == null) return lease;
            lease.Player = player;
            lease.OldRange = _placeDistance(player);
            lease.Active = true;
            _placeDistance(player) = CameraRange;
            _rangeDepth++;
            _stationRange = CameraRange;
            return lease;
        }

        private static void PlacementPrefix(Player __instance, out RangeLease __state)
        {
            __state = BeginRange(__instance);
        }

        private static void GhostPrefix(Player __instance, out RangeLease __state)
        {
            __state = BeginRange(__instance);
        }

        private static Exception RangeFinalizer(Exception __exception, RangeLease __state)
        {
            if (__state.Active)
            {
                if (__state.Player != null && _placeDistance != null)
                    _placeDistance(__state.Player) = __state.OldRange;
                _rangeDepth = Math.Max(0, _rangeDepth - 1);
                if (_rangeDepth == 0) _stationRange = 0f;
            }
            return __exception;
        }

        private static void StationRangePostfix(ref float __result)
        {
            if (_rangeDepth > 0 && __result < _stationRange) __result = _stationRange;
        }

        private static void PieceRayPostfix(Player __instance, Vector3 point, ref bool __result)
        {
            if (!__result || __instance != _owner || !Active) return;
            Vector3 fromPlayer = point - _owner.transform.position;
            float allowed = CameraRange;
            if (fromPlayer.sqrMagnitude > allowed * allowed) __result = false;
        }

        private static void LocalPlayerPostfix(Player __instance)
        {
            if (_active && __instance != _owner) Stop();
        }
    }
}
