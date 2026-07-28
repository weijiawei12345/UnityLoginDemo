using Fusion;
using UnityEngine;

namespace ARPG.Player.Camera
{
    /// <summary>Binds the scene camera only to the player owned by this client's Input Authority.</summary>
    public sealed class PlayerCameraBinder : NetworkBehaviour
    {
        private global::FirstPersonCamera _firstPersonCamera;
        private global::ThirdPersonCamera _thirdPersonCamera;

        public override void Spawned()
        {
            if (!HasInputAuthority)
            {
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError($"[PlayerCameraBinder] Main Camera was not found for {name}.", this);
                return;
            }

            _firstPersonCamera = mainCamera.GetComponent<global::FirstPersonCamera>();
            _thirdPersonCamera = mainCamera.GetComponent<global::ThirdPersonCamera>();
            _firstPersonCamera?.Bind(transform);
            _thirdPersonCamera?.Bind(transform);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _firstPersonCamera?.Unbind(transform);
            _thirdPersonCamera?.Unbind(transform);
            _firstPersonCamera = null;
            _thirdPersonCamera = null;
        }
    }
}
