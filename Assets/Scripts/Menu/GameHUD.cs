using UnityEngine;
using UnityEngine.UI;


public class GameHUD : MonoBehaviour
{
    private GameController gameController;

    public GameObject SettingsMenuContainer;

    public Slider ManaBar;
    public Image[] PowerIconCovers;

    public GameObject MapContainer;
    private bool mapToggled = false;

    public Texture2D ClickyCursorTexture;
    public Image MapCursor;


    private void Start()
    {
        ManaBar.minValue = GameController.MIN_MANA;
        ManaBar.maxValue = GameController.MAX_MANA;
        ManaBar.value = ManaBar.minValue;
    }


    private void Update()
    {
        if (gameController == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) && !mapToggled)
            PauseGame();

        if (Input.GetKeyDown(KeyCode.M))
            ToggleMap();
    }


    private void OnDestroy()
    {
        ResetCursor();
    }


    public void SetGameController(GameController controller)
    {
        gameController = controller;
    }


    private void PauseGame()
    {
        gameController.PauseGame();
        SettingsMenuContainer.SetActive(true);
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


    public void UpdateManaBar(int mana)
    {
        ManaBar.value = mana;

        for (int i = 0; i <= Mathf.FloorToInt(ManaBar.value / 15); ++i)
            PowerIconCovers[i].gameObject.SetActive(false);
    }


    public void ToggleMap()
    {
        mapToggled = !mapToggled;
        ManaBar.gameObject.SetActive(!mapToggled);
        MapContainer.SetActive(mapToggled);
        gameController.SwitchCameras(mapToggled);


        if (mapToggled)
            SetMapCursor();
        else
        {
            ResetCursor();
        }
    }


    public bool IsClickable(Vector3 hitPoint)
    {
        if (Mathf.Abs(Mathf.Round(hitPoint.x / Chunk.TILE_WIDTH) - hitPoint.x / Chunk.TILE_WIDTH) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.y / Chunk.STEP_HEIGHT) - hitPoint.y / Chunk.STEP_HEIGHT) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.z / Chunk.TILE_WIDTH) - hitPoint.z / Chunk.TILE_WIDTH) < 0.1)
        {
            Cursor.SetCursor(
                ClickyCursorTexture,
                new Vector2(ClickyCursorTexture.width / 2, ClickyCursorTexture.height / 2),
                CursorMode.Auto
            );

            return true;
        }
        else
        {
            ResetCursor();
            return false;
        }
    }

    public void SetMapCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        MapCursor.gameObject.SetActive(true);
    }

    public void ResetCursor()
    {
        MapCursor.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
