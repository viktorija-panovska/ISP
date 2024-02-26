using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine;


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



public class SteamLobby : NetworkBehaviour
{
    public static SteamLobby Instance;

    public Button LeaveButton;
    public TMP_Text PasswordField;
    public Button KickPlayerButton;
    public Button StartGameButton;
    public LobbyCard[] lobbyCards;

    public NetworkList<LobbyPlayerState> lobbyPlayers;


    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerState>();
        lobbyPlayers.OnListChanged += RefreshPlayerCards;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        lobbyPlayers.OnListChanged -= RefreshPlayerCards;
        lobbyPlayers.Dispose();
    }



    public override void OnNetworkSpawn()
    {
        Debug.Log("Network Spawn");

        if (IsServer)
        {
            PasswordField.text = "PASSWORD: " + SteamNetworkManager.Instance.CurrentLobby.Value.GetData("password");

            StartGameButton.gameObject.SetActive(true);
            StartGameButton.interactable = false;
        }
        else
        {
            StartGameButton.gameObject.SetActive(false);
        }

        KickPlayerButton.gameObject.SetActive(false);
    }


    public void AddPlayer(string name)
    {
        Debug.Log(name);
    }


    private void RefreshPlayerCards(NetworkListEvent<LobbyPlayerState> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<LobbyPlayerState>.EventType.Add)
            ShowCard(changeEvent.Index);

        if (changeEvent.Type == NetworkListEvent<LobbyPlayerState>.EventType.Remove)
            HideCard(changeEvent.Index);
    }


    private void ShowCard(int playerIndex)
    {
        LobbyCard card = lobbyCards[playerIndex];
        LobbyPlayerState playerState = lobbyPlayers[playerIndex];

        card.playerDisplayName.text = playerState.PlayerName.ToString();
        card.waitingForPlayerPanel.SetActive(false);
    }


    private void HideCard(int playerIndex)
    {
        lobbyCards[playerIndex].waitingForPlayerPanel.SetActive(true);
    }
}
