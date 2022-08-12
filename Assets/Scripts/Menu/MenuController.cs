using UnityEngine;

public class MenuController : MonoBehaviour
{
    public GameObject mainMenuContainer;
    public GameObject gameEntryContainer;
    public GameObject mapSeedField;
    public GameObject createGameButton;
    public GameObject joinGameButton;


    public void ExitButton()
    {
        Application.Quit();
    }

    public void OpenMainMenu()
    {
        mainMenuContainer.SetActive(true);
        gameEntryContainer.SetActive(false);
        mapSeedField.SetActive(false);
        createGameButton.SetActive(false);
        joinGameButton.SetActive(false);
    }

    public void OpenHostMenu()
    {
        mainMenuContainer.SetActive(false);
        gameEntryContainer.SetActive(true);
        mapSeedField.SetActive(true);
        createGameButton.SetActive(true);
    }

    public void OpenJoinMenu()
    {
        mainMenuContainer.SetActive(false);
        gameEntryContainer.SetActive(true);
        joinGameButton.SetActive(true);
    }
}