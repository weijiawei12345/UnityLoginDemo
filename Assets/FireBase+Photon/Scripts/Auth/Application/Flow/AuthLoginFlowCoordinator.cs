using System;
using System.Threading.Tasks;
using ARPG.GameFlow;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// 认证成功后的续流程协调器。
    /// 只负责“昵称档案就绪后进入 Play”，不处理表单输入、Firebase 登录或场景内网络对象。
    /// </summary>
    internal sealed class AuthLoginFlowCoordinator
    {
        private readonly UsernamePanelView _usernamePanelView;
        private readonly LoadingOverlayView _loadingOverlay;
        private readonly Action<bool> _setSubmitting;
        private readonly Action<string> _setStatus;

        public AuthLoginFlowCoordinator(
            UsernamePanelView usernamePanelView,
            LoadingOverlayView loadingOverlay,
            Action<bool> setSubmitting,
            Action<string> setStatus)
        {
            _usernamePanelView = usernamePanelView;
            _loadingOverlay = loadingOverlay;
            _setSubmitting = setSubmitting;
            _setStatus = setStatus;
        }

        public void ContinueAfterLogin()
        {
            _setSubmitting(true);
            Debug.Log("[Login] Auth success, start profile check before Play.");
            _usernamePanelView.CheckCurrentPlayerNameAsync(EnterPlaySceneAsync, OnProfileFlowAborted);
        }

        private void OnProfileFlowAborted()
        {
            _setSubmitting(false);
        }

        // UsernamePanelView 当前以 Action 回调通知昵称就绪；适配层只能使用 void，实际逻辑仍保留为 Task。
        private async void EnterPlaySceneAsync()
        {
            await EnterPlaySceneInternalAsync();
        }

        private async Task EnterPlaySceneInternalAsync()
        {
            Debug.Log("[Login] EnterPlayScene begin.");
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Show();
            }

            try
            {
                await GameSceneController.LoadPlaySceneAsync();
                Debug.Log("[Login] EnterPlayScene load requested.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Hide();
                }

                _setSubmitting(false);
                _setStatus("Failed to enter the game scene.");
            }
        }
    }
}
