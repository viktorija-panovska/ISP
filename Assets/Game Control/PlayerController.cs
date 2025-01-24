using UnityEngine;
using UnityEngine.InputSystem;

namespace Populous
{
    /// <summary>
    /// The <c>PlayerController</c> class processes the player's input and passes it along to the correct systems to execute the player's actions.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Power Markers")]

        [Tooltip("The distance between the cursor and the terrain point within which the click on the point is registered.")]
        [SerializeField] private float m_ClickLeeway = 0.1f;

        [Tooltip("The color of the marker when the power can be used.")]
        [SerializeField] private Color m_HighlightMarkerColor;

        [Tooltip("The color of the marker when the power cannot be used.")]
        [SerializeField] private Color m_GrayedOutMarkerColor;

        [Tooltip("The marker objects corresponding to each power. The index of the marker in this array corresponds to the value of the power in belongs to in the Power enum.")]
        [SerializeField] private GameObject[] m_Markers;

        #endregion


        #region Class Fields

        private static PlayerController m_Instance;
        /// <summary>
        /// Gets a signleton instance of the class.
        /// </summary>
        public static PlayerController Instance { get => m_Instance; }

        /// <summary>
        /// Information about this player, null if the player hasn't been set.
        /// </summary>
        private PlayerInfo? m_PlayerInfo = null;
        /// <summary>
        /// Gets the team this player controls, or Team.NONE if the player hasn't been set.
        /// </summary>
        public Team Team { get => m_PlayerInfo.HasValue ? m_PlayerInfo.Value.Team : Team.NONE; }
        /// <summary>
        /// Gets the network ID of this player, or ulong.MaxValue if the player hasn't been set.
        /// </summary>
        public ulong NetworkId { get => m_PlayerInfo.HasValue ? m_PlayerInfo.Value.NetworkId : ulong.MaxValue; }

        /// <summary>
        /// True if the game is paused, false otherwise.
        /// </summary>
        private bool m_IsPaused;
        /// <summary>
        /// True if the player's input should be processed, false otherwise.
        /// </summary>
        private bool CanInteract { get => m_PlayerInfo.HasValue && !m_IsPaused && m_ActivePower != Power.ARMAGHEDDON; }

        /// <summary>
        /// The <c>TerrainPoint</c> closest to the player's cursor. Null if the player's cursor is outside the bounds of the terrain
        /// </summary>
        private TerrainPoint? m_NearestPoint = null;
        /// <summary>
        /// Gets the player's current active power.
        /// </summary>
        private Power m_ActivePower = Power.MOLD_TERRAIN;
        /// <summary>
        /// Gets the current active behavior of the units in the player's team.
        /// </summary>
        private UnitBehavior m_ActiveBehavior = UnitBehavior.SETTLE;
        /// <summary>
        /// The markerIndex of the power marker that's currently active.
        /// </summary>
        private int m_ActiveMarkerIndex = 0;

        #endregion


        #region Event Functions

