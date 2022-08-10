using UnityEngine;
using Unity.Netcode;
using System.Text;
using TMPro;
using System;

public class PasswordManager : MonoBehaviour
{
    public TMP_InputField clientPassword;


    public void Host()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += PasswordCheck;
        NetworkManager.Singleton.StartHost();
    }

    public void Join()
    {
        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(clientPassword.text);
        NetworkManager.Singleton.StartClient();
    }

    private void PasswordCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string password = Encoding.ASCII.GetString(request.Payload);

        response.Approved = password == clientPassword.text;
        response.CreatePlayerObject = true;
    }
}
