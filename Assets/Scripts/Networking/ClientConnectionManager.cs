using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;



[RequireComponent(typeof(ConnectionManager))]
public class ClientConnectionManager : MonoBehaviour
{
    public static ClientConnectionManager Instance;


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


    // ----- Disconnect ----- //
    public void OnUserDisconnectRequest()
    {
        NetworkManager.Singleton.Shutdown();
        GoToMainMenu();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        Debug.Log("Client Disconnect");

        if (NetworkManager.Singleton.IsHost)
            return;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

        GoToMainMenu();
    }

    private void GoToMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
            return;

        SceneManager.LoadScene("MainMenu");
    }


    // ----- Connect ----- //
    public void OnNetworkReady()
    {
        if (!NetworkManager.Singleton.IsClient)
        {
            enabled = false;
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

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
}