        private void Awake()
        {
            if (m_Instance)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start() => SetupMarkers();

        private void Update()
        {
            if (!CanInteract || m_ActivePower == Power.KNIGHT || m_ActivePower == Power.FLOOD)
                return;

            m_NearestPoint = GetNearestPoint();
            PlaceMarker();
        }

        #endregion


        #region Game Flow

        /// <summary>
        /// Sets the team that the player controls.
        /// </summary>
        /// <param name="team">The <c>Team</c> that should be set.</param>
        public void SetPlayerInfo(PlayerInfo? playerInfo) => m_PlayerInfo = playerInfo;

        /// <summary>
        /// Pauses or unpauses the game, based on the given boolean parameter.
        /// </summary>
        /// <param name="pause">True if the game should be paused, false otherwise.</param>
        public void SetPause(bool pause)
        {
            m_IsPaused = pause;
            Time.timeScale = m_IsPaused ? 0 : 1;

            if (m_IsPaused)
                PauseMenu.Instance.ShowPauseMenu();
            else
                PauseMenu.Instance.HidePauseMenu();
        }

        #endregion


        #region Units

        /// <summary>
        /// Sets the behavior of the units of this player's team to the given behavior.
        /// </summary>
        /// <param name="behavior">The new behavior that should be applied to all the units.</param>
        public void SetUnitBehavior(UnitBehavior behavior)
        {
            if (behavior == m_ActiveBehavior) return;
            GameUI.Instance.SetActiveBehaviorIcon(currentBehavior: behavior, lastBehavior: m_ActiveBehavior);
            m_ActiveBehavior = behavior;
            GameController.Instance.ChangeUnitBehavior_ServerRpc(m_ActiveBehavior, Team);
        }

        #endregion


        #region Powers

        /// <summary>
        /// Checks with the server if the given power can be activated for this player.
        /// </summary>
        /// <param name="power">The <c>Power</c> that the player wants to activate.</param>
        public void TryActivatePower(Power power)
        {
            // Don't call the server unnecessarily when a power is selected multiple times, except for the Knight and Flood powers, which have effects on each selection.
            if (power == m_ActivePower && power != Power.KNIGHT && power != Power.FLOOD) return;
            GameController.Instance.TryActivatePower_ServerRpc(Team, power);
        }

        /// <summary>
        /// Recieves information whether the given power has been activated or not and acts accordingly.
        /// </summary>
        /// <param name="power">The <c>Power</c> the player wanted to activate.</param>
        /// <param name="isActivated">True if the power has been activated, false otherwise.</param>
        public void ReceivePowerActivationInfo(Power power, bool isActivated)
        {
            if (!isActivated)
            {
                GameUI.Instance.ShowNotEnoughManna(power);
                SetActivePower(Power.MOLD_TERRAIN);
                return;
            }

            // these two were activated instantaneously
            if (power == Power.KNIGHT || power == Power.FLOOD)
                power = Power.MOLD_TERRAIN;

            SetActivePower(power);
        }

        /// <summary>
        /// Sets a new currently active power.
        /// </summary>
        /// <param name="power">The <c>Power</c> that should be set.</param>
        private void SetActivePower(Power power)
        {
            GameUI.Instance.SetActivePowerIcon(currentPower: power, lastPower: m_ActivePower);
            m_ActivePower = power;
            SwitchActiveMarker(m_ActivePower);
        }

        #endregion


        #region Power Markers

        /// <summary>
        /// Sizes the markers according to the size of the areas of their respective powers.
        /// </summary>
        private void SetupMarkers()
        {
            GameUtils.ResizeGameObject(m_Markers[(int)Power.EARTHQUAKE], 2 * GameController.Instance.EarthquakeRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)Power.SWAMP], 2 * GameController.Instance.SwampRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)Power.VOLCANO], 2 * GameController.Instance.VolcanoRadius * Terrain.Instance.UnitsPerTileSide);
        }

        /// <summary>
        /// Finds the closest <c>TerrainPoint</c> and places the marker there.
        /// </summary>
        /// <returns>The nearest <c>TerrainPoint</c> to the cursor, or null if none is found.</returns>
        private TerrainPoint? GetNearestPoint()
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit,
                maxDistance: Mathf.Infinity, layerMask: LayerMask.GetMask(LayerData.TERRAIN_LAYER_NAME, LayerData.WATER_LAYER_NAME)) ||
                hit.point.x - m_ClickLeeway < 0 && hit.point.x + m_ClickLeeway > Terrain.Instance.UnitsPerSide ||
                hit.point.z - m_ClickLeeway < 0 && hit.point.z + m_ClickLeeway > Terrain.Instance.UnitsPerSide)
                return new(hit.point.x, hit.point.z, getClosestPoint: true);
           
            return null;
        }

        /// <summary>
        /// Sets the marker's posiiton to the nearest <c>Terrainpoint</c> and changes its color to show whether the power can be executed.
        /// </summary>
        private void PlaceMarker()
        {
            if (!m_NearestPoint.HasValue) return;

            m_Markers[m_ActiveMarkerIndex].transform.position = new Vector3(
                m_NearestPoint.Value.GridX * Terrain.Instance.UnitsPerTileSide,
                m_NearestPoint.Value.Y + 2,
                m_NearestPoint.Value.GridZ * Terrain.Instance.UnitsPerTileSide
            );

            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color =
                (m_ActivePower == Power.MOLD_TERRAIN && CameraDetectionZone.Instance.VisibleTeamObjects <= 0) ||
                (m_ActivePower == Power.GUIDE_FOLLOWERS && m_NearestPoint.Value.Y <= Terrain.Instance.WaterLevel && !m_NearestPoint.Value.IsOnShore)
                ? m_GrayedOutMarkerColor
                : m_HighlightMarkerColor;
        }

        /// <summary>
        /// Sets the active marker to the marker of the given power.
        /// </summary>
        /// <param name="newPower">The power whose marker should be activated.</param>
        private void SwitchActiveMarker(Power newPower)
        {
            if (m_ActivePower == newPower || m_ActivePower == Power.KNIGHT ||
                m_ActivePower == Power.FLOOD || m_ActivePower == Power.ARMAGHEDDON)
                return;

            m_Markers[m_ActiveMarkerIndex].SetActive(false);
            m_ActiveMarkerIndex = (int)newPower;
            m_Markers[m_ActiveMarkerIndex].SetActive(true);
        }

        #endregion


        #region Input Events

        /// <summary>
        /// Triggers the game to pause or unpause, depending on the behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnPause(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            GameController.Instance.TogglePauseGame_ServerRpc();
        }

        /// <summary>
        /// Executes all events triggered by a left click, if the player's cursor is near a clickable point.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnLeftClick(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            // TODO: rethink this
            if (Physics.Raycast(
                Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()),
                out RaycastHit hitInfo,
                Mathf.Infinity,
                LayerMask.GetMask(LayerData.RED_TEAM_LAYER_NAME, LayerData.BLUE_TEAM_LAYER_NAME, LayerData.UI_LAYER_NAME)))
            {
                GameController.Instance.SetFocusedObject_ServerRpc(hitInfo.collider.gameObject, Team);
                return;
            }

            if (!m_NearestPoint.HasValue || !CanInteract) return;

            switch (m_ActivePower)
            {
                case Power.MOLD_TERRAIN:
                    if (CameraDetectionZone.Instance.VisibleTeamObjects <= 0) return;
                    GameController.Instance.MoldTerrain_ServerRpc(m_NearestPoint.Value, lower: false);
                    break;

                case Power.GUIDE_FOLLOWERS:
                    if (m_NearestPoint.Value.Y <= Terrain.Instance.WaterLevel && !m_NearestPoint.Value.IsOnShore) return;
                    GameController.Instance.MoveFlag_ServerRpc(m_NearestPoint.Value, Team);
                    break;

                case Power.EARTHQUAKE:
                    GameController.Instance.Earthquake_ServerRpc(m_NearestPoint.Value);
                    break;

                case Power.SWAMP:
                    GameController.Instance.CreateSwamp_ServerRpc(m_NearestPoint.Value);
                    break;

                case Power.VOLCANO:
                    GameController.Instance.Volcano_ServerRpc(m_NearestPoint.Value);
                    break;
            }

            // after any power is executed, set the active power back to Mold Terrain
            SetActivePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Executes all events triggered by a right click, if the player's cursor is near a clickable point.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (!context.performed || m_ActivePower != Power.MOLD_TERRAIN || !m_NearestPoint.HasValue) return;
            GameController.Instance.MoldTerrain_ServerRpc(m_NearestPoint.Value, lower: true);
        }


        #region Camera Movement Inputs

        /// <summary>
        /// Triggers the position movement of the camera.
        /// </summary>
        /// <param name="context">Data about the input action which triggered this event.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;
            CameraController.Instance.MovementDirection = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Triggers the clockwise rotation of the camera around the point it is looking at.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRotateCameraClockwise(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;

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
            if (m_IsPaused) return;

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
        {
            if (m_IsPaused) return;
            CameraController.Instance.ZoomDirection = (int)context.ReadValue<float>();
        }

        #endregion


        #region Influence Behavior Inputs

        /// <summary>
        /// Activates the Go To Flag unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGoToFlagSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            SetUnitBehavior(UnitBehavior.GO_TO_SYMBOL);
        }

        /// <summary>
        /// Activates the Settle unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSettleSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            SetUnitBehavior(UnitBehavior.SETTLE);
        }

        /// <summary>
        /// Activates the Gather unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGatherSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            SetUnitBehavior(UnitBehavior.GATHER);
        }

        /// <summary>
        /// Activates the FIGHT unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            SetUnitBehavior(UnitBehavior.FIGHT);
        }

        #endregion


        #region Zoom Inputs

        /// <summary>
        /// Triggers camera to show the object this player is focusing on.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFocusedObject(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowFocusedObject_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.FOCUSED_OBJECT);
        }

        /// <summary>
        /// Triggers camera to show the player's team's symbol.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFlag(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowTeamSymbol_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.SYMBOL);
        }

        /// <summary>
        /// Triggers camera to show the player's team's leader.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowLeader(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowLeader_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.LEADER);
        }

        /// <summary>
        /// Triggers camera to show the player's team's settlements.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowSettlements(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowSettlements_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.SETTLEMENT);
        }

        /// <summary>
        /// Triggers camera to show the fights currenly happening..
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFights(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowFights_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.FIGHT);
        }

        /// <summary>
        /// Triggers camera to show the player's team's knights.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowKnights(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            GameController.Instance.ShowKnights_ServerRpc(Team);
            GameUI.Instance.ClickCameraSnap(CameraSnap.KNIGHT);
        }

        #endregion


        #region Powers Inputs

        /// <summary>
        /// Activates the Mold Terrain power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMoldTerrainSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Activates the Guide Followers power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGuideFollowersSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.GUIDE_FOLLOWERS);
        }

        /// <summary>
        /// Activates the Earthquake power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnEarthquakeSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.EARTHQUAKE);
        }

        /// <summary>
        /// Activates the Swamp power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSwampSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.SWAMP);
        }

        /// <summary>
        /// Activates the KNIGHT power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnKnightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.KNIGHT);
        }

        /// <summary>
        /// Activates the Volcano power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnVolcanoSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.VOLCANO);
        }

        /// <summary>
        /// Activates the Flood power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFloodSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.FLOOD);
        }

        /// <summary>
        /// Activates the Armagheddon power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnArmagheddonSelected(InputAction.CallbackContext context)
        {
            if (!context.performed || !CanInteract) return;
            TryActivatePower(Power.ARMAGHEDDON);
        }

        #endregion

        #endregion
    }
}