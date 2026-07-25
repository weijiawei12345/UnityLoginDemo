using ARPG.Player;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// View + 网络状态：同步玩家展示名并刷新世界空间 TMP（参考教程 NetworkPlayer）。
/// Shared Mode 下由 State Authority 写入。
/// </summary>
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("World-space label")]
    [SerializeField] private TMP_Text nameLabel;

    [Networked, OnChangedRender(nameof(ApplyName))]
    public NetworkString<_32> PlayerName { get; set; }

    /// <summary>本机拥有 State Authority 的玩家实例。</summary>
    public static NetworkPlayer Local { get; private set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Local = this;
            PlayerName = PlayerDisplayNameData.GetLocalDisplayName();
        }

        ApplyName();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
        {
            Local = null;
        }
    }

    /// <summary>
    /// 仅 State Authority 可改网络昵称（游戏内改名成功后由 Controller 调用）。
    /// </summary>
    public bool TrySetDisplayName(string displayName)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        PlayerName = PlayerDisplayNameData.Truncate(displayName);
        ApplyName();
        return true;
    }

    private void ApplyName()
    {
        if (nameLabel == null)
        {
            return;
        }

        nameLabel.text = PlayerName.ToString();
    }
}
