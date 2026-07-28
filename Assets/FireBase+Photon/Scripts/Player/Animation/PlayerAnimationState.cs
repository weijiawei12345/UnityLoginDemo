namespace ARPG.Player.Animation
{
    /// <summary>Movement results consumed by the player animation presenter.</summary>
    public readonly struct PlayerAnimationState
    {
        public PlayerAnimationState(float normalizedSpeed, bool grounded, bool freeFall, bool jumping)
        {
            NormalizedSpeed = normalizedSpeed;
            Grounded = grounded;
            FreeFall = freeFall;
            Jumping = jumping;
        }

        public float NormalizedSpeed { get; }
        public bool Grounded { get; }
        public bool FreeFall { get; }
        public bool Jumping { get; }

        /// <summary>Creates presentation values from authoritative movement results.</summary>
        public static PlayerAnimationState FromMovement(
            float normalizedSpeed,
            bool grounded,
            float verticalSpeed,
            bool jumpInProgress)
        {
            bool freeFall = !grounded && verticalSpeed < 0f;
            bool jumpStart = jumpInProgress && verticalSpeed > 0f;
            return new PlayerAnimationState(normalizedSpeed, grounded, freeFall, jumpStart);
        }
    }
}
