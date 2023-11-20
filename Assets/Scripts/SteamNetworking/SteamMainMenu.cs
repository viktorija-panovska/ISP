using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lobby = Steamworks.Data.Lobby;



public class SteamMainMenu : MonoBehaviour
{
    public static SteamMainMenu Instance { get; private set; }

    public Image BackgroundImage;

    public GameObject MainMenuContainer;
    public GameObject HostGameContainer;
    public GameObject JoinGameContainer;

    private GameObject openMenuContainer;


    // HOST

    public TMP_InputField ServerNameInputField;
    public TMP_InputField PasswordInputField;
    public TMP_InputField MapSeedInputField;
    public Image EmptyFieldWarning;
    public Button CreateGameButton;


    // JOIN
    public GameObject LobbyScrollView;
    public GameObject LobbyEntryPrefab;

    private List<GameObject> lobbyEntryList;
    private LobbyEntry selectedLobby;


    public void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        openMenuContainer = MainMenuContainer;
        lobbyEntryList = new();
    }

    public void ExitGame()
    {
        Application.Quit();
    }



    #region Switching Menus

    public void OpenMainMenu()
    {
        openMenuContainer.SetActive(false);
        MainMenuContainer.SetActive(true);
        BackgroundImage.gameObject.SetActive(true);
        openMenuContainer = MainMenuContainer;
    }

    public void OpenHostMenu()
    {
        ResetHostMenu();
        openMenuContainer.SetActive(false);
        HostGameContainer.SetActive(true);
        BackgroundImage.gameObject.SetActive(true);
        openMenuContainer = HostGameContainer;
    }

    public void OpenJoinMenu()
    {
        openMenuContainer.SetActive(false);
        JoinGameContainer.SetActive(true);
        BackgroundImage.gameObject.SetActive(false);
        openMenuContainer = JoinGameContainer;

        FillLobbyList();
    }

    private void ResetHostMenu()
    {
        ServerNameInputField.text = "";
        PasswordInputField.text = "";
        MapSeedInputField.text = "";
    }

    #endregion



    #region Connection

    public void HostGame()
    {
        if (ServerNameInputField.text == "")
        {
            StartCoroutine(Helpers.FlashImage(EmptyFieldWarning));
            return;
        }

        SteamNetworkManager.Instance.StartHost(ServerNameInputField.text, PasswordInputField.text, MapSeedInputField.text);
    }

    public void JoinGame()
    {
        SteamNetworkManager.Instance.JoinLobby(selectedLobby.Id);
        selectedLobby = null;
    }

    public async void FillLobbyList()
    {
        foreach (GameObject lobbyEntry in lobbyEntryList)
            Destroy(lobbyEntry);

        lobbyEntryList = new();

        Task<Lobby[]> gettingLobbies = SteamNetworkManager.Instance.GetActiveLobbies();
        Lobby[] lobbies = await gettingLobbies;

        foreach (Lobby lobby in lobbies)
        {
            if (lobby.GetData("isISP") == "")
                continue;

            GameObject entryObject = Instantiate(LobbyEntryPrefab);
            LobbyEntry entry = entryObject.GetComponent<LobbyEntry>();

            entry.Setup(lobby.Id, lobby.GetData("name"), lobby.GetData("password") != "");
            entryObject.transform.SetParent(LobbyScrollView.transform);
            entryObject.transform.localScale = Vector3.one;

            lobbyEntryList.Add(entryObject);
        }
    }

    public void SelectEntry(LobbyEntry entry)
    {
        if (selectedLobby != null)
            selectedLobby.Deselect();

        selectedLobby = entry;
    }

    public void DeselectEntry()
    {
        selectedLobby = null;
    }

    #endregion
}
