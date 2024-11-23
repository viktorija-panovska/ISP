using UnityEngine;
using UnityEngine.InputSystem;

namespace Populous
{
    /// <summary>
    /// The <c>PlayerController</c> class contains methods for handling player input and
    /// properties for data about the behavior of the player's team.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Markers")]
        [SerializeField] private float m_ClickerLeeway = 0.1f;
        [SerializeField] private GameObject[] m_Markers;
        [SerializeField] private Color m_HighlightMarkerColor;
        [SerializeField] private Color m_GrayedOutMarkerColor;

        private static PlayerController m_Instance;
        /// <summary>
        /// Gets a signleton instance of the class.
        /// </summary>
        public static PlayerController Instance { get => m_Instance; }

        private Team m_Team;
        /// <summary>
        /// Gets the team this player controls.
        /// </summary>
        public Team Team { get => m_Team; }

        private int m_VisibleUnitsAndStructures;
        /// <summary>
        /// Gets the number of units and structures of the player's team currently visible to the player.
        /// </summary>
        public int VisibleUnitsAndStructures { get => m_VisibleUnitsAndStructures; }

        /// <summary>
        /// The <c>MapPoint</c> representing the nearest point on the terrain to the player's 
        /// cursor, null if the player's cursor is outside the bounds of the terrain.
        /// </summary>
        private MapPoint? m_NearestPoint = null;
        /// <summary>
        /// Gets the current active power of the player.
        /// </summary>
        private Power m_ActivePower = Power.MOLD_TERRAIN;
        /// <summary>
        /// Gets the current active behavior of the units in the player's team.
        /// </summary>
        private UnitBehavior m_ActiveBehavior = UnitBehavior.SETTLE;
        /// <summary>
        /// The index of the power marker that's currently active.
        /// </summary>
        private int m_ActiveMarkerIndex = 0;



        private void Awake()
        {
            if (m_Instance)
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
                FindNearestClickablePoint();
        }


        
        #region Input Events

