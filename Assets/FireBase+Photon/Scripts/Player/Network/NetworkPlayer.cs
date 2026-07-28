using ARPG.Player;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// View + 网络状态：同步玩家展示名并刷新世界空间 TMP（参考教程 NetworkPlayer）。
/// Input Authority requests changes; State Authority validates and writes network state.
/// </summary>
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("World-space label")]
    [SerializeField] private TMP_Text nameLabel;

    [Networked, OnChangedRender(nameof(ApplyName))]
    public NetworkString<_32> PlayerName { get; set; }

    /// <summary>The player instance controlled by this client's Input Authority.</summary>
    public static NetworkPlayer Local { get; private set; }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Local = this;
            RPC_RequestDisplayName(PlayerDisplayNameData.GetLocalDisplayName());
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
    /// The local Input Authority may request a display-name change.
    /// </summary>
    public bool TrySetDisplayName(string displayName)
    {
        if (!HasInputAuthority)
        {
            return false;
        }

        RPC_RequestDisplayName(displayName);
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDisplayName(string displayName, RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority)
        {
            return;
        }

        PlayerName = NormalizeDisplayName(displayName);
        ApplyName();
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return PlayerDisplayNameData.FallbackName;
        }

        string trimmed = displayName.Trim();
        var sanitized = new System.Text.StringBuilder(trimmed.Length);
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (!char.IsControl(trimmed[i]))
            {
                sanitized.Append(trimmed[i]);
            }
        }

        return PlayerDisplayNameData.Truncate(sanitized.ToString());
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
