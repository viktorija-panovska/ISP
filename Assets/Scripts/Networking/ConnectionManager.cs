using System;
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



[RequireComponent(typeof(ServerConnectionManager), typeof(ClientConnectionManager))]
public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }


    // ----- Connection ----- //
    public void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        OnNetworkReady();
        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
    }

    private void OnNetworkReady()
    {
        ClientConnectionManager.Instance.OnNetworkReady();
        ServerConnectionManager.Instance.OnNetworkReady();
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.LoadComplete)
            return;

        ServerConnectionManager.Instance.OnClientSceneChanged(sceneEvent.ClientId, SceneManager.GetSceneByName(sceneEvent.SceneName).buildIndex);
    }


    // ----- Control ----- //
    public void StartHost(string password)
    {
        ServerConnectionManager.Instance.SetServerPassword(password);
        NetworkManager.Singleton.StartHost();
    }

    public void RequestDisconnect()
    {
        Debug.Log("Request Disconnect");
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        if (NetworkManager.Singleton.IsClient)
            ClientConnectionManager.Instance.OnUserDisconnectRequest();

        if (NetworkManager.Singleton.IsHost)
            ServerConnectionManager.Instance.OnUserDisconnectRequest();
    }
}
