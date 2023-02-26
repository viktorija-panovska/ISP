using UnityEngine;


public class GameHUD : MonoBehaviour
{
    private GameController gameController;
    public GameObject SettingsMenuContainer;


    private void Update()
    {
        if (gameController == null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            PauseGame();
    }


    public void SetGameController(GameController controller)
    {
        gameController = controller;
    }


    private void PauseGame()
    {
        gameController.PauseGame();
        SettingsMenuContainer.gameObject.SetActive(true);
    }


    public void LeaveGame()
    {
        ConnectionManager.Instance.RequestDisconnect();
    }


    public void CloseMenu()
    {
        gameController.ResumeGame();
        SettingsMenuContainer.gameObject.SetActive(false);
    }
}
