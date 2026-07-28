namespace ARPG.Player.Input
{
    /// <summary>Supplies the current local input snapshot to a Fusion input callback.</summary>
    public interface IPlayerInputSource
    {
        PlayerInputFrame Capture();
        void ConsumeTickButtons();
    }
}
