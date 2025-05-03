using Unity.Netcode;
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

        [Header("Markers")]
        [Tooltip("The color of the marker when the Divine Intervention can be used.")]
        [SerializeField] private Color m_HighlightMarkerColor;
        [Tooltip("The color of the marker when the Divine Intervention cannot be used.")]
        [SerializeField] private Color m_GrayedOutMarkerColor;
        [Tooltip("The marker objects corresponding to each Divine Intervention. The index of each marker in this array corresponds to the value of its corresponding Divine Intervention in the DivineIntervention enum.")]
        [SerializeField] private GameObject[] m_Markers;
        [Tooltip("An increase to the height at which a marker sits to make it not clip into the terrain.")]
        [SerializeField] private float m_MarkerExtraHeight = 2;

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
        /// Gets the faction this player controls, or NONE if the player hasn't been set.
        /// </summary>
        public Faction Faction { get => m_PlayerInfo.HasValue ? m_PlayerInfo.Value.Faction : Faction.NONE; }

        /// <summary>
        /// True if the game is paused, false otherwise.
        /// </summary>
        private bool m_IsPaused;
        /// <summary>
        /// True if the Armageddon Divine Intervention has been activated, false otherwise.
        /// </summary>
        private bool m_IsArmageddonActive = false;

        private bool m_IsQueryModeActive;
        /// <summary>
        /// True if the game is in Inspect Mode, false otherwise.
        /// </summary>
        public bool IsQueryModeActive { get => m_IsQueryModeActive; }
        
        /// <summary>
        /// True if the player can activate Divine Interventions and Influence Behavior actions, false otherwise.
        /// </summary>
        private bool CanUseActions { get => m_PlayerInfo.HasValue && !m_IsPaused && !m_IsQueryModeActive && !m_IsArmageddonActive; }

        /// <summary>
        /// The <c>TerrainPoint</c> closest to the player's cursor. Null if the player's cursor is outside the bounds of the terrain
        /// </summary>
        private TerrainPoint? m_NearestPoint = null;
        /// <summary>
        /// Gets the player's current active Divine Intervention.
        /// </summary>
        private DivineIntervention m_ActiveDivineIntervention = DivineIntervention.MOLD_TERRAIN;
        /// <summary>
        /// Gets the current active behavior of the units in the player's faction.
        /// </summary>
        private UnitBehavior m_ActiveBehavior = UnitBehavior.SETTLE;
        /// <summary>
        /// The index of the Divine Intervention marker that's currently active.
        /// </summary>
        private int m_ActiveMarkerIndex = 0;

        #endregion


        #region Event Functions

        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start() 
        {
            m_PlayerInfo = GameData.Instance.GetPlayerInfoByNetworkId(NetworkManager.Singleton.LocalClientId);
            SetupMarkers();
        }

        private void Update()
        {
            if (!CanUseActions) return;

            m_NearestPoint = GetNearestPoint();
            if (!m_NearestPoint.HasValue) return;
            PlaceMarkerAtPoint(m_NearestPoint.Value);
        }

        #endregion


        #region Game Flow

        /// <summary>
        /// Pauses or unpauses the game, based on the given boolean parameter.
        /// </summary>
        /// <param name="pause">True if the game should be paused, false otherwise.</param>
        public void SetPause(bool pause)
        {
            m_IsPaused = pause;
            Time.timeScale = m_IsPaused ? 0 : 1;
            PauseMenu.Instance.TogglePauseMenu(show: m_IsPaused);
        }

        /// <summary>
        /// Pauses the game and shows the end game menu.
        /// </summary>
        /// <param name="winner">The <c>Faction</c> that won the game.</param>
        public void EndGame(Faction winner)
        {
            m_IsPaused = true;
            EndGameUI.Instance.ShowEndGameUI(winner);
        }

        #endregion


        #region Divine Interventions

        /// <summary>
        /// Checks with the server if the given Divine Intervention can be activated for this player.
        /// </summary>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> that the player wants to activate.</param>
        public void TryActivateDivineIntervention(DivineIntervention divineIntervention)
        {
            // Don't call the server unnecessarily when a Divine Intervention is selected multiple times,
            // except for the Knight and Flood Divine Interventions, which have effects on each selection.
            if (!CanUseActions || (divineIntervention == m_ActiveDivineIntervention && 
                divineIntervention != DivineIntervention.KNIGHT && divineIntervention != DivineIntervention.FLOOD)) 
                return;

            DivineInterventionController.Instance.TryActivateDivineIntervention_ServerRpc(Faction, divineIntervention);
        }

        /// <summary>
        /// Recieves information whether the given Divine Intervention has been activated or not and acts accordingly.
        /// </summary>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> the player wanted to activate.</param>
        /// <param name="isActivated">True if the Divine Intervention has been activated, false otherwise.</param>
        public void ReceiveInterventionActivationInfo(DivineIntervention divineIntervention, bool isActivated)
        {
            if (!isActivated)
            {
                GameUI.Instance.ShowNotEnoughManna(divineIntervention);
                SetActiveDivineIntervention(DivineIntervention.MOLD_TERRAIN);
                return;
            }

            // these two were activated instantaneously
            if (divineIntervention == DivineIntervention.KNIGHT || divineIntervention == DivineIntervention.FLOOD)
                divineIntervention = DivineIntervention.MOLD_TERRAIN;

            SetActiveDivineIntervention(divineIntervention);
        }

        /// <summary>
        /// Sets a new currently active Divine Intervention.
        /// </summary>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> that should be set.</param>
        private void SetActiveDivineIntervention(DivineIntervention divineIntervention)
        {
            SwitchActiveMarker(divineIntervention);
            GameUI.Instance.SetActiveDivineInterventionIcon(currentIntervention: divineIntervention, lastIntervention: m_ActiveDivineIntervention);

            m_ActiveDivineIntervention = divineIntervention;
        }

        /// <summary>
        /// Sets the IsArmageddon flag to active.
        /// </summary>
        public void ActivateArmageddon() => m_IsArmageddonActive = true;

        #region Markers

        /// <summary>
        /// Sizes the markers according to the size of the areas of their respective Divine Interventions.
        /// </summary>
        private void SetupMarkers()
        {
            foreach (GameObject marker in m_Markers)
                marker.GetComponent<MeshRenderer>().material.color = m_GrayedOutMarkerColor;

            GameUtils.ResizeGameObject(m_Markers[(int)DivineIntervention.EARTHQUAKE], 2 * DivineInterventionController.Instance.EarthquakeRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)DivineIntervention.SWAMP], 2 * DivineInterventionController.Instance.SwampRadius * Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_Markers[(int)DivineIntervention.VOLCANO], 2 * DivineInterventionController.Instance.VolcanoRadius * Terrain.Instance.UnitsPerTileSide);
        }

        /// <summary>
        /// Finds the closest <c>TerrainPoint</c> to the cursor.
        /// </summary>
        /// <returns>The nearest <c>TerrainPoint</c> to the cursor, or null if none is found.</returns>
        private TerrainPoint? GetNearestPoint()
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit,
                maxDistance: Mathf.Infinity, layerMask: LayerMask.GetMask(LayerData.TERRAIN_LAYER_NAME)))
                return new(hit.point);

            return null;
        }

        /// <summary>
        /// Sets the marker's posiiton to the given <c>TerrainPoint</c> and changes its color to show whether the Divine Intervention can be executed.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> the marker should be set at.</param>
        private void PlaceMarkerAtPoint(TerrainPoint point)
        {
            m_Markers[m_ActiveMarkerIndex].transform.position = point.ToScenePosition() + new Vector3(0, m_MarkerExtraHeight, 0);

            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color =
                (m_ActiveDivineIntervention == DivineIntervention.MOLD_TERRAIN && CameraDetectionZone.Instance.VisibleObjectsAmount <= 0) ||
                (m_ActiveDivineIntervention == DivineIntervention.PLACE_MAGNET && m_NearestPoint.Value.IsUnderwater())
                ? m_GrayedOutMarkerColor
                : m_HighlightMarkerColor;
        }

        /// <summary>
        /// Sets the active marker to the marker of the given Divine Intervention.
        /// </summary>
        /// <param name="newDivineIntervention">The <c>DivineIntervention</c> whose marker should be activated.</param>
        private void SwitchActiveMarker(DivineIntervention newDivineIntervention)
        {
            if (m_ActiveDivineIntervention == newDivineIntervention || newDivineIntervention == DivineIntervention.KNIGHT ||
                newDivineIntervention == DivineIntervention.FLOOD || newDivineIntervention == DivineIntervention.ARMAGEDDON)
                return;

            SetCurrentMarker(isActive: false);
            m_ActiveMarkerIndex = (int)newDivineIntervention;
            SetCurrentMarker(isActive: true);
        }

        /// <summary>
        /// Activates or deactivates the current marker.
        /// </summary>
        /// <param name="isActive">True if the marker should be activated, false otherwise.</param>
        private void SetCurrentMarker(bool isActive) => m_Markers[m_ActiveMarkerIndex].SetActive(isActive);

        #endregion

        #endregion


        #region Other Player Actions

        /// <summary>
        /// Tells the server to set the behavior of the units of this player's faction to the given behavior.
        /// </summary>
        /// <param name="behavior">The new behavior that should be applied to all the units.</param>
        public void SetUnitBehavior(UnitBehavior behavior)
        {
            if (!CanUseActions || behavior == m_ActiveBehavior) return;

            GameUI.Instance.SetActiveBehaviorIcon(currentBehavior: behavior, lastBehavior: m_ActiveBehavior);
            m_ActiveBehavior = behavior;
            UnitManager.Instance.ChangeUnitBehavior_ServerRpc(Faction, m_ActiveBehavior);
        }

        /// <summary>
        /// Tells the server to snap the camera to the position of an object, depending on the given <c>SnapTo</c> enum value.
        /// </summary>
        /// <param name="cameraSnap">The <c>SnapTo</c> enum value represents the object that the camera should move to.</param>
        public void SnapCameraToObject(SnapTo cameraSnap)
        {
            if (!CanUseActions) return;

            switch (cameraSnap)
            {
                case SnapTo.INSPECTED_OBJECT:
                    SnapToObjectController.Instance.SnapToInspectedObject_ServerRpc(Faction);
                    break;

                case SnapTo.UNIT_MAGNET:
                    SnapToObjectController.Instance.SnapToUnitMagnet_ServerRpc(Faction);
                    break;

                case SnapTo.LEADER:
                    SnapToObjectController.Instance.SnapToLeader_ServerRpc(Faction);
                    break;

                case SnapTo.SETTLEMENT:
                    SnapToObjectController.Instance.SnapToSettlements_ServerRpc(Faction);
                    break;

                case SnapTo.FIGHT:
                    SnapToObjectController.Instance.SnapToFights_ServerRpc(Faction);
                    break;

                case SnapTo.KNIGHT:
                    SnapToObjectController.Instance.SnapToKnights_ServerRpc(Faction);
                    break;
            }

            GameUI.Instance.SimulateClickOnSnapIcon(cameraSnap);
        }

        /// <summary>
        /// Toggles the Query Mode from on to off and vice versa, and passes that state to the server.
        /// </summary>
        public void SetQueryMode(bool isActive) 
        {
            if (m_IsArmageddonActive) return;

            m_IsQueryModeActive = isActive;
            GameUI.Instance.SetQueryIcon(m_IsQueryModeActive);
            SetCurrentMarker(isActive: !m_IsQueryModeActive);
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
            GameController.Instance.SetPause_ServerRpc(!m_IsPaused);
        }

        /// <summary>
        /// Executes the effects of the current active Divine Intervention that trigger on a left mouse click.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnLeftClick(InputAction.CallbackContext context)
        {
            if (!context.performed || GameUI.Instance.IsPointerOnUI || !CanUseActions || !m_NearestPoint.HasValue) 
                return;

            switch (m_ActiveDivineIntervention)
            {
                case DivineIntervention.MOLD_TERRAIN:
                    if (m_NearestPoint.Value.IsAtMaxHeight() || CameraDetectionZone.Instance.VisibleObjectsAmount <= 0) return;
                    DivineInterventionController.Instance.MoldTerrain_ServerRpc(m_NearestPoint.Value, lower: false);
                    break;

                case DivineIntervention.PLACE_MAGNET:
                    if (m_NearestPoint.Value.IsUnderwater()) return;
                    DivineInterventionController.Instance.PlaceUnitMagnet_ServerRpc(Faction, m_NearestPoint.Value);
                    break;

                case DivineIntervention.EARTHQUAKE:
                    DivineInterventionController.Instance.CreateEarthquake_ServerRpc(Faction, m_NearestPoint.Value);
                    break;

                case DivineIntervention.SWAMP:
                    DivineInterventionController.Instance.CreateSwamp_ServerRpc(Faction, m_NearestPoint.Value);
                    break;

                case DivineIntervention.VOLCANO:
                    DivineInterventionController.Instance.CreateVolcano_ServerRpc(Faction, m_NearestPoint.Value);
                    break;
            }

            // after any Divine Intervention is executed, set the active Divine Intervention back to Mold Terrain
            SetActiveDivineIntervention(DivineIntervention.MOLD_TERRAIN);
        }

        /// <summary>
        /// Executes the effects of the current active Divine Intervention that trigger on a right mouse click.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (!context.performed || GameUI.Instance.IsPointerOnUI || !CanUseActions || 
                m_ActiveDivineIntervention != DivineIntervention.MOLD_TERRAIN || !m_NearestPoint.HasValue || 
                CameraDetectionZone.Instance.VisibleObjectsAmount <= 0 || m_NearestPoint.Value.GetHeight() <= Terrain.Instance.WaterLevel)
                return;

            DivineInterventionController.Instance.MoldTerrain_ServerRpc(m_NearestPoint.Value, lower: true);
        }

        /// <summary>
        /// Triggers the toggling of the Inspect Mode.
        /// </summary>
        /// <param name="context"></param>
        public void OnQueryToggled(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetQueryMode(!m_IsQueryModeActive);
        }


        #region Camera Movement Inputs

        /// <summary>
        /// Triggers the movement of the camera.
        /// </summary>
        /// <param name="context">Data about the input action which triggered this event.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;
            PlayerCamera.Instance.MovementDirection = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Triggers the clockwise rotation of the camera around the point it is looking at.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRotateCameraClockwise(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;

            if (context.performed)
                PlayerCamera.Instance.RotationDirection = 1;

            if (context.canceled)
                PlayerCamera.Instance.RotationDirection = 0;
        }

        /// <summary>
        /// Triggers the counter-clockwise rotation of the camera around the point it is looking at.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRotateCameraCounterclockwise(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;

            if (context.performed)
                PlayerCamera.Instance.RotationDirection = -1;

            if (context.canceled)
                PlayerCamera.Instance.RotationDirection = 0;
        }

        /// <summary>
        /// Triggers the zoom in or out of the camera, focused on the point it is looking at.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnZoomCamera(InputAction.CallbackContext context)
        {
            if (m_IsPaused) return;
            PlayerCamera.Instance.ZoomDirection = (int)context.ReadValue<float>();
        }

        #endregion


        #region Influence Behavior Inputs

        /// <summary>
        /// Activates the Go To Magnet unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnGoToMagnetSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.GO_TO_MAGNET);
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
        /// Activates the FIGHT unit behavior.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetUnitBehavior(UnitBehavior.FIGHT);
        }

        #endregion


        #region Camera Snap Inputs

        /// <summary>
        /// Triggers camera to show the object this player is inspecting.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowInspectedObject(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.INSPECTED_OBJECT);
        }

        /// <summary>
        /// Triggers camera to show the player's unit magnet.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowMagnet(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.UNIT_MAGNET);
        }

        /// <summary>
        /// Triggers camera to show the player's faction's leader.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowLeader(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.LEADER);
        }

        /// <summary>
        /// Triggers camera to show the player's faction's settlements.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowSettlements(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.SETTLEMENT);
        }

        /// <summary>
        /// Triggers camera to show the fights currenly happening.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.FIGHT);
        }

        /// <summary>
        /// Triggers camera to show the player's faction's knights.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowKnights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCameraToObject(SnapTo.KNIGHT);
        }

        #endregion


        #region Divine Intervention Inputs

        /// <summary>
        /// Activates the Mold Terrain Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMoldTerrainSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetActiveDivineIntervention(DivineIntervention.MOLD_TERRAIN);
        }

        /// <summary>
        /// Activates the Place Unit Magnet Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnPlaceUnitMagnet(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.PLACE_MAGNET);
        }

        /// <summary>
        /// Activates the Earthquake Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnEarthquakeSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.EARTHQUAKE);
        }

        /// <summary>
        /// Activates the Swamp Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSwampSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.SWAMP);
        }

        /// <summary>
        /// Activates the Knight Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnKnightSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.KNIGHT);
        }

        /// <summary>
        /// Activates the Volcano Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnVolcanoSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.VOLCANO);
        }

        /// <summary>
        /// Activates the Flood Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnFloodSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.FLOOD);
        }

        /// <summary>
        /// Activates the Armagheddon Divine Intervention.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnArmagheddonSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            TryActivateDivineIntervention(DivineIntervention.ARMAGEDDON);
        }

        #endregion

        #endregion
    }
}