namespace ARPG.Player.Movement.Core
{
    /// <summary>Immutable values used by deterministic player movement rules.</summary>
    public readonly struct PlayerMovementConfigData
    {
        public PlayerMovementConfigData(
            float runSpeed,
            float walkSpeedMultiplier,
            float gravity,
            float groundStickVelocity,
            float jumpSpeed)
        {
            RunSpeed = runSpeed;
            WalkSpeedMultiplier = walkSpeedMultiplier;
            Gravity = gravity;
            GroundStickVelocity = groundStickVelocity;
            JumpSpeed = jumpSpeed;
        }

        public float RunSpeed { get; }
        public float WalkSpeedMultiplier { get; }
        public float Gravity { get; }
        public float GroundStickVelocity { get; }
        public float JumpSpeed { get; }
    }
}
