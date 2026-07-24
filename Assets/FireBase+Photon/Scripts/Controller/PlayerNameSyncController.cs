using System.Threading.Tasks;
using ARPG.Auth;

namespace ARPG.Player
{
    /// <summary>
    /// 昵称业务编排：先持久化到 Firestore，再推送到本地 NetworkPlayer 供房间同步。
    /// </summary>
    public sealed class PlayerNameSyncController
    {
        private readonly UserNameController _userNameController;

        public PlayerNameSyncController(UserNameController userNameController = null)
        {
            _userNameController = userNameController ?? new UserNameController();
        }

        public Task<UserNameResult> LoadCurrentPlayerNameAsync()
        {
            return _userNameController.LoadCurrentPlayerNameAsync();
        }

        /// <summary>
        /// 游戏内改名：Firestore 成功后再写 Fusion 网络属性。
        /// </summary>
        public async Task<UserNameResult> RenameAndSyncAsync(string inputName)
        {
            UserNameResult result = await _userNameController.SaveCurrentPlayerNameAsync(inputName);
            if (!result.Success)
            {
                return result;
            }

            string displayName = PlayerDisplayNameData.Truncate(result.Name);
            if (NetworkPlayer.Local != null)
            {
                NetworkPlayer.Local.TrySetDisplayName(displayName);
            }

            return UserNameResult.Found(displayName);
        }
    }
}
