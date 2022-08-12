using UnityEngine;
using Unity.Netcode;
using System.Text;
using TMPro;
using UnityEngine.SceneManagement;



public class ServerSetup : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public TMP_InputField passwordInputField;



    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += ConnectClient;
        NetworkManager.Singleton.OnClientDisconnectCallback += DisconnectClient;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= ConnectClient;
        NetworkManager.Singleton.OnClientDisconnectCallback -= DisconnectClient;
    }


    private void ConnectClient(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            passwordInputField.text = "connecting...";
        }
    }

    private void DisconnectClient(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            passwordInputField.text = "disconnecting...";
        }
    }


    public void Host()
    {
        if (passwordInputField.text == "" || nameInputField.text == "")
            return;


        NetworkManager.Singleton.ConnectionApprovalCallback += PasswordCheck;
        NetworkManager.Singleton.StartHost();
        SceneManager.LoadScene("Lobby");
    }

    public void Join()
    {
        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(passwordInputField.text);
        NetworkManager.Singleton.StartClient();
    }

    public void Leave()
    {
        NetworkManager.Singleton.Shutdown();

        if (NetworkManager.Singleton.IsHost)
            NetworkManager.Singleton.ConnectionApprovalCallback -= PasswordCheck;
    }

    private void PasswordCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string password = Encoding.ASCII.GetString(request.Payload);

        response.Approved = password == passwordInputField.text;
    }
}
