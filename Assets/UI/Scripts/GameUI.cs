using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace Populous
{
    public class GameUI : MonoBehaviour
    {
        #region Inspector Fields

        [SerializeField] private Image m_InspectIcon;
        [SerializeField] private Slider m_MannaBar;
        [SerializeField] private Slider[] m_PopulationBars;
        [SerializeField] private RawImage[] m_PlayerAvatars;
        [SerializeField] private Image[] m_BehaviorIcons;
        [SerializeField] private Image[] m_CameraSnapIcons;
        [SerializeField] private Button[] m_PowerIcons;

        [Header("Tooltip")]
        [SerializeField] private RectTransform m_TooltipBox;
        [SerializeField] private TMP_Text m_TooltipText;

        [Header("Inspected Unit")]
        [SerializeField] private GameObject m_UnitDataScreen;
        [SerializeField] private Image[] m_UnitTeamImages;
        [SerializeField] private TMP_Text m_UnitClassText;
        [SerializeField] private Slider m_UnitStrengthSlider;

        [Header("Inspected Settlement")]
        [SerializeField] private GameObject m_SettlementDataScreen;
        [SerializeField] private Image[] m_SettlementTeamImages;
        [SerializeField] private Slider m_SettlementTypeSlider;
        [SerializeField] private Slider m_SettlementFollowersSlider;


        [Header("Minimap")]
        [SerializeField] private RectTransform m_Minimap;
        [SerializeField] private Renderer m_MinimapRenderer;
        [SerializeField] private int m_MinimapIconScale;
        [SerializeField] private Color[] m_MinimapUnitColors;
        [SerializeField] private Color[] m_MinimapSettlementColors;

        #endregion


        #region Class Fields

        private static GameUI m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static GameUI Instance { get => m_Instance; }

        private bool m_IsPointerOnUI;
        /// <summary>
        /// True if the mouse cursor is over a UI element, false otherwise.
        /// </summary>
        public bool IsPointerOnUI { get => m_IsPointerOnUI; }

        /// <summary>
        /// True if Inspect Mode is active, false otherwise.
        /// </summary>
        private bool m_IsInspectIconActive;
        /// <summary>
        /// The index of the currently visible image at the top of the Inspect Unit panel. 
        /// </summary>
        /// <remark>0 means the red unit image is shown, 1 means the blue unit image is shown, and -1 means no image is shown.</remark>
        private int m_CurrentlyShownUnitImage = -1;
        /// <summary>
        /// The index of the currently visible image at the top of the Inspect Settlement panel. 
        /// </summary>
        /// <remark>0 means the red settlement image is shown, 1 means the blue settlement image is shown, and -1 means no image is shown.</remark>
        private int m_CurrentlyShownSettlementImage = -1;
        /// <summary>
        /// 
        /// </summary>
        private int m_MaxUnitsInInspectedSettlement = -1;



        public int MinimapIconScale { get => m_MinimapIconScale; }
        public Color[] MinimapUnitColors { get => m_MinimapUnitColors; }
        public Color[] MinimapSettlementColors { get => m_MinimapSettlementColors; }




        private Texture2D m_MinimapTexture;



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

        private async void Start()
        {
            foreach (var bar in m_PopulationBars)
                bar.value = (float)UnitManager.Instance.StartingUnits / UnitManager.Instance.MaxPopulation;

            UpdateMannaBar(0, 0);
            SetActivePowerIcon(Power.MOLD_TERRAIN, Power.MOLD_TERRAIN);
            SetActiveBehaviorIcon(UnitBehavior.SETTLE, UnitBehavior.SETTLE);


            // TODO: uncomment
            //PlayerInfo? redPlayerInfo = GameData.Instance.GetPlayerInfoByTeam(Team.RED);
            //PlayerInfo? bluePlayerInfo = GameData.Instance.GetPlayerInfoByTeam(Team.BLUE);

            //if (redPlayerInfo.HasValue)
            //    m_PlayerAvatars[0].texture = await InterfaceUtils.GetSteamAvatar(redPlayerInfo.Value.SteamId);

            //if (bluePlayerInfo.HasValue)
            //    m_PlayerAvatars[1].texture = await InterfaceUtils.GetSteamAvatar(bluePlayerInfo.Value.SteamId);
        }

        #endregion


        #region UI Functions

        /// <summary>
        /// Sets whether the cursor is above an element of the UI or not.
        /// </summary>
        /// <remarks>Used to differentiate between clicking in the game and clicking on the UI.</remarks>
        /// <param name="isPointerOnUI">True if the cursor is over a UI element, false otherwise.</param>
        public void SetIsPointerOnUI(bool isPointerOnUI) => m_IsPointerOnUI = isPointerOnUI;

        #endregion


        #region Population Bars

        /// <summary>
        /// Updates the slider position of the population slider for the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose population bar should be updated.</param>
        /// <param name="currentPopulation">The amount of population the given team has.</param>
        public void UpdatePopulationBar(Team team, int currentPopulation)
            => m_PopulationBars[(int)team].value = (float)currentPopulation / UnitManager.Instance.MaxPopulation;

        #endregion


        #region Manna Bar

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentManna">The current amount of manna.</param>
        /// <param name="activePowers"></param>
        public void UpdateMannaBar(int currentManna, int activePowers) 
        {
            m_MannaBar.value = (float)currentManna / GameController.Instance.MaxManna;

            for (int i = 0; i < m_PowerIcons.Length; ++i)
                m_PowerIcons[i].interactable = i <= activePowers;
        }

        /// <summary>
        /// Signals to the player that they don't have enough manna to execute the given power.
        /// </summary>
        /// <param name="power">The power the player wants to execute.</param>
        public void ShowNotEnoughManna(Power power)
            => InterfaceUtils.FlashWrong(m_PowerIcons[(int)power].GetComponent<Image>());

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentPower">The currently activated power.</param>
        /// <param name="lastPower">The previously activated power.</param>
        public void SetActivePowerIcon(Power currentPower, Power lastPower)
        {
            InterfaceUtils.ShowActiveIcon(m_PowerIcons[(int)lastPower].GetComponent<Image>(), isActive: false);
            InterfaceUtils.ShowActiveIcon(m_PowerIcons[(int)currentPower].GetComponent<Image>(), isActive: true);
        }

        #endregion


        #region Behavior Icons

        public void SetActiveBehaviorIcon(UnitBehavior currentBehavior, UnitBehavior lastBehavior)
        {
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)lastBehavior], isActive: false);
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)currentBehavior], isActive: true);
        }

        #endregion


        #region Camera Snap Icons

        public void SimulateClickCameraSnapIcon(CameraSnap snapOption)
            => InterfaceUtils.ClickIcon(m_CameraSnapIcons[(int)snapOption]);

        public void NotifyCannotSnapCamera(CameraSnap snapOption)
            => InterfaceUtils.FlashWrong(m_CameraSnapIcons[(int)snapOption]);

        #endregion


        #region Tooltips

        public void ShowTooltip(string message)
        {
            m_TooltipText.text = message;
            m_TooltipBox.sizeDelta = new Vector2(m_TooltipText.preferredWidth > 400 ? 400 : m_TooltipText.preferredWidth, m_TooltipText.preferredHeight);

            Vector3 mousePosition = Mouse.current.position.ReadValue();

            if (mousePosition.x - m_TooltipBox.sizeDelta.x / 2 < 0)
                m_TooltipBox.transform.position = new Vector2(mousePosition.x + m_TooltipBox.sizeDelta.x / 2, mousePosition.y);
            else if (mousePosition.x + m_TooltipBox.sizeDelta.x / 2 > Screen.width)
                m_TooltipBox.transform.position = new Vector2(mousePosition.x - m_TooltipBox.sizeDelta.x / 2, mousePosition.y);
            else
                m_TooltipBox.transform.position = new Vector2(mousePosition.x, mousePosition.y + 50);
            m_TooltipBox.gameObject.SetActive(true);
        }

        public void HideTooltip() => m_TooltipBox.gameObject.SetActive(false);

        #endregion


        #region Minimap

        public void SetInitialMinimapTexture()
        {
            m_MinimapTexture = new(Terrain.Instance.TilesPerSide + 1, Terrain.Instance.TilesPerSide + 1);

            Color32[] colors = new Color32[m_MinimapTexture.width * m_MinimapTexture.height];

            for (int z = 0; z <= Terrain.Instance.TilesPerSide; ++z)
                for (int x = 0; x <= Terrain.Instance.TilesPerSide; ++x)
                    colors[z * m_MinimapTexture.width + x] = Terrain.Instance.GetPointHeight((x, z)) > Terrain.Instance.WaterLevel ? Color.green : Color.blue;

            m_MinimapTexture.filterMode = FilterMode.Point;
            m_MinimapTexture.wrapMode = TextureWrapMode.Clamp;
            m_MinimapTexture.SetPixels32(colors);
            m_MinimapTexture.Apply();

            m_MinimapRenderer.sharedMaterial.mainTexture = m_MinimapTexture;
            m_MinimapRenderer.transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, 0, Terrain.Instance.UnitsPerSide / 2);
            GameUtils.ResizeGameObject(m_MinimapRenderer.gameObject, Terrain.Instance.UnitsPerSide);
        }

        public void UpdateMinimapTexture()
        {
            Color32[] colors = new Color32[m_MinimapTexture.width * m_MinimapTexture.height];

            for (int i = 0; i < 6; ++i)
                colors[i] = Color.white;

            m_MinimapTexture.SetPixels32(0, 0, 5, 5, colors);
            m_MinimapTexture.Apply();
        }


        public void OnMinimapClicked()
        {
            Vector2 minimapBottomLeft = (Vector2)m_Minimap.position - new Vector2(m_Minimap.rect.width / 2, m_Minimap.rect.height / 2);
            float scale = Terrain.Instance.UnitsPerSide / m_Minimap.rect.width;
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector2 positionOnTerrain = new Vector2(mousePosition.x - minimapBottomLeft.x, mousePosition.y - minimapBottomLeft.y) * scale;

            TerrainPoint closestPoint = new(positionOnTerrain.x, positionOnTerrain.y, getClosestPoint: true);
            CameraController.Instance.SetCameraLookPosition(closestPoint.ToWorldPosition());
        }

        #endregion


        #region Inspect

        public void ShowUnitData(Team team, UnitClass unitClass, int strength)
        {
            HideInspectedObjectPanel();

            if (m_CurrentlyShownUnitImage >= 0)
            {
                m_UnitTeamImages[m_CurrentlyShownUnitImage].gameObject.SetActive(false);
                m_CurrentlyShownUnitImage = -1;
            }

            m_UnitTeamImages[(int)team].gameObject.SetActive(true);
            m_CurrentlyShownUnitImage = (int)team;
            UpdateUnitClass(unitClass);
            UpdateUnitStrength(strength);

            m_UnitDataScreen.SetActive(true);
        }

        public void ShowSettlementData(Team team, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement)
        {
            HideInspectedObjectPanel();

            if (m_CurrentlyShownSettlementImage >= 0)
            {
                m_SettlementTeamImages[m_CurrentlyShownSettlementImage].gameObject.SetActive(false);
                m_CurrentlyShownSettlementImage = -1;
            }

            UpdateSettlementTeam(team);
            UpdateSettlementType(type, maxUnitsInSettlement);
            UpdateSettlementFollowers(unitsInSettlement);

            m_SettlementDataScreen.SetActive(true);
        }

        public void ShowFightData(int redStrength, int blueStrength)
        {

        }

        public void HideInspectedObjectPanel()
        {
            m_UnitDataScreen.SetActive(false);
            m_SettlementDataScreen.SetActive(false);
        }


        public void UpdateUnitClass(UnitClass unitClass)
            => m_UnitClassText.text = unitClass.ToString().ToUpper();

        public void UpdateUnitStrength(int strength)
            => m_UnitStrengthSlider.value = (float)strength / UnitManager.Instance.MaxUnitStrength;

        public void UpdateSettlementTeam(Team team)
        {
            m_SettlementTeamImages[(int)team].gameObject.SetActive(true);
            m_CurrentlyShownSettlementImage = (int)team;
        }

        public void UpdateSettlementType(SettlementType type, int maxUnitsInSettlement)
        {
            m_SettlementTypeSlider.value = (float)type / Enum.GetValues(typeof(SettlementType)).Length;
            m_MaxUnitsInInspectedSettlement = maxUnitsInSettlement;
        }

        public void UpdateSettlementFollowers(int unitsInSettlement)
            => m_SettlementFollowersSlider.value = (float)unitsInSettlement / m_MaxUnitsInInspectedSettlement;

        public void UpdateFight(Team team, int strength)
        {

        }

        #endregion


        #region Button Clicks

        /// <summary>
        /// Activates inspect mode if it is inactive, and vice versa.
        /// </summary>
        public void OnInspectClicked()
        {
            m_IsInspectIconActive = !m_IsInspectIconActive;
            InterfaceUtils.ShowActiveIcon(m_InspectIcon, m_IsInspectIconActive);
            PlayerController.Instance.ToggleInspectMode();
        }

        #region Influence Behavior Buttons

        /// <summary>
        /// Activates the Go To Magnet unit behavior.
        /// </summary>
        public void OnGoToMagnetClicked() => PlayerController.Instance.SetUnitBehavior(UnitBehavior.GO_TO_MAGNET);

        /// <summary>
        /// Activates the Settle unit behavior.
        /// </summary>
        public void OnSettleClicked() => PlayerController.Instance.SetUnitBehavior(UnitBehavior.SETTLE);

        /// <summary>
        /// Activates the Gather unit behavior.
        /// </summary>
        public void OnGatherClicked() => PlayerController.Instance.SetUnitBehavior(UnitBehavior.GATHER);

        /// <summary>
        /// Activates the FIGHT unit behavior.
        /// </summary>
        public void OnFightClicked() => PlayerController.Instance.SetUnitBehavior(UnitBehavior.FIGHT);

        #endregion


        #region Zoom Inputs

        /// <summary>
        /// Triggers camera to show the object the player is inspecting.
        /// </summary>
        public void OnShowInspectedObjectClicked() => PlayerController.Instance.SnapCamera(CameraSnap.INSPECTED_OBJECT);

        /// <summary>
        /// Triggers camera to show the player's team's symbol.
        /// </summary>
        public void OnShowMagnetClicked() => PlayerController.Instance.SnapCamera(CameraSnap.MAGNET);

        /// <summary>
        /// Triggers camera to show the player's team's leader.
        /// </summary>
        public void OnShowLeaderClicked() => PlayerController.Instance.SnapCamera(CameraSnap.LEADER);

        /// <summary>
        /// Triggers camera to show the player's team's settlements.
        /// </summary>
        public void OnShowSettlementsClicked() => PlayerController.Instance.SnapCamera(CameraSnap.SETTLEMENT);

        /// <summary>
        /// Triggers camera to show the fights currenly happening..
        /// </summary>
        public void OnShowFightsClicked() => PlayerController.Instance.SnapCamera(CameraSnap.FIGHT);

        /// <summary>
        /// Triggers camera to show the player's team's knights.
        /// </summary>
        public void OnShowKnightsClicked() => PlayerController.Instance.SnapCamera(CameraSnap.KNIGHT);

        #endregion


        #region Powers Buttons

        /// <summary>
        /// Activates the Mold Terrain power.
        /// </summary>
        public void OnMoldTerrainClicked() => PlayerController.Instance.TryActivatePower(Power.MOLD_TERRAIN);

        /// <summary>
        /// Activates the Guide Followers power.
        /// </summary>
        public void OnGuideFollowersClicked() => PlayerController.Instance.TryActivatePower(Power.MOVE_MAGNET);

        /// <summary>
        /// Activates the Earthquake power.
        /// </summary>
        public void OnEarthquakeClicked() => PlayerController.Instance.TryActivatePower(Power.EARTHQUAKE);

        /// <summary>
        /// Activates the Swamp power.
        /// </summary>
        public void OnSwampClicked() => PlayerController.Instance.TryActivatePower(Power.SWAMP);

        /// <summary>
        /// Activates the KNIGHT power.
        /// </summary>
        public void OnKnightClicked() => PlayerController.Instance.TryActivatePower(Power.KNIGHT);

        /// <summary>
        /// Activates the Volcano power.
        /// </summary>
        public void OnVolcanoClicked() => PlayerController.Instance.TryActivatePower(Power.VOLCANO);

        /// <summary>
        /// Activates the Flood power.
        /// </summary>
        public void OnFloodClicked() => PlayerController.Instance.TryActivatePower(Power.FLOOD);

        /// <summary>
        /// Activates the Armagheddon power.
        /// </summary>
        public void OnArmagheddonClicked() => PlayerController.Instance.TryActivatePower(Power.ARMAGHEDDON);

        #endregion


        #endregion

    }
}