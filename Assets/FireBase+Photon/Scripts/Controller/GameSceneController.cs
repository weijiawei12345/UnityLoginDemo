using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARPG.GameFlow
{
    /// <summary>
    /// 场景切换 Controller：负责登录后进入 Play 等流程，不处理 UI 与存档。
    /// </summary>
    public static class GameSceneController
    {
        public static void LoadPlayScene()
        {
            LoadScene(GameSceneIds.Play);
        }

        public static void LoadLoginMenuScene()
        {
            LoadScene(GameSceneIds.LoginMenu);
        }

        public static async Task LoadPlaySceneAsync()
        {
            await LoadSceneAsync(GameSceneIds.Play);
        }

        private static void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GameScene] Scene name is empty.");
                return;
            }

            Debug.Log($"[GameScene] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private static async Task LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GameScene] Scene name is empty.");
                return;
            }

            Debug.Log($"[GameScene] Loading scene async: {sceneName}");
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[GameScene] Failed to start loading scene: {sceneName}");
                return;
            }

            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
    }
}
