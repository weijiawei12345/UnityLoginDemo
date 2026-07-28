namespace ARPG.Networking.Lobby
{
    public sealed class LobbyRoomSnapshot
    {
        public LobbyRoomSnapshot(
            string name,
            int playerCount,
            int maxPlayers,
            bool isOpen,
            bool isVisible,
            string map,
            string difficulty,
            string phase,
            string build)
        {
            Name = name ?? string.Empty;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            IsOpen = isOpen;
            IsVisible = isVisible;
            Map = map ?? string.Empty;
            Difficulty = difficulty ?? string.Empty;
            Phase = phase ?? string.Empty;
            Build = build ?? string.Empty;
        }

        public string Name { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }
        public bool IsOpen { get; }
        public bool IsVisible { get; }
        public string Map { get; }
        public string Difficulty { get; }
        public string Phase { get; }
        public string Build { get; }
        public bool CanJoin => IsVisible && IsOpen && PlayerCount < MaxPlayers;
    }
}
