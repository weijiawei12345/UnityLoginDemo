namespace ARPG.Networking.Lobby
{
    public static class FusionRoomRules
    {
        public const int MaxRoomNameLength = 24;
        public const int MaxPlayers = 4;

        public static string NormalizeRoomName(string roomName)
        {
            return roomName == null ? string.Empty : roomName.Trim();
        }

        public static bool TryValidateRoomName(string roomName, out string error)
        {
            string normalized = NormalizeRoomName(roomName);
            if (normalized.Length == 0)
            {
                error = "Enter a room name.";
                return false;
            }

            if (normalized.Length > MaxRoomNameLength)
            {
                error = $"Room names can contain up to {MaxRoomNameLength} characters.";
                return false;
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                if (!char.IsLetterOrDigit(character)
                    && character != '-'
                    && character != '_'
                    && character != '.')
                {
                    error = "Use letters, numbers, dots, dashes, or underscores only.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
