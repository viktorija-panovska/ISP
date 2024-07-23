using System;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;



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

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }



    #region Network

    public override void OnNetworkSpawn()
    {
        lobbyPlayers.OnListChanged += RefreshPlayerCards;

        if (IsServer)
        {
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = false;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
                OnClientConnected(client.ClientId);

            password.text = "PASSWORD: " + LocalConnectionManager.Instance.GetServerPassword();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        var playerData = LocalConnectionManager.Instance.GetPlayerData(clientId);

        if (!playerData.HasValue)
            return;

        lobbyPlayers.Add(new LobbyPlayerState(clientId, playerData.Value.PlayerName, false));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; ++i)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    #endregion



    #region Player Cards

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

        //if (NetworkManager.Singleton.IsHost && lobbyPlayerState.ClientId != NetworkManager.Singleton.LocalClientId)
        //    card.kickPlayerButton.gameObject.SetActive(true);

        card.waitingForPlayerPanel.SetActive(false);
    }

    private void HideCard(LobbyCard card)
    {
        card.waitingForPlayerPanel.SetActive(true);
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

    #endregion



    #region Lobby

    public void LeaveLobby()
    {
        LocalConnectionManager.Instance.Disconnect();
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

        LocalConnectionManager.Instance.StartGame();
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
                LocalConnectionManager.Instance.KickClient(lobbyPlayers[i].ClientId);
                return;
            }
        }        
    }

    #endregion
}
