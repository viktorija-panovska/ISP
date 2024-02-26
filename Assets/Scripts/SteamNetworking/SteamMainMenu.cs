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
    public GameObject PasswordContainer;

    private GameObject openMenuContainer;


    // HOST
    public TMP_InputField ServerNameInputField;
    public TMP_InputField PasswordInputField;
    public TMP_InputField MapSeedInputField;
    public Image EmptyServerNameWarning;
    public Button CreateGameButton;


    // JOIN
    public GameObject LobbyScrollView;
    public GameObject LobbyEntryPrefab;

    private List<GameObject> lobbyEntryList;
    private LobbyEntry selectedLobby;

    public TMP_InputField PasswordField;
    public Image EmptyPasswordWarning;


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
            StartCoroutine(Helpers.FlashImage(EmptyServerNameWarning));
            return;
        }

        SteamNetworkManager.Instance.StartHost(ServerNameInputField.text, PasswordInputField.text, MapSeedInputField.text);
    }

    public void JoinGame()
    {
        if (selectedLobby.HasPassword)
            OpenPasswordWindow();
        else
            EnterLobby();
    }

    public void EnterLobby()
    {
        SteamNetworkManager.Instance.JoinLobby(selectedLobby.Id, "");
        selectedLobby = null;
    }

    #endregion



    #region Lobbies 

    public async void FillLobbyList()
    {
        foreach (GameObject lobbyEntry in lobbyEntryList)
            Destroy(lobbyEntry);

        lobbyEntryList = new();

        Task<Lobby[]> gettingLobbies = SteamNetworkManager.Instance.GetActiveLobbies();
        Lobby[] lobbies = await gettingLobbies;

        foreach (Lobby lobby in lobbies)
        {
            //if (lobby.GetData("isISP") == "")
            //    continue;

            GameObject entryObject = Instantiate(LobbyEntryPrefab);
            LobbyEntry entry = entryObject.GetComponent<LobbyEntry>();

            entry.Setup(lobby.Id, "1", lobby.GetData("password") == "");
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


    #region Password Window

    public void OpenPasswordWindow()
    {
        PasswordContainer.SetActive(true);
    }

    public void ClosePasswordWindow()
    {
        PasswordContainer.SetActive(false);
    }

    public void SubmitPassword()
    {
        if (PasswordField.text == "")
        {
            StartCoroutine(Helpers.FlashImage(EmptyPasswordWarning));
            return;
        }

        ClosePasswordWindow();
        EnterLobby();
    }

    #endregion
}
