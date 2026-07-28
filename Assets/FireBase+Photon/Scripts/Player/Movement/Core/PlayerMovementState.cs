using UnityEngine;

namespace ARPG.Player.Movement.Core
{
    /// <summary>Runtime values carried between player movement ticks.</summary>
    public struct PlayerMovementState
    {
        public Vector3 Velocity;
        public bool IsGrounded;
        public bool JumpInProgress;
        public bool WasAirborne;
        public bool JumpedThisTick;
        public bool LandedThisTick;
        public int JumpStartedTick;
    }
}
