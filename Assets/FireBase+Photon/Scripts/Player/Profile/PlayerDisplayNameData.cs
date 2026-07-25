using ARPG.Auth;

namespace ARPG.Player
{
    /// <summary>
    /// 玩家展示名数据源：从本机会话读取，不直接访问 Firestore / Fusion。
    /// </summary>
    public static class PlayerDisplayNameData
    {
        public const string FallbackName = "Guest";
        public const int MaxNetworkNameLength = 32;

        public static string GetLocalDisplayName()
        {
            if (UserSession.IsLoggedIn
                && UserSession.Current != null
                && !string.IsNullOrWhiteSpace(UserSession.Current.Name))
            {
                return Truncate(UserSession.Current.Name.Trim());
            }

            return FallbackName;
        }

        public static string Truncate(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return FallbackName;
            }

            return name.Length <= MaxNetworkNameLength
                ? name
                : name.Substring(0, MaxNetworkNameLength);
        }
    }
}
