using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play 场景改名 View：绑定 RenameBtn，复用 UsernamePanel 唤出改名流程。
/// </summary>
public sealed class PlayRenameView : MonoBehaviour
{
    [SerializeField] private Button renameButton;
    [SerializeField] private Transform uiRoot;

    private UsernamePanelView _usernamePanelView;

    private void Awake()
    {
        if (uiRoot == null)
        {
            uiRoot = transform;
        }

        if (renameButton == null)
        {
            Transform btn = transform.Find("RenameBtn");
            if (btn != null)
            {
                renameButton = btn.GetComponent<Button>();
            }
        }

        _usernamePanelView = UsernamePanelView.GetOrCreate(uiRoot);

        if (renameButton != null)
        {
            renameButton.onClick.AddListener(OnRenameClicked);
        }
        else
        {
            Debug.LogWarning("[PlayRenameView] RenameBtn was not found on Canvas.");
        }
    }

    private void OnDestroy()
    {
        if (renameButton != null)
        {
            renameButton.onClick.RemoveListener(OnRenameClicked);
        }
    }

    private void OnRenameClicked()
    {
        if (_usernamePanelView == null)
        {
            _usernamePanelView = UsernamePanelView.GetOrCreate(uiRoot);
        }

        _usernamePanelView?.ShowForRename();
    }
}
