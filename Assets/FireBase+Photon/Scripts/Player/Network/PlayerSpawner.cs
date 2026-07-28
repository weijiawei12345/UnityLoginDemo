using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    private readonly Dictionary<PlayerRef, NetworkObject> _players =
        new Dictionary<PlayerRef, NetworkObject>();

    public GameObject PlayerPrefab;

    public void PlayerJoined(PlayerRef player)
    {
        NetworkRunner runner = Runner;
        if (runner != null)
        {
            EnsurePlayerSpawned(runner, player);
        }
    }

    public void EnsureLocalPlayerSpawned(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning || !runner.IsPlayerValid(runner.LocalPlayer))
        {
            Debug.LogWarning("[PlayerSpawner] Local player is not ready after scene load.");
            return;
        }

        Debug.Log($"[PlayerSpawner] Scene-ready spawn check for {runner.LocalPlayer}.");
        EnsurePlayerSpawned(runner, runner.LocalPlayer);
    }

    private void EnsurePlayerSpawned(NetworkRunner runner, PlayerRef player)
    {
        if (player != runner.LocalPlayer || _players.ContainsKey(player))
        {
            return;
        }

        if (runner.TryGetPlayerObject(player, out NetworkObject existing) && existing != null)
        {
            _players[player] = existing;
            return;
        }

        if (PlayerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] PlayerPrefab is not assigned.");
            return;
        }

        NetworkObject playerObject = runner.Spawn(
            PlayerPrefab,
            new Vector3(0, -0.1f, 0),
            Quaternion.identity,
            player,
            null,
            NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);
        if (playerObject == null)
        {
            Debug.LogError($"[PlayerSpawner] Failed to spawn player {player}.");
            return;
        }

        _players[player] = playerObject;
        runner.SetPlayerObject(player, playerObject);
        Debug.Log($"[PlayerSpawner] Spawned local player object for {player}.");
    }

    public void PlayerLeft(PlayerRef player)
    {
        NetworkRunner runner = Runner;
        if (runner == null)
        {
            _players.Remove(player);
            return;
        }

        if (!_players.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.TryGetPlayerObject(player, out playerObject);
        }

        _players.Remove(player);
        if (playerObject != null && playerObject.HasStateAuthority)
        {
            runner.Despawn(playerObject);
        }
    }
}
