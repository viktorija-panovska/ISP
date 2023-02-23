using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(ConnectionManager))]
public class ServerConnectionManager : MonoBehaviour
{
    public static ServerConnectionManager Instance;

    private const int maxPlayers = 2;
    private const int maxConnectionPayload = 1024;

    private static int serverScene => SceneManager.GetActiveScene().buildIndex;
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
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += OnServerStart;

        clientData = new Dictionary<string, PlayerData>();
        clientIdToPlayerId = new Dictionary<ulong, string>();
        clientSceneMap = new Dictionary<ulong, int>();
    }

    private void OnDestroy()
    {
        if (ConnectionManager.Instance == null || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted -= OnServerStart;
    }

    public PlayerData? GetPlayerData(ulong clientId)
    {
        if (clientIdToPlayerId.TryGetValue(clientId, out string clientGuid))
            if (clientData.TryGetValue(clientGuid, out PlayerData playerData))
                return playerData;

        return null;
    }

    public void SetServerPassword(string password) => serverPassword = password;

     
    public string GetServerPassword() => serverPassword;

    public void OnClientSceneChanged(ulong clientId, int sceneIndex)
    {
        clientSceneMap[clientId] = sceneIndex;
    }



    // ----- Disconnect ----- //
    public void OnUserDisconnectRequest()
    {
        foreach (var pair in clientSceneMap)
            if (NetworkManager.Singleton.LocalClientId != pair.Key)
                KickClient(pair.Key);

        NetworkManager.Singleton.Shutdown();
        ClearAllClientData();
        SceneManager.LoadScene("MainMenu");
    }

    private void OnClientDisconnect(ulong clientId)
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
        }
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



    // ----- Connect ----- //
    public void OnNetworkReady()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        if (NetworkManager.Singleton.IsHost)
            clientSceneMap[NetworkManager.Singleton.LocalClientId] = serverScene;
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

        if (connectionData.Length > maxConnectionPayload)
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

        if (!gameInProgress && clientData.Count < maxPlayers)
        {
            clientIdToPlayerId[clientId] = connectionPayload.playerId;
            clientData[connectionPayload.playerId] = new PlayerData(connectionPayload.playerName, clientId);

            clientSceneMap[clientId] = connectionPayload.clientScene;

            response.Approved = true;
            return;
        }

        KickClient(clientId);
    }

    public void StartGame()
    {
        gameInProgress = true;

        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }
}
