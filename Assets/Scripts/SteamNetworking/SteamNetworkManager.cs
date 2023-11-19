using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections.Generic;


public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance { get; private set; }

    private const uint STEAM_APP_ID = 480;
    private List<Lobby> activeLobbies;
    private bool gameInProgress;

    // Lobby
    private Lobby? currentLobby = null;

    private const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;

    private string serverName;
    private string password;
    private string mapSeed;



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            //try
            //{
            //    SteamClient.Init(STEAM_APP_ID, true);

            //    if (!SteamClient.IsValid)
            //    {
            //        Debug.LogError("Steam client not valid");
            //        throw new System.Exception();
            //    }

            //    Debug.Log("Steam client valid");

            //    activeLobbies = new List<Lobby>();
            //}
            //catch (System.Exception e) { Debug.Log($"Error connecting to Steam: {e}"); }
        }
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
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void Disconnect()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.Shutdown();
    }



    #region Hosting

    public async void StartHost(string serverName, string password = "", string mapSeed = "")
    {
        this.serverName = serverName;
        this.password = password;
        this.mapSeed = mapSeed;

        currentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK) return;

        lobby.SetData("name", serverName);
        lobby.SetData("password", password);
        lobby.SetData("mapSeed", mapSeed);

        lobby.SetPublic();
        lobby.SetJoinable(true);

        NetworkManager.Singleton.StartHost();
    }

    #endregion



    #region Joining

    public async void JoinGame(SteamId lobbyId)
    {
        Debug.Log("Join Game");
        await SteamMatchmaking.JoinLobbyAsync(lobbyId);
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Debug.Log("OnLobbyMemberJoined");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Debug.Log("OnLobbyEntered");
        Debug.Log($"Name: {lobby.GetData("name")}\n");
        Debug.Log($"Password: {lobby.GetData("password")}\n");
        Debug.Log($"MapSeed: {lobby.GetData("mapSeed")}\n");
        Debug.Log($"Owner: {lobby.Owner.Name}");

        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);

        if (NetworkManager.Singleton.IsHost) return;

        NetworkManager.Singleton.StartClient();
    }


    public List<Lobby> GetActiveLobbies()
    {
        RequestLobbies();

        if (activeLobbies.Count > 0)
            return activeLobbies;

        return null;
    }

    private async void RequestLobbies()
    {
        activeLobbies.Clear();
        Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithMaxResults(10).RequestAsync();

        Debug.Log("Lobbies");

        if (lobbies != null)
            foreach (Lobby lobby in lobbies)
                activeLobbies.Add(lobby);
    }


    #endregion
}