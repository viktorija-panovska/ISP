using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class GameHUD : MonoBehaviour
{
    private OldPlayerController controller;

    public GameObject SettingsMenuContainer;
    public GameObject EndGameContainer;
    public GameObject HUDMenuContainer;

    public Slider ManaBar;
    public Image[] PowerIconCovers;
    public Image LeaderIconFlash;
    public Image KnightIconFlash;

    public GameObject MapContainer;
    public Image MapCursor;
    private bool mapToggled = false;

    public TMP_Text WinnerText;

    private const double CLICKER_ERROR = 0.2;
    public GameObject[] MarkerPrefabs = new GameObject[4];
    private readonly GameObject[] markers = new GameObject[4];

    private readonly Color grayTransparent = new(0.5f, 0.5f, 0.5f, 0.2f);
    private readonly Color greenTransparent = new(0, 1, 0, 0.2f);



    #region Setup

    private void Awake()
    {
        // Set markers
        for (int i = 0; i < MarkerPrefabs.Length; ++i)
        {
            markers[i] = Instantiate(MarkerPrefabs[i]);
            markers[i].SetActive(false);
        }

        GameObject earthquakeMarker = markers[(int)Power.EARTHQUAKE];

        // size the earthquake marker to the size of the map
        Vector3 scale = earthquakeMarker.transform.localScale;
        scale.x = (OldGameController.EARTHQUAKE_RANGE * Chunk.TILE_WIDTH) * scale.x / earthquakeMarker.GetComponent<Renderer>().bounds.size.x;
        scale.z = (OldGameController.EARTHQUAKE_RANGE * Chunk.TILE_WIDTH) * scale.z / earthquakeMarker.GetComponent<Renderer>().bounds.size.z;
        earthquakeMarker.transform.localScale = scale;

        ManaBar.minValue = OldGameController.MIN_MANA;
        ManaBar.maxValue = OldGameController.MAX_MANA;

        UpdateManaBar(ManaBar.minValue);
    }

    private void OnDestroy()
    {
        ResetCursor();
    }

    public void SetController(OldPlayerController controller)
    {
        this.controller = controller;
    }

    #endregion


    private void Update()
    {
        if (controller == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) && !mapToggled)
            PauseGame();

        if (Input.GetKeyDown(KeyCode.M))
            ToggleMap();
    }


    public void ResetHUD()
    {
        SettingsMenuContainer.SetActive(false);
        EndGameContainer.SetActive(false);
        HUDMenuContainer.SetActive(true);

        ResetCursor();
        SwitchMarker(0);
        UpdateManaBar(controller.Mana);
    }


    #region Settings Menu

    private void PauseGame()
    {
        controller.PauseGame();
        HUDMenuContainer.SetActive(false);
        SettingsMenuContainer.SetActive(true);
    }

    public void LeaveGame()
    {
        //LocalConnectionManager.Instance.Disconnect();
    }

    public void CloseMenu()
    {
        controller.ResumeGame();
        SettingsMenuContainer.SetActive(false);
        HUDMenuContainer.SetActive(true);
    }

    #endregion



    #region End Game Menu

    public void OpenEndGameMenu(Team winner)
    {
        HUDMenuContainer.SetActive(false);
        EndGameContainer.SetActive(true);
        WinnerText.text = $"{winner} WINS!";
    }

    public void NewGame()
    {
        EndGameContainer.SetActive(false);
        OldGameController.Instance.ResetGameServerRpc();
    }
    
    #endregion



    #region GameHUD

    public bool IsClickable(Vector3 hitPoint)
        => Mathf.Abs(Mathf.Round(hitPoint.x / Chunk.TILE_WIDTH) - hitPoint.x / Chunk.TILE_WIDTH) < CLICKER_ERROR &&
           Mathf.Abs(Mathf.Round(hitPoint.z / Chunk.TILE_WIDTH) - hitPoint.z / Chunk.TILE_WIDTH) < CLICKER_ERROR &&
           Mathf.Abs(Mathf.Round(hitPoint.y / Chunk.STEP_HEIGHT) - hitPoint.y / Chunk.STEP_HEIGHT) < CLICKER_ERROR &&
           hitPoint.y > OldGameController.Instance.WaterLevel.Value;


    public void UpdateManaBar(float mana)
    {
        ManaBar.value = mana;

        for (int i = 0; i <= OldGameController.Instance.PowerActivateLevel.Length; ++i)
        {
            if (OldGameController.Instance.PowerActivateLevel[i] > mana)
                break;

            PowerIconCovers[i].gameObject.SetActive(false);
        }
    }


    public void FlashLeaderIcon()
    {
        StartCoroutine(Helpers.FlashImage(LeaderIconFlash));
    }

    public void FlashKnightIcon()
    {
        StartCoroutine(Helpers.FlashImage(KnightIconFlash));
    }

    #endregion


    
    #region Map Menu

    public void ToggleMap()
    {
        mapToggled = !mapToggled;
        HUDMenuContainer.SetActive(!mapToggled);
        MapContainer.SetActive(mapToggled);
        controller.SwitchCameras(mapToggled);

        if (mapToggled)
            SetMapCursor();
        else
            ResetCursor();
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

    #endregion



    #region Markers

    public void SwitchMarker(int index)
    {
        for (int i = 0; i < markers.Length; ++i)
            markers[i].SetActive(false);

        markers[index].SetActive(true);
    }

    public void HighlightMarker(WorldLocation location, int index, bool changeHeight)
    {
        markers[index].transform.position = new Vector3(location.X, changeHeight ? WorldMap.Instance.GetHeight(location) : Chunk.MAX_HEIGHT, location.Z);
        markers[index].GetComponent<MeshRenderer>().material.color = greenTransparent;
    }

    public void GrayoutMarker(Vector3 position, int index, bool changeHeight = true)
    {
        markers[index].GetComponent<MeshRenderer>().material.color = grayTransparent;
        markers[index].transform.position = new Vector3(position.x, changeHeight ? position.y : Chunk.MAX_HEIGHT, position.z);
    }

    #endregion
}
