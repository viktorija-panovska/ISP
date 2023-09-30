using UnityEngine;
using Steamworks;
using UnityEngine.UI;

public class LobbyEntry : Toggle
{
    public SteamId LobbyId { get; private set; }
    public string LobbyName { get; private set; }
    public string Password { get; private set; }
    public string MapSeed { get; private set; }
    public Friend Owner { get; private set; }

    public Text Name;
    public Image Icon;
    public Image Lock;


    public void Setup(SteamId lobbyId, string lobbyName, string password, string mapSeed, Friend owner)
    {
        LobbyId = lobbyId;
        LobbyName = lobbyName;
        Password = password;
        MapSeed = mapSeed;
        Owner = owner;

        FillEntry();
    }


    private void FillEntry()
    {
        Name.text = LobbyName;

        if (Password != "")
            Lock.gameObject.SetActive(true);
    }


    public void ClickEntry()
    {
        if (isOn)
            SteamMainMenu.Instance.SelectEntry(this);
        else
            SteamMainMenu.Instance.DeselectEntry();
    }
}
