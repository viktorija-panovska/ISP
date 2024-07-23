using System;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// The <c>PlayerController</c> class contains methods for handling player input and
/// properties for data about the state of the player's team.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Markers")]
    [SerializeField] private float m_ClickerError = 0.05f;
    [SerializeField] private GameObject[] m_Markers;
    [SerializeField] private Color m_HighlightMarkerColor;
    [SerializeField] private Color m_GrayedOutMarkerColor;

    private static PlayerController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static PlayerController Instance { get => m_Instance; }

    private bool m_IsPaused;
    /// <summary>
    /// Gets a value indicating whether the game is paused.
    /// </summary>
    public bool IsPaused { get => m_IsPaused; }

    private float m_Manna;
    public float Manna { get => m_Manna; }

    private Power m_ActivePower = Power.MOLD_TERRAIN;
    public Power ActivePower { get => m_ActivePower; }

    private MapPoint? m_NearestClickablePoint = null;
    public Action<bool> OnClickableChange;
    private int m_ActiveMarkerIndex = 0;



    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    private void Update()
    {
        if ((int)m_ActivePower <= 3)
            SetNearestClickablePoint();
    }

    #endregion



    #region Input Handlers

    /// <summary>
    /// Handles the <b>Pause</b> input action.
    /// </summary>
    /// <param name="context">Details about the input action which triggered this event.</param>
    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        HandlePause();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        CameraController.Instance.Movement = context.ReadValue<Vector2>();
    }

    public void OnRotateCameraClockwise(InputAction.CallbackContext context)
    {
        if (context.performed)
            CameraController.Instance.IsRotating = 1;

        if (context.canceled)
            CameraController.Instance.IsRotating = 0;
    }

    public void OnRotateCameraCounterclockwise(InputAction.CallbackContext context)
    {
        if (context.performed)
            CameraController.Instance.IsRotating = -1;

        if (context.canceled)
            CameraController.Instance.IsRotating = 0;
    }

    public void OnZoomCamera(InputAction.CallbackContext context)
    {
        CameraController.Instance.ZoomDirection = (int)context.ReadValue<float>();
    }

    public void OnMoldTerrainSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.MOLD_TERRAIN);
    }

    public void OnGuideFollowersSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.GUIDE_FOLLOWERS);
    }

    public void OnEarthquakeSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.EARTHQUAKE);
    }

    public void OnSwampSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.SWAMP);
    }

    public void OnKnightSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.KNIGHT);
    }

    public void OnVolcanoSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.VOLCANO);
    }

    public void OnFloodSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.FLOOD);
    }

    public void OnArmagheddonSelected(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        SwitchActivePower(Power.ARMAGHEDDON);
    }

    public void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed || !m_NearestClickablePoint.HasValue) return;

        switch (m_ActivePower)
        {
            case Power.MOLD_TERRAIN:
                UseManna();
                if (m_NearestClickablePoint.HasValue)
                    GameController.Instance.MoldTerrain(m_NearestClickablePoint.Value, lower: true);
                break;
        }

    }

    public void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed || !m_NearestClickablePoint.HasValue || m_ActivePower != Power.MOLD_TERRAIN) return;

        if (m_NearestClickablePoint.HasValue)
            GameController.Instance.MoldTerrain(m_NearestClickablePoint.Value, lower: false);
    }
    #endregion



    private void SetNearestClickablePoint()
    {
        if (!Physics.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit) ||
            hit.point.x < 0 || hit.point.z < 0 || hit.point.x > Terrain.Instance.UnitsPerSide || hit.point.z > Terrain.Instance.UnitsPerSide)
        {
            m_NearestClickablePoint = null;
            SetHighlight(hit.point);

            return;
        }

        if (
            Mathf.Abs(Mathf.Round(hit.point.x / Terrain.Instance.UnitsPerTile) - hit.point.x / Terrain.Instance.UnitsPerTile) < m_ClickerError &&
            Mathf.Abs(Mathf.Round(hit.point.z / Terrain.Instance.UnitsPerTile) - hit.point.z / Terrain.Instance.UnitsPerTile) < m_ClickerError &&
            Mathf.Abs(Mathf.Round(hit.point.y / Terrain.Instance.StepHeight) - hit.point.y / Terrain.Instance.StepHeight) < m_ClickerError)// &&
            //hit.point.y > GameController.Instance.WaterLevel && (m_ActivePower != Power.MOLD_TERRAIN || CanSeeAllies()))
            m_NearestClickablePoint = new MapPoint(hit.point.x, hit.point.z);
        else
            m_NearestClickablePoint = null;

        SetHighlight(hit.point);
    }


    private bool CanSeeAllies()
    {
        // TODO implement
        return true;
    }


    private bool HasEnoughManna(Power power)
    {
        if (m_Manna >= GameController.Instance.PowerActivationThreshold[(int)power])
            return true;

        // flash the symbol red
        return false;
    }

    private void SwitchActivePower(Power power)
    {
        if (HasEnoughManna(power))
        {
            m_ActivePower = power;
            SwitchActiveMarker((int)power);
        }
    }

    private void UseManna()
    {

    }



    #region Markers

    private void SwitchActiveMarker(int index)
    {
        if (m_ActiveMarkerIndex == index) return;

        m_Markers[m_ActiveMarkerIndex].SetActive(false);
        m_ActiveMarkerIndex = index;
        m_Markers[m_ActiveMarkerIndex].SetActive(true);
    }

    private void SetHighlight(Vector3 position)
    {
        if (m_NearestClickablePoint.HasValue)
        {
            m_Markers[m_ActiveMarkerIndex].transform.position = m_NearestClickablePoint.Value.ToVector3();
            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color = m_HighlightMarkerColor;
        }
        else
        {
            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color = m_GrayedOutMarkerColor;
            m_Markers[m_ActiveMarkerIndex].transform.position = position;
        }
    }

    #endregion



    /// <summary>
    /// Pauses the game if it is unpaused and unpauses the game if it is paused.
    /// </summary>
    public void HandlePause()
    {
        if (m_IsPaused)
            PauseMenuController.Instance.HidePauseMenu();
        else
            PauseMenuController.Instance.ShowPauseMenu();

        m_IsPaused = !m_IsPaused;
    }
}