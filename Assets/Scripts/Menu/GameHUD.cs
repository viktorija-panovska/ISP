using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


public class GameHUD : MonoBehaviour
{
    private PlayerController controller;

    public GameObject SettingsMenuContainer;

    public Slider ManaBar;
    public Image[] PowerIconCovers;

    public GameObject MapContainer;
    private bool mapToggled = false;

    public Image MapCursor;
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

        GameObject earthquakeMarker = markers[(int)Powers.Earthquake];

        // size the earthquake marker to the size of the map
        Vector3 scale = earthquakeMarker.transform.localScale;
        scale.x = (GameController.EARTHQUAKE_RANGE * Chunk.TILE_WIDTH) * scale.x / earthquakeMarker.GetComponent<Renderer>().bounds.size.x;
        scale.z = (GameController.EARTHQUAKE_RANGE * Chunk.TILE_WIDTH) * scale.z / earthquakeMarker.GetComponent<Renderer>().bounds.size.z;
        earthquakeMarker.transform.localScale = scale;

        ManaBar.minValue = GameController.MIN_MANA;
        ManaBar.maxValue = GameController.MAX_MANA;
        ManaBar.value = ManaBar.minValue;
    }

    private void OnDestroy()
    {
        ResetCursor();
    }

    public void SetController(PlayerController controller)
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

    private void PauseGame()
    {
        controller.PauseGame();
        SettingsMenuContainer.SetActive(true);
    }

    public void LeaveGame()
    {
        ConnectionManager.Instance.RequestDisconnect();
    }

    public void CloseMenu()
    {
        controller.ResumeGame();
        SettingsMenuContainer.gameObject.SetActive(false);
    }


    public bool IsClickable(RaycastHit hitInfo)
        => hitInfo.collider.gameObject.layer != LayerMask.NameToLayer("Water") &&
           Mathf.Abs(Mathf.Round(hitInfo.point.x / Chunk.TILE_WIDTH) - hitInfo.point.x / Chunk.TILE_WIDTH) < CLICKER_ERROR &&
           Mathf.Abs(Mathf.Round(hitInfo.point.y / Chunk.STEP_HEIGHT) - hitInfo.point.y / Chunk.STEP_HEIGHT) < CLICKER_ERROR &&
           Mathf.Abs(Mathf.Round(hitInfo.point.z / Chunk.TILE_WIDTH) - hitInfo.point.z / Chunk.TILE_WIDTH) < CLICKER_ERROR;


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
        controller.SwitchCameras(mapToggled);


        if (mapToggled)
            SetMapCursor();
        else
        {
            ResetCursor();
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
}
