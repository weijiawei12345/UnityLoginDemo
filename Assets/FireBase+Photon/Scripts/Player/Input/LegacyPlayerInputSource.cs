using Fusion;
using UnityEngine;

namespace ARPG.Player.Input
{
    /// <summary>Adapts Unity's Legacy Input Manager to the player network input contract.</summary>
    public sealed class LegacyPlayerInputSource : MonoBehaviour, IPlayerInputSource
    {
        private Vector2 _move;
        private Vector3 _cameraForward = Vector3.forward;
        private NetworkButtons _buttons;
        private bool _isSprinting;

        private void Update()
        {
            _move = Vector2.ClampMagnitude(
                new Vector2(UnityEngine.Input.GetAxis("Horizontal"), UnityEngine.Input.GetAxis("Vertical")),
                1f);

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    _cameraForward = forward.normalized;
                }
            }

            if (_move.sqrMagnitude <= 0.0001f)
            {
                _isSprinting = false;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftShift)
                     || UnityEngine.Input.GetKeyDown(KeyCode.RightShift))
            {
                _isSprinting = true;
            }

            if (UnityEngine.Input.GetButtonDown("Jump"))
            {
                _buttons.Set(PlayerInputButton.Jump, true);
            }

            _buttons.Set(PlayerInputButton.Sprint, _isSprinting);
        }

        public PlayerInputFrame Capture()
        {
            return new PlayerInputFrame
            {
                Move = _move,
                CameraForward = _cameraForward,
                Buttons = _buttons
            };
        }

        public void ConsumeTickButtons()
        {
            // Jump is a render-frame edge. Keep it latched until Fusion accepts an input sample.
            _buttons.Set(PlayerInputButton.Jump, false);
        }
    }
}
