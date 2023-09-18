using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using UnityEngine.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using System;



public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance { get; private set; }
    
    private FacepunchTransport NetworkTransport { get => GetComponent<FacepunchTransport>(); }
    private Lobby? currentLobby = null;

    // Lobby specs
    private const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;

    private string serverName;
    private string password;
    private string mapSeed;



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
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        //SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        //SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        //SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        //SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        //SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        //SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
    }

    private void OnApplicationQuit()
    {

    }



    #region Hosting

    public async void StartHost(string serverName, string password = "", string mapSeed = "")
    {
        this.serverName = serverName;
        this.password = password;
        this.mapSeed = mapSeed;

        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        NetworkManager.Singleton.StartHost();

        currentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK) return;

        lobby.SetData("name", serverName);
        lobby.SetData("password", password);
        lobby.SetData("mapSeed", mapSeed);

        name = password = mapSeed = "";

        lobby.SetPublic();
        lobby.SetJoinable(true);
    }

    private void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    private void OnClientConnected(ulong obj)
    {
        if (NetworkManager.Singleton.IsHost) return;

        throw new NotImplementedException();
    }

    private void OnClientDisconnect(ulong obj)
    {
        throw new NotImplementedException();
    }

    #endregion
}