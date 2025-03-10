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

        [Tooltip("The color of the marker when the power can be used.")]
        [SerializeField] private Color m_HighlightMarkerColor;

        [Tooltip("The color of the marker when the power cannot be used.")]
        [SerializeField] private Color m_GrayedOutMarkerColor;

        [Tooltip("The marker objects corresponding to each power. The index of each marker in this array corresponds to the value of its corresponding power in the Power enum.")]
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
        /// Gets the team this player controls, or Team.NONE if the player hasn't been set.
        /// </summary>
        public Faction Faction { get => m_PlayerInfo.HasValue ? m_PlayerInfo.Value.Faction : Faction.NONE; }
        /// <summary>
        /// Gets the network ID of this player, or ulong.MaxValue if the player hasn't been set.
        /// </summary>
        public ulong NetworkId { get => m_PlayerInfo.HasValue ? m_PlayerInfo.Value.NetworkId : ulong.MaxValue; }

        /// <summary>
        /// True if the game is paused, false otherwise.
        /// </summary>
        private bool m_IsPaused;
        /// <summary>
        /// True if the game is in Inspect Mode, false otherwise.
        /// </summary>
        private bool m_IsInspectActive;
        public bool IsInspectActive { get => m_IsInspectActive; }
        /// <summary>
        /// True if the player's input should be processed, false otherwise.
        /// </summary>
        private bool CanInteract { get => m_PlayerInfo.HasValue && !m_IsPaused; }
        /// <summary>
        /// True if the player can activate powers, false otherwise.
        /// </summary>
        private bool CanUseActions { get => CanInteract && !m_IsInspectActive && m_ActivePower != Power.ARMAGEDDON; }

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
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start() 
        {
            m_PlayerInfo = new(0, 0, Faction.RED); //GameData.Instance.GetPlayerInfoByNetworkId(NetworkManager.Singleton.LocalClientId);
            SetupMarkers();
        }

        private void Update()
        {
            if (!CanUseActions || m_ActivePower == Power.KNIGHT || m_ActivePower == Power.FLOOD)
                return;

            m_NearestPoint = GetNearestPoint();

            if (m_NearestPoint.HasValue)
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
        /// <param name="winner">The <c>Team</c> that won the game.</param>
        public void EndGame(Faction winner)
        {
            m_IsPaused = true;
            Time.timeScale = 0;
            EndGameUI.Instance.ShowEndGameUI(winner);
        }

        #endregion


        #region Units

        /// <summary>
        /// Sets the behavior of the units of this player's team to the given behavior.
        /// </summary>
        /// <param name="behavior">The new behavior that should be applied to all the units.</param>
        public void SetUnitBehavior(UnitBehavior behavior)
        {
            if (!CanUseActions || behavior == m_ActiveBehavior) 
                return;

            GameUI.Instance.SetActiveBehaviorIcon(currentBehavior: behavior, lastBehavior: m_ActiveBehavior);
            m_ActiveBehavior = behavior;
            UnitManager.Instance.ChangeUnitBehavior_ServerRpc(Faction, m_ActiveBehavior);
        }

        #endregion


        #region Camera Snap

        public void SnapCamera(SnapTo cameraSnap)
        {
            if (!CanUseActions) return;

            switch (cameraSnap)
            {
                case SnapTo.INSPECTED_OBJECT:
                    GameController.Instance.ShowInspectedObject_ServerRpc(Faction);
                    break;

                case SnapTo.UNIT_MAGNET:
                    GameController.Instance.ShowMagnet_ServerRpc(Faction);
                    break;

                case SnapTo.LEADER:
                    GameController.Instance.ShowLeader_ServerRpc(Faction);
                    break;

                case SnapTo.SETTLEMENT:
                    GameController.Instance.ShowSettlements_ServerRpc(Faction);
                    break;

                case SnapTo.FIGHT:
                    GameController.Instance.ShowFights_ServerRpc(Faction);
                    break;

                case SnapTo.KNIGHT:
                    GameController.Instance.ShowKnights_ServerRpc(Faction);
                    break;
            }

            GameUI.Instance.SimulateClickOnSnapIcon(cameraSnap);
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
            if (!CanUseActions || (power == m_ActivePower && power != Power.KNIGHT && power != Power.FLOOD)) 
                return;

            GameController.Instance.TryActivatePower_ServerRpc(Faction, power);
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
            SwitchActiveMarker(power);
            GameUI.Instance.SetActivePowerIcon(currentPower: power, lastPower: m_ActivePower);

            m_ActivePower = power;
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
                maxDistance: Mathf.Infinity, layerMask: LayerMask.GetMask(LayerData.TERRAIN_LAYER_NAME)))
                return new(hit.point);
           
            return null;
        }

        /// <summary>
        /// Sets the marker's posiiton to the nearest <c>Terrainpoint</c> and changes its color to show whether the power can be executed.
        /// </summary>
        private void PlaceMarkerAtPoint(TerrainPoint point)
        {
            m_Markers[m_ActiveMarkerIndex].transform.position = point.ToScenePosition() + new Vector3(0, m_MarkerExtraHeight, 0);

            m_Markers[m_ActiveMarkerIndex].GetComponent<MeshRenderer>().material.color =
                (m_ActivePower == Power.MOLD_TERRAIN && CameraDetectionZone.Instance.VisibleObjectsAmount <= 0) ||
                (m_ActivePower == Power.PLACE_MAGNET && m_NearestPoint.Value.IsUnderwater())
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
                m_ActivePower == Power.FLOOD || m_ActivePower == Power.ARMAGEDDON)
                return;

            m_Markers[m_ActiveMarkerIndex].SetActive(false);
            m_ActiveMarkerIndex = (int)newPower;
            m_Markers[m_ActiveMarkerIndex].SetActive(true);
        }

        #endregion


        #region Inspect Mode

        /// <summary>
        /// Toggles the Inspect Mode from on to off and vice versa, and passes that state to the server.
        /// </summary>
        public void ToggleQuery() 
        { 
            m_IsInspectActive = !m_IsInspectActive;
            GameUI.Instance.SetQueryIcon(m_IsInspectActive);
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
        /// Executes the effects of the current active power that trigger on a left mouse click.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnLeftClick(InputAction.CallbackContext context)
        {
            if (!context.performed || GameUI.Instance.IsPointerOnUI || !CanUseActions || !m_NearestPoint.HasValue) 
                return;

            switch (m_ActivePower)
            {
                case Power.MOLD_TERRAIN:
                    if (m_NearestPoint.Value.IsAtMaxHeight() || CameraDetectionZone.Instance.VisibleObjectsAmount <= 0) 
                        return;
                    
                    GameController.Instance.MoldTerrain_ServerRpc(Faction, m_NearestPoint.Value, lower: false);
                    break;

                case Power.PLACE_MAGNET:
                    if (m_NearestPoint.Value.IsUnderwater()) return;
                    TerrainTile tile = new(m_NearestPoint.Value.X, m_NearestPoint.Value.Z);
                    StructureManager.Instance.CreateSettlement(tile, Faction.RED);
                    //GameController.Instance.PlaceUnitMagnet_ServerRpc(Team, m_NearestPoint.Value);
                    break;

                case Power.EARTHQUAKE:
                    TerrainTile tileA = new(m_NearestPoint.Value.X, m_NearestPoint.Value.Z);
                    StructureManager.Instance.CreateSettlement(tileA, Faction.BLUE);

                    //GameController.Instance.CreateEarthquake_ServerRpc(Team, m_NearestPoint.Value);
                    break;

                case Power.SWAMP:
                    TerrainTile tileB = new(m_NearestPoint.Value.X, m_NearestPoint.Value.Z);
                    Settlement settlement = (Settlement)tileB.GetStructure();
                    settlement.BurnDown();
                    //GameController.Instance.CreateSwamp_ServerRpc(Faction,m_NearestPoint.Value);
                    break;

                case Power.VOLCANO:
                    GameController.Instance.CreateVolcano_ServerRpc(Faction, m_NearestPoint.Value);
                    break;
            }

            // after any power is executed, set the active power back to Mold Terrain
            SetActivePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Executes the effects of the current active power that trigger on a right mouse click.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnRightClick(InputAction.CallbackContext context)
        {            
            if (!context.performed || GameUI.Instance.IsPointerOnUI || !CanUseActions || m_ActivePower != Power.MOLD_TERRAIN ||
                !m_NearestPoint.HasValue || CameraDetectionZone.Instance.VisibleObjectsAmount <= 0 || 
                m_NearestPoint.Value.GetHeight() <= Terrain.Instance.WaterLevel)
                return;

            GameController.Instance.MoldTerrain_ServerRpc(Faction, m_NearestPoint.Value, lower: true);
        }

        /// <summary>
        /// Triggers the toggling of the Inspect Mode.
        /// </summary>
        /// <param name="context"></param>
        public void OnInspectActivated(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            ToggleQuery();
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
            SnapCamera(SnapTo.INSPECTED_OBJECT);
        }

        /// <summary>
        /// Triggers camera to show the player's unit magnet.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowMagnet(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCamera(SnapTo.UNIT_MAGNET);
        }

        /// <summary>
        /// Triggers camera to show the player's team's leader.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowLeader(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCamera(SnapTo.LEADER);
        }

        /// <summary>
        /// Triggers camera to show the player's team's settlements.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowSettlements(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCamera(SnapTo.SETTLEMENT);
        }

        /// <summary>
        /// Triggers camera to show the fights currenly happening.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowFights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCamera(SnapTo.FIGHT);
        }

        /// <summary>
        /// Triggers camera to show the player's team's knights.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnShowKnights(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SnapCamera(SnapTo.KNIGHT);
        }

        #endregion


        #region Powers Inputs

        /// <summary>
        /// Activates the Mold Terrain power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnMoldTerrainSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetActivePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Activates the Place Unit Magnet power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnPlaceUnitMagnet(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetActivePower(Power.PLACE_MAGNET);
            //TryActivatePower(Power.PLACE_MAGNET);
        }

        /// <summary>
        /// Activates the Earthquake power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnEarthquakeSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetActivePower(Power.EARTHQUAKE);

            //TryActivatePower(Power.EARTHQUAKE);
        }

        /// <summary>
        /// Activates the Swamp power.
        /// </summary>
        /// <param name="context">Details about the input action which triggered this event.</param>
        public void OnSwampSelected(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            SetActivePower(Power.SWAMP);

            //TryActivatePower(Power.SWAMP);
        }

        /// <summary>
        /// Activates the KNIGHT power.
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
            TryActivatePower(Power.ARMAGEDDON);
        }

        #endregion

        #endregion
    }
}