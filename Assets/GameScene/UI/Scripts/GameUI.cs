using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// The <c>GameUI</c> class controls the behavior of the UI overlayed over the Gameplay Scene.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The images the player avatars should be placed on, where index 0 is the image for the red faction, and index 1 is the image for the blue faction.")]
        [SerializeField] private RawImage[] m_PlayerAvatars;
        [Tooltip("The sliders for the population bars, where index 0 is the slider for the red faction, and index 1 is the slider for the blue faction.")]
        [SerializeField] private Slider[] m_PopulationBars;
        [Tooltip("The slider for the manna bar.")]
        [SerializeField] private Slider m_MannaBar;


        [Header("Player Action Icons")]
        [SerializeField] private Image m_QueryIcon;
        [SerializeField] private Image[] m_BehaviorIcons;
        [SerializeField] private Image[] m_CameraSnapIcons;
        [SerializeField] private Button[] m_DivineInterventionIcons;

        [Header("Minimap")]
        [SerializeField] private RectTransform m_Minimap;

        [Header("Tooltip")]
        [SerializeField] private RectTransform m_TooltipBox;
        [SerializeField] private TMP_Text m_TooltipText;

        [Header("Inspected Unit")]
        [SerializeField] private GameObject m_InspectedUnitPanel;
        [SerializeField] private Image[] m_UnitImages;
        [SerializeField] private TMP_Text m_UnitTypeText;
        [SerializeField] private Slider m_UnitStrengthSlider;

        [Header("Inspected Settlement")]
        [SerializeField] private GameObject m_InspectedSettlementPanel;
        [SerializeField] private Image[] m_SettlementImages;
        [SerializeField] private Slider m_SettlementTypeSlider;
        [SerializeField] private Slider m_SettlementFollowersSlider;

        [Header("Inspected Fight")]
        [SerializeField] private GameObject m_InspectedFightPanel;
        [SerializeField] private Slider m_RedUnitStrengthBar;
        [SerializeField] private Slider m_BlueUnitStrengthBar;

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
        /// The maximum number of followers that can be in the currently inspected settlement.
        /// </summary>
        private int m_InspectedSettlementCapacity = -1;

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
                bar.value = (float)UnitManager.Instance.StartingUnits / UnitManager.Instance.MaxFollowersInFaction;

            UpdateMannaBar(0, 0);
            SetActiveDivineInterventionIcon(DivineIntervention.MOLD_TERRAIN, DivineIntervention.MOLD_TERRAIN);
            SetActiveBehaviorIcon(UnitBehavior.SETTLE, UnitBehavior.SETTLE);

            // TODO: uncomment
            //PlayerInfo? redPlayerInfo = GameData.Instance.GetPlayerInfoByFaction(Faction.RED);
            //PlayerInfo? bluePlayerInfo = GameData.Instance.GetPlayerInfoByFaction(Faction.BLUE);

            //if (redPlayerInfo.HasValue)
            //    m_PlayerAvatars[0].texture = await InterfaceUtils.GetSteamAvatar(redPlayerInfo.Value.SteamId);

            //if (bluePlayerInfo.HasValue)
            //    m_PlayerAvatars[1].texture = await InterfaceUtils.GetSteamAvatar(bluePlayerInfo.Value.SteamId);
        }

        #endregion


        #region Cursor Functions

        /// <summary>
        /// Sets whether the cursor is over an element of the UI or not.
        /// </summary>
        /// <remarks>Used to differentiate between clicking in the game and clicking on the UI.</remarks>
        /// <param name="isPointerOnUI">True if the cursor is over a UI element, false otherwise.</param>
        public void SetIsPointerOnUI(bool isPointerOnUI) => m_IsPointerOnUI = isPointerOnUI;

        #endregion


        #region Population Bars

        /// <summary>
        /// Updates the slider position of the population slider for the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose population bar should be updated.</param>
        /// <param name="currentPopulation">The amount of population the given faction has.</param>
        public void UpdatePopulationBar(Faction faction, int currentPopulation)
            => m_PopulationBars[(int)faction].value = (float)currentPopulation / UnitManager.Instance.MaxFollowersInFaction;

        #endregion


        #region Behavior Icons

        /// <summary>
        /// Lights up the icon of the currently active unit behavior.
        /// </summary>
        /// <param name="currentBehavior">The currently active <c>UnitBehavior</c>.</param>
        /// <param name="lastBehavior">The previously active <c>UnitBehavior</c></param>
        public void SetActiveBehaviorIcon(UnitBehavior currentBehavior, UnitBehavior lastBehavior)
        {
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)lastBehavior], isActive: false);
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)currentBehavior], isActive: true);
        }

        #endregion


        #region Snap To Icons

        /// <summary>
        /// Flashes the icon green, making it look as if it has been clicked.
        /// </summary>
        /// <remarks>Used when the Snap To actions are triggered via keyboard.</remarks>
        /// <param name="snapOption">The <c>SnapTo</c> value corresponding to the performed action.</param>
        public void SimulateClickOnSnapIcon(SnapTo snapOption)
            => InterfaceUtils.ClickIcon(m_CameraSnapIcons[(int)snapOption]);

        /// <summary>
        /// Flashes the icon red, notifying that the action wasn't performed.
        /// </summary>
        /// <param name="snapOption">The <c>SnapTo</c> value corresponding to the performed action.</param>
        public void NotifyCannotSnapTo(SnapTo snapOption)
            => InterfaceUtils.FlashWrong(m_CameraSnapIcons[(int)snapOption]);

        #endregion


        #region Manna Bar

        /// <summary>
        /// Updates the fill of the manna bar and enables the icons of the available Divine Interventions, based on the current amount of manna.
        /// </summary>
        /// <param name="currentManna">The current amount of manna.</param>
        /// <param name="activeDivineIntervention">The number of Divine Interventions that are available to the player, in order.</param>
        public void UpdateMannaBar(int currentManna, int activeDivineIntervention) 
        {
            m_MannaBar.value = (float)currentManna / DivineInterventionsController.Instance.MaxManna;

            for (int i = 0; i < m_DivineInterventionIcons.Length; ++i)
                m_DivineInterventionIcons[i].interactable = i <= activeDivineIntervention;
        }

        /// <summary>
        /// Flashes the icon of the given Divine Intervention red, notifying the player that they don't have enough manna to execute it.
        /// </summary>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> the player wants to execute.</param>
        public void ShowNotEnoughManna(DivineIntervention divineIntervention)
            => InterfaceUtils.FlashWrong(m_DivineInterventionIcons[(int)divineIntervention].GetComponent<Image>());

        /// <summary>
        /// Lights up the icon of the currently active Divine Intervention.
        /// </summary>
        /// <param name="currentIntervention">The currently active <c>DivineIntervention</c>.</param>
        /// <param name="lastIntervention">The previously active <c>DivineIntervention</c>.</param>
        public void SetActiveDivineInterventionIcon(DivineIntervention currentIntervention, DivineIntervention lastIntervention)
        {
            InterfaceUtils.ShowActiveIcon(m_DivineInterventionIcons[(int)lastIntervention].GetComponent<Image>(), isActive: false);
            InterfaceUtils.ShowActiveIcon(m_DivineInterventionIcons[(int)currentIntervention].GetComponent<Image>(), isActive: true);
        }

        #endregion


        #region Tooltips

        /// <summary>
        /// Creates a tooltip with the given message near the player's cursor.
        /// </summary>
        /// <param name="message">The message that should be shown on the tooltip.</param>
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

        /// <summary>
        /// Disables the tooltip.
        /// </summary>
        public void HideTooltip() => m_TooltipBox.gameObject.SetActive(false);

        #endregion


        #region Minimap

        /// <summary>
        /// Finds the position on the terrain that corresponds to the position clicked on the minimap and moves the player camera there.
        /// </summary>
        public void OnMinimapClicked()
        {
            Vector2 minimapBottomLeft = (Vector2)m_Minimap.position - new Vector2(m_Minimap.rect.width / 2, m_Minimap.rect.height / 2);
            float scale = Terrain.Instance.UnitsPerSide / m_Minimap.rect.width;
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            PlayerCamera.Instance.SetCameraLookPosition(new Vector3(mousePosition.x - minimapBottomLeft.x, 0, mousePosition.y - minimapBottomLeft.y) * scale);
        }

        #endregion


        #region Inspect

        /// <summary>
        /// Either lights up the Query icon green or returns it to normal, depending on if Query Mode is active.
        /// </summary>
        /// <param name="isActive">True if the icon should be lit up, false otherwise.</param>
        public void SetQueryIcon(bool isActive) => InterfaceUtils.ShowActiveIcon(m_QueryIcon, isActive);


        #region Show/Hide

        /// <summary>
        /// Hides all the Inpsect Object panels.
        /// </summary>
        public void HideInspectedObjectPanel()
        {
            m_InspectedUnitPanel.SetActive(false);
            if (m_CurrentlyShownUnitImage >= 0)
            {
                m_UnitImages[m_CurrentlyShownUnitImage].gameObject.SetActive(false);
                m_CurrentlyShownUnitImage = -1;
            }

            m_InspectedSettlementPanel.SetActive(false);
            if (m_CurrentlyShownSettlementImage >= 0)
            {
                m_SettlementImages[m_CurrentlyShownSettlementImage].gameObject.SetActive(false);
                m_CurrentlyShownSettlementImage = -1;
            }

            m_InspectedFightPanel.SetActive(false);
        }

        /// <summary>
        /// Shows the Inspect Unit panel containing the given unit data.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit belongs to.</param>
        /// <param name="type">The <c>UnitType</c> of the unit./param>
        /// <param name="strength">The current strength of the unit.</param>
        public void ShowUnitData(Faction faction, UnitType type, int strength)
        {
            // if there's an active panel, hide it.
            HideInspectedObjectPanel();

            // show the red unit image if the unit is from the red faction, or the blue unit image if it is from the blue faction
            m_CurrentlyShownUnitImage = (int)faction;
            m_UnitImages[m_CurrentlyShownUnitImage].gameObject.SetActive(true);

            UpdateUnitType(type);
            UpdateUnitStrength(strength);

            m_InspectedUnitPanel.SetActive(true);
        }

        /// <summary>
        /// Shows the Inspect Settlement panel containing the given settlement data.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the settlement belongs to.</param>
        /// <param name="type">The <c>SettlementType</c> of the settlement.</param>
        /// <param name="followers">The number of followers currently in the settlement.</param>
        /// <param name="settlementCapacity">The maximum number of followers that can be in the settlement.</param>
        public void ShowSettlementData(Faction faction, SettlementType type, int followers, int settlementCapacity)
        {
            HideInspectedObjectPanel();

            UpdateSettlementFaction(faction);
            UpdateSettlementType(type, settlementCapacity);
            UpdateSettlementFollowers(followers);

            m_InspectedSettlementPanel.SetActive(true);
        }

        /// <summary>
        /// Shows the Inspect Fight panel containing the given strengths of the participating units.
        /// </summary>
        /// <param name="redStrength">The strength of the red unit in the fight.</param>
        /// <param name="blueStrength">The strength of the blue unit in the fight.</param>
        public void ShowFightData(int redStrength, int blueStrength)
        {
            HideInspectedObjectPanel();

            UpdateFight(redStrength, blueStrength);

            m_InspectedFightPanel.SetActive(true);
        }

        #endregion


        #region Update

        /// <summary>
        /// Displays the given unit type on the Inspect Unit panel.
        /// </summary>
        /// <param name="type">The <c>UnitType</c> that should be shown.</param>
        public void UpdateUnitType(UnitType type) => m_UnitTypeText.text = type.ToString().ToUpper();

        /// <summary>
        /// Displays the given unit strength on the Inpsect Unit panel.
        /// </summary>
        /// <param name="strength">The amount of strength that should be shown.</param>
        public void UpdateUnitStrength(int strength)
            => m_UnitStrengthSlider.value = (float)strength / UnitManager.Instance.MaxFollowersInFaction;


        /// <summary>
        /// Displays the settlement image for the settlement belonging to the given faction on the Inspect Settlement panel.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the settlement belongs to.</param>
        public void UpdateSettlementFaction(Faction faction)
        {
            m_CurrentlyShownSettlementImage = (int)faction;
            m_SettlementImages[m_CurrentlyShownSettlementImage].gameObject.SetActive(true);
        }

        /// <summary>
        /// Displays the given settlement type on the Inspect Settlement panel.
        /// </summary>
        /// <param name="type">The <c>SettlementType</c> that should be displayed.</param>
        /// <param name="settlementCapacity">The maximum number of followers that can be in the settlement of the given type.</param>
        public void UpdateSettlementType(SettlementType type, int settlementCapacity)
        {
            m_SettlementTypeSlider.value = (float)type / Enum.GetValues(typeof(SettlementType)).Length;
            m_InspectedSettlementCapacity = settlementCapacity;
        }

        /// <summary>
        /// Displays the given number of followers in the settlement on the Inspect Settlement panel.
        /// </summary>
        /// <param name="followers">The number of followers currently in the settlement.</param>
        public void UpdateSettlementFollowers(int followers)
            => m_SettlementFollowersSlider.value = (float)followers / m_InspectedSettlementCapacity;

        /// <summary>
        /// Displays the given strengths for the red and blue unit participating in the fight on the Inspected Fight panel.
        /// </summary>
        /// <param name="redStrength">The strength of the red unit in the fight.</param>
        /// <param name="blueStrength">The strength of the blue unit in the fight.</param>
        public void UpdateFight(int redStrength, int blueStrength)
        {
            m_RedUnitStrengthBar.value = (float)redStrength / UnitManager.Instance.MaxFollowersInFaction;
            m_BlueUnitStrengthBar.value = (float)blueStrength / UnitManager.Instance.MaxFollowersInFaction;
        }

        #endregion

        #endregion


        #region Button Clicks

        /// <summary>
        /// Activates the Query action if it is inactive, and vice versa.
        /// </summary>
        public void OnQueryClicked() => PlayerController.Instance.ToggleQueryMode();


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
        public void OnShowInspectedObjectClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.INSPECTED_OBJECT);

        /// <summary>
        /// Triggers camera to show the player's unit magnet.
        /// </summary>
        public void OnShowMagnetClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.UNIT_MAGNET);

        /// <summary>
        /// Triggers camera to show the player's faction's leader.
        /// </summary>
        public void OnShowLeaderClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.LEADER);

        /// <summary>
        /// Triggers camera to show the player's faction's settlements.
        /// </summary>
        public void OnShowSettlementsClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.SETTLEMENT);

        /// <summary>
        /// Triggers camera to show the fights currenly happening.
        /// </summary>
        public void OnShowFightsClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.FIGHT);

        /// <summary>
        /// Triggers camera to show the player's faction's knights.
        /// </summary>
        public void OnShowKnightsClicked() => PlayerController.Instance.SnapCameraToObject(SnapTo.KNIGHT);

        #endregion


        #region Divine Intervention Buttons

        /// <summary>
        /// Activates the Mold Terrain Divine Intervention.
        /// </summary>
        public void OnMoldTerrainClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.MOLD_TERRAIN);

        /// <summary>
        /// Activates the Place Unit Magnet Divine Intervention.
        /// </summary>
        public void OnPlaceUnitMagnetClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.PLACE_MAGNET);

        /// <summary>
        /// Activates the Earthquake Divine Intervention.
        /// </summary>
        public void OnEarthquakeClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.EARTHQUAKE);

        /// <summary>
        /// Activates the Swamp Divine Intervention.
        /// </summary>
        public void OnSwampClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.SWAMP);

        /// <summary>
        /// Activates the KNIGHT Divine Intervention.
        /// </summary>
        public void OnKnightClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.KNIGHT);

        /// <summary>
        /// Activates the Volcano Divine Intervention.
        /// </summary>
        public void OnVolcanoClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.VOLCANO);

        /// <summary>
        /// Activates the Flood Divine Intervention.
        /// </summary>
        public void OnFloodClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.FLOOD);

        /// <summary>
        /// Activates the Armagheddon Divine Intervention.
        /// </summary>
        public void OnArmagheddonClicked() => PlayerController.Instance.TryActivateDivineIntervention(DivineIntervention.ARMAGEDDON);

        #endregion

        #endregion
    }
}