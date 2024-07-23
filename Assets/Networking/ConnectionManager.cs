using Steamworks;
using Steamworks.Data;
using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


public class ConnectionManager : MonoBehaviour
{
    public bool Local = true; // TODO: Remove - for testing only

    private static ConnectionManager m_Instance;
    public static ConnectionManager Instance { get => m_Instance; }

    private const uint APP_ID = 480;     // SpaceWar id
    public const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;

    private Lobby? m_CurrentLobby;


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            SteamClient.Init(APP_ID, true);
            Debug.Log("Steam is up and running");
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private void Start()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
        SteamClient.Shutdown();
    }

    #endregion



    public void StartGame()
    {
        ScreenFader.Instance.OnFadeOutComplete += OnGameStartReady;
        ScreenFader.Instance.FadeOut();
    }

    private void OnGameStartReady()
    {
        SceneLoader.Instance.SwitchToScene(Scene.GAME_SCENE);
    }


    #region Hosting

    public void StartHost(string lobbyName, string lobbyPassword, string mapSeed)
    {
        GameData.Instance.CurrentLobbyInfo = new LobbyInfo(lobbyName, lobbyPassword);
        GameData.Instance.MapSeed = mapSeed.Length > 0 ? uint.Parse(mapSeed) : (uint)new Random().Next(0, int.MaxValue);

        ScreenFader.Instance.OnFadeOutComplete += OnHostStartReady;
        ScreenFader.Instance.FadeOut();
    }

    private async void OnHostStartReady()
    {
        ScreenFader.Instance.OnFadeOutComplete -= OnHostStartReady;

        if (Local)
        {
            LocalConnectionManager.Instance.StartHost("test");
            return;
        }

        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproval;

        m_CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK) return;

        lobby.SetData("name", GameData.Instance.CurrentLobbyInfo.LobbyName);
        lobby.SetData("password", GameData.Instance.CurrentLobbyInfo.LobbyPassword);
        lobby.SetData("seed", GameData.Instance.MapSeed.ToString());
        lobby.SetData("isISP", "true");
        lobby.SetPublic();
        lobby.SetJoinable(true);

        NetworkManager.Singleton.StartHost();
    }

    private void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        SceneLoader.Instance.SwitchToScene(Scene.LOBBY);
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        // If there is no password, there is no need to check anything. Host can enter automatically
        if (clientId == NetworkManager.ServerClientId || string.IsNullOrEmpty(GameData.Instance.CurrentLobbyInfo.LobbyPassword))
        {
            response.Approved = true;
            return;
        }

        byte[] connectionData = request.Payload;

        if (connectionData.Length > MAX_CONNECTION_PAYLOAD)
        {
            response.Approved = false;
            return;
        }

        string payload = Encoding.UTF8.GetString(connectionData);
        var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

        if (connectionPayload.password != GameData.Instance.CurrentLobbyInfo.LobbyPassword)
        {
            response.Approved = false;
            return;
        }

        if (m_CurrentLobby.Value.MemberCount < MAX_PLAYERS)
        {
            response.Approved = true;
            return;
        }

        NetworkManager.Singleton.DisconnectClient(clientId);
    }

    #endregion


    #region Joining

    public async Task<Lobby[]> GetActiveLobbies()
        => await SteamMatchmaking.LobbyList.WithMaxResults(10).RequestAsync();

    public void JoinLobby(SteamId lobbyId)
    {
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        //NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

        //NetworkManager.Singleton.GetComponent<FacepunchTransport>().targetSteamId = lobbyId;
        //Debug.Log("Joining room hosted by " + lobbyId);

        ScreenFader.Instance.OnFadeOutComplete += OnClientStartReady;
        ScreenFader.Instance.FadeOut();
    }

    private void OnClientStartReady()
    {
        ScreenFader.Instance.OnFadeOutComplete -= OnClientStartReady;

        if (Local)
        {
            LocalConnectionManager.Instance.StartClient("test");
            return;
        }

        if (NetworkManager.Singleton.StartClient())
            Debug.Log("Client has started");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        if (NetworkManager.Singleton.IsHost) return;
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {

    }

    #endregion


    #region Leaving

    public void Disconnect()
    {
        if (Local)
        {
            LocalConnectionManager.Instance.Disconnect();
        }

        //if (NetworkManager.Singleton == null)
        //    return;

        //if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
        //    NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        //if (NetworkManager.Singleton.IsHost)
        //    OnHostDisconnectRequest();

        //else if (NetworkManager.Singleton.IsClient)
        //    OnClientDisconnectRequest();
    }

    public void KickClient()
    {

    }

    #endregion
}
