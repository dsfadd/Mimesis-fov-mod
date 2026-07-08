using HarmonyLib;
using Unity.Cinemachine;
using UnityEngine;

namespace MimesisFovMod
{
    /// <summary>
    /// Applies the configured field of view to the player's Cinemachine camera
    /// (<c>CameraManager.playerCamera</c>) and keeps it applied, without ever fighting the
    /// game's own FOV animations (zoom-to-target, spectate, death cam).
    ///
    /// In this game the player view is a <see cref="CinemachineCamera"/> whose
    /// <c>Lens.FieldOfView</c> drives <c>Camera.main</c> through the CinemachineBrain, so the
    /// lens is the correct and only lever for the base FOV.
    /// </summary>
    [HarmonyPatch]
    internal static class CameraFov
    {
        // CameraManager.playerCamera is private; grab a fast, allocation-free accessor once.
        private static readonly AccessTools.FieldRef<CameraManager, CinemachineCamera> PlayerCameraRef =
            AccessTools.FieldRefAccess<CameraManager, CinemachineCamera>("playerCamera");

        private static CameraManager _manager;

        /// <summary>
        /// The game (re)creates / re-targets the player camera on spawn and respawn.
        /// Re-apply our FOV right after it does.
        /// </summary>
        [HarmonyPatch(typeof(CameraManager), "SetupPlayerCamera")]
        [HarmonyPostfix]
        private static void SetupPlayerCamera_Postfix(CameraManager __instance)
        {
            _manager = __instance;
            Apply();
        }

        /// <summary>Force the configured FOV onto the player camera right now (config change / camera setup).</summary>
        public static void Apply()
        {
            CameraManager mgr = GetManager();
            if (mgr == null) return;
            if (mgr.IsZoomToTargetActive) return; // the zoom animation owns the FOV while active

            CinemachineCamera cam = PlayerCameraRef(mgr);
            if (cam == null) return;
            SetFieldOfView(cam, FovModPlugin.Fov.Value);
        }

        /// <summary>
        /// Cheap per-frame re-assert so respawns and camera blends can't quietly revert the FOV.
        /// Only touches the first-person player camera and never the zoom animation.
        /// </summary>
        public static void Tick()
        {
            CameraManager mgr = GetManager();
            if (mgr == null) return;
            if (mgr.Mode != CameraManager.CameraMode.Normal) return; // not while spectating / possessing
            if (mgr.IsZoomToTargetActive) return;                    // not during the zoom-to-target animation

            CinemachineCamera cam = PlayerCameraRef(mgr);
            if (cam == null) return;

            float target = FovModPlugin.Fov.Value;
            if (Mathf.Abs(cam.Lens.FieldOfView - target) > 0.01f)
                SetFieldOfView(cam, target);
        }

        private static CameraManager GetManager()
        {
            if (_manager == null)
                _manager = Object.FindFirstObjectByType<CameraManager>();
            return _manager;
        }

        private static void SetFieldOfView(CinemachineCamera cam, float fov)
        {
            // LensSettings is a struct field on the camera: read-modify-write the whole struct.
            LensSettings lens = cam.Lens;
            lens.FieldOfView = fov;
            cam.Lens = lens;
        }
    }
}
