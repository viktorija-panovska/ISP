using System;
using TMPro;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine.UI;


public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public bool IsReady;

    public LobbyPlayerState(ulong clientId, FixedString32Bytes playerName, bool isReady)
    {
        ClientId = clientId;
        PlayerName = playerName;
        IsReady = isReady;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerState other)
    {
        return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName) && IsReady == other.IsReady;
    }
}


public class LobbyUI : NetworkBehaviour
{
    public LobbyCard[] lobbyCards;
    public NetworkList<LobbyPlayerState> lobbyPlayers;
    public Button startGameButton;
    public TMP_Text password;


    public void Awake()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        lobbyPlayers.OnListChanged -= RefreshPlayerCards;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }


    // ----- Start Network ----- //

    public override void OnNetworkSpawn()
    {
        if (IsClient)
            lobbyPlayers.OnListChanged += RefreshPlayerCards;

        if (IsServer)
        {
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = false;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
                OnClientConnected(client.ClientId);

            password.text = "PASSWORD: " + ServerConnectionManager.Instance.GetServerPassword();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        var playerData = ServerConnectionManager.Instance.GetPlayerData(clientId);

        if (!playerData.HasValue)
            return;

        lobbyPlayers.Add(new LobbyPlayerState(clientId, playerData.Value.PlayerName, false));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Lobby");
        for (int i = 0; i < lobbyPlayers.Count; ++i)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }



    // ----- Player Cards ----- //

    private void RefreshPlayerCards(NetworkListEvent<LobbyPlayerState> lobbyState)
    {
        for (int i = 0; i < lobbyPlayers.Count; ++i)
            OpenCard(lobbyCards[i], lobbyPlayers[i]);

        for (int i = lobbyPlayers.Count; i < lobbyCards.Length; ++i)
            HideCard(lobbyCards[i]);
    }

    private void OpenCard(LobbyCard card, LobbyPlayerState lobbyPlayerState)
    {
        card.playerDisplayName.text = lobbyPlayerState.PlayerName.ToString();

        if (lobbyPlayerState.IsReady)
            card.isReadyToggle.GetComponent<Image>().color = Color.green;
        else
            card.isReadyToggle.GetComponent<Image>().color = Color.white;

        if (NetworkManager.Singleton.IsHost && lobbyPlayerState.ClientId != NetworkManager.Singleton.LocalClientId)
            card.kickPlayerButton.gameObject.SetActive(true);

        card.waitingForPlayerPanel.SetActive(false);
        card.playerInfoPanel.SetActive(true);
    }

    private void HideCard(LobbyCard card)
    {
        card.waitingForPlayerPanel.SetActive(true);
        card.playerInfoPanel.SetActive(false);
    }

    public void ToggleReady(LobbyCard card)
    {
        for (int i = 0; i < lobbyCards.Length; ++i)
        {
            if (lobbyCards[i] == card && lobbyPlayers[i].ClientId == NetworkManager.Singleton.LocalClientId)
            {
                ReadyServerRpc();
                return;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReadyServerRpc(ServerRpcParams serverRpcParams = default)
    {

        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == serverRpcParams.Receive.SenderClientId)
            {
                lobbyPlayers[i] = new LobbyPlayerState(lobbyPlayers[i].ClientId, lobbyPlayers[i].PlayerName, !lobbyPlayers[i].IsReady);

                if (ArePlayersReady())
                    startGameButton.interactable = true;
                else
                    startGameButton.interactable = false;

                return;
            }
        }
    }



    // ----- Lobby Buttons ----- //

    public void LeaveLobby()
    {
        ConnectionManager.Instance.RequestDisconnect();
    }

    public void StartGame()
    {
        StartGameServerRpc();
    }

    [ServerRpc]
    private void StartGameServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (serverRpcParams.Receive.SenderClientId != NetworkManager.Singleton.LocalClientId)
            return;

        ServerConnectionManager.Instance.StartGame();
    }

    private bool ArePlayersReady()
    {
        if (lobbyPlayers.Count < lobbyCards.Length)
            return false;

        foreach (LobbyPlayerState playerState in lobbyPlayers)
            if (!playerState.IsReady)
                return false;

        return true;
    }

    public void KickPlayer(LobbyCard card)
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        for (int i = 0; i < lobbyCards.Length; ++i)
        {
            if (lobbyCards[i] == card)
            {
                ServerConnectionManager.Instance.KickClient(lobbyPlayers[i].ClientId);
                return;
            }
        }        
    }
}
