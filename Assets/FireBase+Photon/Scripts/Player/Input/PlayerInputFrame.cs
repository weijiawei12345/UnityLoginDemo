using Fusion;
using UnityEngine;

namespace ARPG.Player.Input
{
    /// <summary>Discrete actions carried by a player input frame.</summary>
    public enum PlayerInputButton
    {
        Jump = 0,
        Sprint = 1
    }

    /// <summary>Device intent submitted to Fusion for one simulation tick.</summary>
    public struct PlayerInputFrame : INetworkInput
    {
        public Vector2 Move;
        public Vector3 CameraForward;
        public NetworkButtons Buttons;
    }
}
