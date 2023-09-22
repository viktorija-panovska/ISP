using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;



public struct PlayerData
{
    public string PlayerName { get; private set; }
    public ulong PlayerId { get; private set; }

    public PlayerData(string playerName, ulong clientId)
    {
        PlayerName = playerName;
        PlayerId = clientId;
    }
}

[Serializable]
public class ConnectionPayload
{
    public string playerId;
    public int clientScene = -1;
    public string playerName;
    public string password;
}



public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance;

    private const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;

    private string serverPassword;
    private bool gameInProgress;

    private Dictionary<string, PlayerData> clientData;
    private Dictionary<ulong, string> clientIdToPlayerId;
    private Dictionary<ulong, int> clientSceneMap;          // which client is in which scene



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += OnServerStart;

        clientData = new Dictionary<string, PlayerData>();
        clientIdToPlayerId = new Dictionary<ulong, string>();
        clientSceneMap = new Dictionary<ulong, int>();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted -= OnServerStart;
    }

    public void OnApplicationQuit()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        if (NetworkManager.Singleton.IsHost)
            OnHostDisconnectRequest();

        else if (NetworkManager.Singleton.IsClient)
            OnClientDisconnectRequest();
    }


    #region Getters

    public PlayerData? GetPlayerData(ulong clientId)
    {
        if (clientIdToPlayerId.TryGetValue(clientId, out string clientGuid))
            if (clientData.TryGetValue(clientGuid, out PlayerData playerData))
                return playerData;

        return null;
    }

    public string GetServerPassword() => serverPassword;

    #endregion



    #region Scene Management

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.LoadComplete)
            return;

        clientSceneMap[sceneEvent.ClientId] = SceneManager.GetSceneByName(sceneEvent.SceneName).buildIndex;
    }

    private void GoToMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
            return;

        SceneManager.LoadScene("MainMenu");
    }

    public void StartGame()
    {
        gameInProgress = true;

        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    #endregion



    private void OnClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton.IsHost)
        {
            clientSceneMap.Remove(clientId);

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                ClearAllClientData();
            }
            else
            {
                ClearClientData(clientId);

                if (gameInProgress)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
                    gameInProgress = false;
                }
            }

            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        GoToMainMenu();
    }



    #region Host

    public void StartHost(string password)
    {        
        serverPassword = password;
        NetworkManager.Singleton.StartHost();
    }

    private void OnServerStart()
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        string clientGuid = Guid.NewGuid().ToString();
        string playerName = PlayerPrefs.GetString("PlayerName", "Missing Name");

        clientData.Add(clientGuid, new PlayerData(playerName, NetworkManager.Singleton.LocalClientId));
        clientIdToPlayerId.Add(NetworkManager.Singleton.LocalClientId, clientGuid);

        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        byte[] connectionData = request.Payload;
        ulong clientId = request.ClientNetworkId;

        if (connectionData.Length > MAX_CONNECTION_PAYLOAD)
        {
            response.Approved = false;
            return;
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            response.Approved = true;
            return;
        }

        string payload = Encoding.UTF8.GetString(connectionData);
        var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

        if (connectionPayload.password != serverPassword)
        {
            response.Approved = false;
            return;
        }

        if (!gameInProgress && clientData.Count < MAX_PLAYERS)
        {
            clientIdToPlayerId[clientId] = connectionPayload.playerId;
            clientData[connectionPayload.playerId] = new PlayerData(connectionPayload.playerName, clientId);

            clientSceneMap[clientId] = connectionPayload.clientScene;

            response.Approved = true;
            return;
        }

        KickClient(clientId);
    }

    public void OnHostDisconnectRequest()
    {
        foreach (var pair in clientSceneMap)
            if (NetworkManager.Singleton.LocalClientId != pair.Key)
                KickClient(pair.Key);

        NetworkManager.Singleton.Shutdown();
        ClearAllClientData();
        SceneManager.LoadScene("MainMenu");
    }

    private void ClearAllClientData()
    {
        clientData.Clear();
        clientIdToPlayerId.Clear();
        clientSceneMap.Clear();

        gameInProgress = false;
    }

    private void ClearClientData(ulong clientId)
    {
        if (clientIdToPlayerId.TryGetValue(clientId, out string guid))
        {
            clientIdToPlayerId.Remove(clientId);

            if (clientData[guid].PlayerId == clientId)
                clientData.Remove(guid);
        }
    }

    public void KickClient(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        NetworkManager.Singleton.DisconnectClient(clientId);
    }

    #endregion



    #region Client

    public void StartClient(string password)
    {
        string payload = JsonUtility.ToJson(new ConnectionPayload()
        {
            playerId = Guid.NewGuid().ToString(),
            clientScene = SceneManager.GetActiveScene().buildIndex,
            playerName = PlayerPrefs.GetString("PlayerName", "Missing Name"),
            password = password
        });

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
        NetworkManager.Singleton.StartClient();
    }

    public void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
    }

    public void OnClientDisconnectRequest()
    {
        NetworkManager.Singleton.Shutdown();
        GoToMainMenu();
    }

    #endregion
}