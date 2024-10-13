using UnityEngine;
using UnityEngine.InputSystem;

namespace Populous
{
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

        private Team m_Team;

        private bool m_IsPaused;
        /// <summary>
        /// Gets a value indicating whether the game is paused.
        /// </summary>
        public bool IsPaused { get => m_IsPaused; }


        private float m_Manna;
        /// <summary>
        /// Gets the amount of manna the player currently has.
        /// </summary>
        public float Manna { get => m_Manna; }

        private Power m_ActivePower = Power.MOLD_TERRAIN;
        /// <summary>
        /// Gets the current active power of the player.
        /// </summary>
        public Power ActivePower { get => m_ActivePower; }

        private int m_ActiveMarkerIndex = 0;
        private MapPoint? m_NearestClickablePoint = null;

        private UnitState m_ActiveUnitState = UnitState.SETTLE;



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            m_Team = GameController.Instance.IsPlayerHosting ? Team.RED : Team.BLUE;

            GameUtils.ResizeGameObject(m_Markers[(int)Power.EARTHQUAKE], 2 * GameController.Instance.EarthquakeRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)Power.SWAMP], 2 * GameController.Instance.SwampRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)Power.VOLCANO], 2 * GameController.Instance.VolcanoRadius * Terrain.Instance.UnitsPerTileSide);
        }

        private void Update()
        {
            if (m_ActivePower != Power.KNIGHT && m_ActivePower != Power.FLOOD && m_ActivePower != Power.ARMAGHEDDON)
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

        /// <summary>
        /// Handles the <b>Move</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            CameraController.Instance.Movement = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Handles the <b>RotateCameraClockwise</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRotateCameraClockwise(InputAction.CallbackContext context)
        {
            if (context.performed)
                CameraController.Instance.RotationDirection = 1;

            if (context.canceled)
                CameraController.Instance.RotationDirection = 0;
        }

        /// <summary>
        /// Handles the <b>RotateCameraCounterclockwise</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRotateCameraCounterclockwise(InputAction.CallbackContext context)
        {
            if (context.performed)
                CameraController.Instance.RotationDirection = -1;

            if (context.canceled)
                CameraController.Instance.RotationDirection = 0;
        }

        /// <summary>
        /// Handles the <b>ZoomCamera</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnZoomCamera(InputAction.CallbackContext context)
        {
            CameraController.Instance.ZoomDirection = (int)context.ReadValue<float>();
        }

        /// <summary>
        /// Handles the <b>LeftClick</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnLeftClick(InputAction.CallbackContext context)
        {
            if (!context.performed || !m_NearestClickablePoint.HasValue || !m_NearestClickablePoint.HasValue) return;

            UseManna();
            switch (m_ActivePower)
            {
                case Power.MOLD_TERRAIN:
                    GameController.Instance.MoldTerrain(m_NearestClickablePoint.Value, lower: true);
                    break;

                case Power.GUIDE_FOLLOWERS:
                    GameController.Instance.MoveFlag/*ServerRpc*/(m_NearestClickablePoint.Value, m_Team);
                    break;

                case Power.EARTHQUAKE:
                    GameController.Instance.EarthquakeServerRpc(m_NearestClickablePoint.Value);
                    break;

                case Power.SWAMP:
                    GameController.Instance.SwampServerRpc(m_NearestClickablePoint.Value);
                    break;

                case Power.VOLCANO:
                    GameController.Instance.VolcanoServerRpc(m_NearestClickablePoint.Value);
                    break;
            }

        }

        /// <summary>
        /// Handles the <b>RightClick</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (!context.performed || !m_NearestClickablePoint.HasValue || m_ActivePower != Power.MOLD_TERRAIN) return;

            if (m_NearestClickablePoint.HasValue)
                GameController.Instance.MoldTerrain(m_NearestClickablePoint.Value, lower: false);
        }
        #endregion



        #region Point Markers

        private void SetNearestClickablePoint()
        {
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit) ||
                hit.point.x - m_ClickerError < 0 || hit.point.z - m_ClickerError < 0 ||
                hit.point.x + m_ClickerError > Terrain.Instance.UnitsPerSide || hit.point.z + m_ClickerError > Terrain.Instance.UnitsPerSide)
            {
                m_NearestClickablePoint = null;
                SetHighlight(hit.point);
                return;
            }

            if (IsPointClickable(hit.point))
                m_NearestClickablePoint = new MapPoint(hit.point.x, hit.point.z);
            else
                m_NearestClickablePoint = null;

            SetHighlight(hit.point);
        }

        private bool IsPointClickable(Vector3 point)
        {
            if (Mathf.Abs(Mathf.Round(point.x / Terrain.Instance.UnitsPerTileSide) - point.x / Terrain.Instance.UnitsPerTileSide) > m_ClickerError ||
                Mathf.Abs(Mathf.Round(point.z / Terrain.Instance.UnitsPerTileSide) - point.z / Terrain.Instance.UnitsPerTileSide) > m_ClickerError ||
                (m_ActivePower == Power.MOLD_TERRAIN && !CanSeeAllies()))
                return false;

            return true;
        }

        private bool CanSeeAllies()
        {
            // TODO implement
            return true;
        }

        private void SwitchActiveMarker(int index)
        {
            if (m_ActiveMarkerIndex == index || m_ActivePower == Power.KNIGHT || m_ActivePower == Power.FLOOD || m_ActivePower == Power.ARMAGHEDDON)
                return;

            m_Markers[m_ActiveMarkerIndex].SetActive(false);
            m_ActiveMarkerIndex = index;
            m_Markers[m_ActiveMarkerIndex].SetActive(true);
        }

        private void SetHighlight(Vector3 position)
        {
            if (m_NearestClickablePoint.HasValue)
            {
                m_Markers[m_ActiveMarkerIndex].transform.position = new Vector3(
                    m_NearestClickablePoint.Value.TileX * Terrain.Instance.UnitsPerTileSide,
                    m_NearestClickablePoint.Value.Y + 5,
                    m_NearestClickablePoint.Value.TileZ * Terrain.Instance.UnitsPerTileSide);
                m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color = m_HighlightMarkerColor;
            }
            else
            {
                m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color = m_GrayedOutMarkerColor;
                m_Markers[m_ActiveMarkerIndex].transform.position = new Vector3(position.x, position.y < 0 ? 0 : position.y + 5, position.z);
            }
        }

        #endregion



        #region Manna and Powers

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

        /// <summary>
        /// Handles the <b>MoldTerrainSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMoldTerrainSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Handles the <b>GuideFollowersSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGuideFollowersSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.GUIDE_FOLLOWERS);
        }

        /// <summary>
        /// Handles the <b>EarthquakeSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnEarthquakeSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.EARTHQUAKE);
        }

        /// <summary>
        /// Handles the <b>SwampSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSwampSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.SWAMP);
        }

        /// <summary>
        /// Handles the <b>KnightSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnKnightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.KNIGHT);

            GameController.Instance.CreateKnight(m_Team);
        }

        /// <summary>
        /// Handles the <b>VolcanoSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnVolcanoSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.VOLCANO);
        }

        /// <summary>
        /// Handles the <b>FloodSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFloodSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.FLOOD);
            GameController.Instance.FloodServerRpc();
        }

        /// <summary>
        /// Handles the <b>ArmagheddonSelected</b> input action.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnArmagheddonSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SwitchActivePower(Power.ARMAGHEDDON);
        }

        #endregion



        #region Influence Behaviors

        public void OnGoToFlagSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActiveUnitState == UnitState.GO_TO_FLAG) return;
            SwitchState(UnitState.GO_TO_FLAG);
        }

        public void OnSettleSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActiveUnitState == UnitState.SETTLE) return;
            SwitchState(UnitState.SETTLE);
        }

        public void OnGatherSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActiveUnitState == UnitState.GATHER) return;
            SwitchState(UnitState.GATHER);
        }

        public void OnBattleSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActiveUnitState == UnitState.BATTLE) return;
            SwitchState(UnitState.BATTLE);
        }

        private void SwitchState(UnitState state)
        {
            m_ActiveUnitState = state;
            UnitManager.Instance.UnitStateChange/*ServerRpc*/(m_ActiveUnitState, m_Team);
        }

        #endregion



        #region Zoom

        public void OnShowLeader(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowLeaderServerRpc(m_Team);
        }

        public void OnShowFlag(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowFlagServerRpc(m_Team);
        }

        public void OnShowKnights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowKnightsServerRpc(m_Team);
        }

        public void OnShowSettlements(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowSettlementsServerRpc(m_Team);
        }

        public void OnShowBattles(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowBattlesServerRpc();
        }

        #endregion



        #region Game Control

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

        #endregion
    }
}