using ARPG.Player.Movement.Core;
using UnityEngine;

namespace ARPG.Player.Movement
{
    /// <summary>Inspector-facing movement settings for a network player.</summary>
    public sealed class PlayerMovementConfig : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _runSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float _walkSpeedMultiplier = 0.5f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _groundStickVelocity = -1f;
        [SerializeField, Min(0f)] private float _jumpSpeed = 5f;

        /// <summary>Maximum horizontal movement speed.</summary>
        public float RunSpeed => _runSpeed;

        /// <summary>Creates the immutable values consumed by movement rules.</summary>
        public PlayerMovementConfigData ToData()
        {
            return new PlayerMovementConfigData(
                _runSpeed,
                _walkSpeedMultiplier,
                _gravity,
                _groundStickVelocity,
                _jumpSpeed);
        }

        private void OnValidate()
        {
            _runSpeed = Mathf.Max(0f, _runSpeed);
            _walkSpeedMultiplier = Mathf.Clamp01(_walkSpeedMultiplier);
            _gravity = Mathf.Min(0f, _gravity);
            _groundStickVelocity = Mathf.Min(0f, _groundStickVelocity);
            _jumpSpeed = Mathf.Max(0f, _jumpSpeed);
        }
    }
}
