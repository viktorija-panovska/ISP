using TMPro;
using UnityEngine;


public class MainMenuUI : MonoBehaviour
{
    public GameObject mainMenuContainer;
    public GameObject gameEntryContainer;

    public GameObject createGameButton;
    public GameObject joinGameButton;

    public TMP_InputField nameInputField;
    public TMP_InputField passwordInputField;



    // ---- SWITCHING MENUS ---- //

    public void OpenMainMenu()
    {
        mainMenuContainer.SetActive(true);
        gameEntryContainer.SetActive(false);
        passwordInputField.text = "";
    }

    public void OpenHostMenu()
    {
        mainMenuContainer.SetActive(false);
        gameEntryContainer.SetActive(true);
        createGameButton.SetActive(true);
        joinGameButton.SetActive(false);
    }

    public void OpenJoinMenu()
    {
        mainMenuContainer.SetActive(false);
        gameEntryContainer.SetActive(true);
        createGameButton.SetActive(false);
        joinGameButton.SetActive(true);
    }



    // ------ STARTING GAME ----- //

    public void HostGame()
    {
        if (nameInputField.text == "" || passwordInputField.text == "")
            return;

        PlayerPrefs.SetString("PlayerName", nameInputField.text);
        LocalConnectionManager.Instance.StartHost(passwordInputField.text);
    }

    public void JoinGame()
    {
        if (nameInputField.text == "" || passwordInputField.text == "")
            return;

        PlayerPrefs.SetString("PlayerName", nameInputField.text);
        LocalConnectionManager.Instance.StartClient(passwordInputField.text);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}