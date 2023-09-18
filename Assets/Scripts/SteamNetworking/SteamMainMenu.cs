using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;


public class SteamMainMenu : MonoBehaviour
{
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


    private void Start()
    {
        openMenuContainer = MainMenuContainer;
    }

    public void ExitGame()
    {
        Application.Quit();
    }


    // ---- SWITCHING MENUS ---- //

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
    }

    private void ResetHostMenu()
    {
        ServerNameInputField.text = "";
        PasswordInputField.text = "";
        MapSeedInputField.text = "";
    }



    //// ------ STARTING GAME ----- //

    public void HostGame()
    {
        if (ServerNameInputField.text == "")
        {
            StartCoroutine(Helpers.FlashImage(EmptyFieldWarning));
            return;
        }

        SteamNetworkManager.Instance.StartHost(ServerNameInputField.text, PasswordInputField.text, MapSeedInputField.text);
    }


    //public void JoinGame()
    //{
    //    if (nameInputField.text == "" || passwordInputField.text == "")
    //        return;

    //    PlayerPrefs.SetString("PlayerName", nameInputField.text);
    //    ClientConnectionManager.Instance.StartClient(passwordInputField.text);
    //}

}
