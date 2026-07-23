using System;
using System.Threading.Tasks;

namespace ARPG.Auth
{
    /// <summary>
    /// 昵称业务控制器：负责输入校验、调用数据访问层和更新当前会话。
    /// </summary>
    public sealed class UserNameController
    {
        private const int MaxNameLength = 16;

        private readonly FirestoreUserProfileRepository _profileRepository;

        public UserNameController(FirestoreUserProfileRepository profileRepository = null)
        {
            _profileRepository = profileRepository ?? new FirestoreUserProfileRepository();
        }

        public async Task<UserNameResult> LoadCurrentPlayerNameAsync()
        {
            if (!UserSession.IsLoggedIn)
            {
                return UserNameResult.Fail("Please log in before setting a player name.");
            }

            UserNameResult result = await _profileRepository.LoadNameAsync(UserSession.Current.Uid);
            if (result.Success && result.HasName)
            {
                UserSession.Current.Name = result.Name;
            }

            return result;
        }

        public async Task<UserNameResult> SaveCurrentPlayerNameAsync(string inputName)
        {
            if (!UserSession.IsLoggedIn)
            {
                return UserNameResult.Fail("Please log in before setting a player name.");
            }

            string playerName = inputName == null ? string.Empty : inputName.Trim();
            if (string.IsNullOrEmpty(playerName))
            {
                return UserNameResult.Fail("Please enter a player name.");
            }

            if (playerName.Length > MaxNameLength)
            {
                return UserNameResult.Fail("Player name must be 16 characters or fewer.");
            }

            UserNameResult result = await _profileRepository.SaveNameAsync(UserSession.Current.Uid, playerName);
            if (result.Success)
            {
                UserSession.Current.Name = result.Name;
            }

            return result;
        }
    }
}