        /// <summary>
        /// Triggers the game to pause or unpause, depending on the behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnPause(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.TogglePauseGameServerRpc();
        }

        /// <summary>
        /// Executes all events triggered by a left click, if the player's cursor is near a clickable point.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnLeftClick(InputAction.CallbackContext context)
        {
            if (!context.performed || !m_NearestPoint.HasValue) return;

            switch (m_ActivePower)
            {
                case Power.MOLD_TERRAIN:
                    //if (m_VisibleUnitsAndStructures > 0)
                        GameController.Instance.MoldTerrain/*ServerRpc*/(m_NearestPoint.Value, lower: false);
                    break;

                case Power.GUIDE_FOLLOWERS:                    
                    GameController.Instance.MoveFlag/*ServerRpc*/(m_NearestPoint.Value, m_Team);
                    break;

                case Power.EARTHQUAKE:
                    GameController.Instance.EarthquakeServerRpc(m_NearestPoint.Value);
                    break;

                case Power.SWAMP:
                    GameController.Instance.SwampServerRpc(m_NearestPoint.Value);
                    break;

                case Power.VOLCANO:
                    GameController.Instance.VolcanoServerRpc(m_NearestPoint.Value);
                    break;
            }

            m_ActivePower = Power.MOLD_TERRAIN;
            SwitchActiveMarker((int)m_ActivePower);
        }

        /// <summary>
        /// Executes all events triggered by a right click, if the player's cursor is near a clickable point.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActivePower != Power.MOLD_TERRAIN || !m_NearestPoint.HasValue) return;

            GameController.Instance.MoldTerrain/*ServerRpc*/(m_NearestPoint.Value, lower: true);
        }


        #region Camera Movement Inputs

        /// <summary>
        /// Triggers the position movement of the camera.
        /// </summary>
        /// <param name="context">Data about the input action which triggered this event.</param>
        public void OnMove(InputAction.CallbackContext context)
            => CameraController.Instance.MovementDirection = context.ReadValue<Vector2>();

        /// <summary>
        /// Triggers the clockwise rotation of the camera around the point it is looking at.
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
        /// Triggers the counter-clockwise rotation of the camera around the point it is looking at.
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
        /// Triggers the zoom in or out of the camera, focused on the point it is looking at.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnZoomCamera(InputAction.CallbackContext context)
            => CameraController.Instance.ZoomDirection = (int)context.ReadValue<float>();

        #endregion


        #region Powers Inputs

        /// <summary>
        /// Activates the Mold Terrain power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMoldTerrainSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Activates the Guide Followers power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGuideFollowersSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.GUIDE_FOLLOWERS);
        }

        /// <summary>
        /// Activates the Earthquake power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnEarthquakeSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.EARTHQUAKE);
        }

        /// <summary>
        /// Activates the Swamp power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSwampSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.SWAMP);
        }

        /// <summary>
        /// Activates the Knight power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnKnightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.KNIGHT);
        }

        /// <summary>
        /// Activates the Volcano power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnVolcanoSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.VOLCANO);
        }

        /// <summary>
        /// Activates the Flood power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFloodSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.FLOOD);
        }

        /// <summary>
        /// Activates the Armagheddon power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnArmagheddonSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivatePower(Power.ARMAGHEDDON);
        }

        #endregion


        #region Influence Behavior Inputs

        /// <summary>
        /// Activates the Go To Flag unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGoToFlagSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.GO_TO_SYMBOL);
        }

        /// <summary>
        /// Activates the Settle unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSettleSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.SETTLE);
        }

        /// <summary>
        /// Activates the Gather unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGatherSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.GATHER);
        }

        /// <summary>
        /// Activates the Fight unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.FIGHT);
        }

        #endregion


        #region Zoom Inputs

        /// <summary>
        /// Triggers camera to show the player's team's symbol.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFlag(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowTeamSymbolServerRpc(m_Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's leader.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowLeader(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowLeaderServerRpc(m_Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's settlements.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowSettlements(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowSettlementsServerRpc(m_Team);
        }

        /// <summary>
        /// Triggers camera to show the fights currenly happening..
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowFightsServerRpc(m_Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's knights.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowKnights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.ShowKnightsServerRpc(m_Team);
        }

        #endregion

        #endregion


        #region Power Markers

        /// <summary>
        /// Finds the nearest point on the terrain that can be clicked. The points that 
        /// can be clicked are the points at the intersections of the terrain grid.
        /// </summary>
        private void FindNearestClickablePoint()
        {
            m_NearestPoint =
                !Physics.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit,
                    maxDistance: Mathf.Infinity, layerMask: LayerMask.GetMask(LayerData.TERRAIN_LAYER_NAME)) ||
                    hit.point.x - m_ClickerLeeway < 0 && hit.point.x + m_ClickerLeeway > Terrain.Instance.UnitsPerSide ||
                    hit.point.z - m_ClickerLeeway < 0 && hit.point.z + m_ClickerLeeway > Terrain.Instance.UnitsPerSide
                ? null
                : new MapPoint(hit.point.x, hit.point.z, getClosestPoint: true);

            SetupMarker();
        }

        /// <summary>
        /// Sets the active marker to the marker with the given index.
        /// </summary>
        /// <param name="index">The index of the new active marker.</param>
        private void SwitchActiveMarker(int index)
        {
            if (m_ActiveMarkerIndex == index || m_ActivePower == Power.KNIGHT || 
                m_ActivePower == Power.FLOOD || m_ActivePower == Power.ARMAGHEDDON)
                return;

            m_Markers[m_ActiveMarkerIndex].SetActive(false);
            m_ActiveMarkerIndex = index;
            m_Markers[m_ActiveMarkerIndex].SetActive(true);
        }

        /// <summary>
        /// Sets the marker's posiiton and changes it's color if an action cannot be performed.
        /// </summary>
        private void SetupMarker()
        {
            if (!m_NearestPoint.HasValue) return;

            m_Markers[m_ActiveMarkerIndex].transform.position = new Vector3(
                m_NearestPoint.Value.GridX * Terrain.Instance.UnitsPerTileSide,
                m_NearestPoint.Value.Y + 2,
                m_NearestPoint.Value.GridZ * Terrain.Instance.UnitsPerTileSide
            );

            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color =
                (m_ActivePower != Power.MOLD_TERRAIN || m_VisibleUnitsAndStructures > 0) 
                ? m_HighlightMarkerColor 
                : m_GrayedOutMarkerColor;
        }

        #endregion



        /// <summary>
        /// Checks with the server if this the given power can be activated for this player.
        /// </summary>
        /// <param name="power">The <c>Power</c> that the player wants to activate.</param>
        private void TryActivatePower(Power power)
        {
            if (power == m_ActivePower && power != Power.KNIGHT && power != Power.FLOOD) return;
            GameController.Instance.TryActivatePower/*ServerRpc*/(m_Team, power);
        }

        /// <summary>
        /// Recieves information whether the given power has been activated or not and acts accordingly.
        /// </summary>
        /// <param name="power">The <c>Power</c> the player wanted to activate.</param>
        /// <param name="isActivated">True if the power has been activated, false otherwise.</param>
        public void ReceivePowerActivation(Power power, bool isActivated)
        {
            if (!isActivated)
            {
                GameUI.Instance.NotEnoughManna(power);
                SwitchActiveMarker(0);
                return;
            }

            if (power == Power.KNIGHT || power == Power.FLOOD)
                power = Power.MOLD_TERRAIN;

            m_ActivePower = power;
            SwitchActiveMarker((int)m_ActivePower);
        }

        /// <summary>
        /// Sets the behavior of the units of this player's team to the given behavior.
        /// </summary>
        /// <param name="behavior">The new behavior that should be applied to all the units.</param>
        private void SetUnitBehavior(UnitBehavior behavior)
        {
            if (behavior == m_ActiveBehavior) return;
            m_ActiveBehavior = behavior;
            UnitManager.Instance.ChangeUnitBehavior/*ServerRpc*/(m_ActiveBehavior, m_Team);
        }

        public void AddVisibleUnitOrStructure() => m_VisibleUnitsAndStructures++;

        public void RemoveVisibleUnitOrStructure() => m_VisibleUnitsAndStructures--;
    }
}