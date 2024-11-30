using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using UnityEngine.InputSystem;

namespace Populous
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Slider[] m_PopulationBars;
        [SerializeField] private Slider m_MannaBar;
        [SerializeField] private Button[] m_PowerIcons;
        [SerializeField] private Button[] m_BehaviorIcons;
        [SerializeField] private Button[] m_CameraSnapIcons;

        [Header("Focused Unit")]
        [SerializeField] private GameObject m_UnitDataScreen;
        [SerializeField] private Image[] m_UnitTeamImages;
        [SerializeField] private TMP_Text m_UnitClassText;
        [SerializeField] private Slider m_UnitStrengthSlider;

        [Header("Focused Settlement")]
        [SerializeField] private GameObject m_SettlementDataScreen;
        [SerializeField] private Image[] m_SettlementTeamImages;
        [SerializeField] private Slider m_SettlementTypeSlider;
        [SerializeField] private Slider m_SettlementFollowersSlider;

        [Header("Tooltip")]
        [SerializeField] private RectTransform m_TooltipBox;
        [SerializeField] private TMP_Text m_TooltipText;

        [Header("Minimap")]
        [SerializeField] private RectTransform m_Minimap;
        [SerializeField] private int m_MinimapIconScale;
        [SerializeField] private Color[] m_MinimapUnitColors;
        [SerializeField] private Color[] m_MinimapSettlementColors;

        public int MinimapIconScale { get => m_MinimapIconScale; }
        public Color[] MinimapUnitColors { get => m_MinimapUnitColors; }
        public Color[] MinimapSettlementColors { get => m_MinimapSettlementColors; }


        private static GameUI m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static GameUI Instance { get => m_Instance; }

        private bool m_IsPointerOnUI;
        public bool IsPointerOnUI { get => m_IsPointerOnUI; }

        private int m_MaxPopulation = -1;
        private int m_MaxManna = -1;
        private int m_MaxUnitStrength = -1;

        private int m_CurrentlyShownUnitImage = -1;
        private int m_CurrentlyShownSettlementImage = -1;
        private int m_MaxUnitsInFocusedSettlement = -1;



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        #endregion


        public void Setup(int maxPopulation, int maxManna, int maxUnitStrength, int startingUnits)
        {
            m_MaxPopulation = maxPopulation;
            m_MaxManna = maxManna;
            m_MaxUnitStrength = maxUnitStrength;

            foreach (var bar in m_PopulationBars)
                bar.value = (float)startingUnits / maxPopulation;

            SetActiveBehaviorIcon(UnitBehavior.SETTLE, UnitBehavior.SETTLE);

            UpdateMannaBar(0, 0);
            SetActivePowerIcon(Power.MOLD_TERRAIN, Power.MOLD_TERRAIN);
        }


        #region Population Bars

        public void UpdatePopulationBar(Team team, int currentPopulation)
            => m_PopulationBars[(int)team].value = (float)currentPopulation / m_MaxPopulation;

        #endregion


        #region Manna Bar

        public void UpdateMannaBar(int currentManna, int activePowers) 
        {
            m_MannaBar.value = (float)currentManna / m_MaxManna;

            for (int i = 0; i < m_PowerIcons.Length; ++i)
                m_PowerIcons[i].interactable = i <= activePowers;
        }

        public void NotEnoughManna(Power power)
            => InterfaceUtils.FlashWrong(m_PowerIcons[(int)power].GetComponent<Image>());

        public void SetActivePowerIcon(Power currentPower, Power lastPower)
        {
            InterfaceUtils.ShowActiveIcon(m_PowerIcons[(int)lastPower].GetComponent<Image>(), isActive: false);
            InterfaceUtils.ShowActiveIcon(m_PowerIcons[(int)currentPower].GetComponent<Image>(), isActive: true);
        }

        #endregion


        #region Focused Data


        public void ShowFocusedUnit(Team team, UnitClass unitClass, int strength)
        {
            HideFocusedData();

            if (m_CurrentlyShownUnitImage >= 0)
            {
                m_UnitTeamImages[m_CurrentlyShownUnitImage].gameObject.SetActive(false);
                m_CurrentlyShownUnitImage = -1;
            }

            m_UnitTeamImages[(int)team].gameObject.SetActive(true);
            m_CurrentlyShownUnitImage = (int)team;
            UpdateFocusedUnitClass(unitClass);
            UpdateFocusedUnitStrength(strength);

            m_UnitDataScreen.SetActive(true);
        }

        public void ShowFocusedSettlement(Team team, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement)
        {
            HideFocusedData();

            if (m_CurrentlyShownSettlementImage >= 0)
            {
                m_SettlementTeamImages[m_CurrentlyShownSettlementImage].gameObject.SetActive(false);
                m_CurrentlyShownSettlementImage = -1;
            }

            UpdateFocusedSettlementTeam(team);
            UpdateFocusedSettlementType(type, maxUnitsInSettlement);
            UpdateFocusedSettlementFollowers(unitsInSettlement);

            m_SettlementDataScreen.SetActive(true);
        }

        public void HideFocusedData()
        {
            m_UnitDataScreen.SetActive(false);
            m_SettlementDataScreen.SetActive(false);
        }


        public void UpdateFocusedUnitClass(UnitClass unitClass)
            => m_UnitClassText.text = unitClass.ToString().ToUpper();

        public void UpdateFocusedUnitStrength(int strength)
            => m_UnitStrengthSlider.value = (float)strength / m_MaxUnitStrength;

        public void UpdateFocusedSettlementTeam(Team team)
        {
            m_SettlementTeamImages[(int)team].gameObject.SetActive(true);
            m_CurrentlyShownSettlementImage = (int)team;
        }

        public void UpdateFocusedSettlementType(SettlementType type, int maxUnitsInSettlement)
        {
            m_SettlementTypeSlider.value = (float)type / Enum.GetValues(typeof(SettlementType)).Length;
            m_MaxUnitsInFocusedSettlement = maxUnitsInSettlement;
        }

        public void UpdateFocusedSettlementFollowers(int unitsInSettlement)
            => m_SettlementFollowersSlider.value = (float)unitsInSettlement / m_MaxUnitsInFocusedSettlement;

        #endregion



        public void SetActiveBehaviorIcon(UnitBehavior currentBehavior, UnitBehavior lastBehavior)
        {
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)lastBehavior].GetComponent<Image>(), isActive: false);
            InterfaceUtils.ShowActiveIcon(m_BehaviorIcons[(int)currentBehavior].GetComponent<Image>(), isActive: true);
        }

        public void CannotSnapToOption(CameraSnap snapOption)
            => InterfaceUtils.FlashWrong(m_CameraSnapIcons[(int)snapOption].GetComponent<Image>());

        public void ClickCameraSnap(CameraSnap snapOption)
            => InterfaceUtils.ClickIcon(m_CameraSnapIcons[(int)snapOption].GetComponent<Image>());


        #region Button Clicks

        #region Powers Inputs

        /// <summary>
        /// Activates the Mold Terrain power.
        /// </summary>
        public void OnMoldTerrainClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.MOLD_TERRAIN);
        }

        /// <summary>
        /// Activates the Guide Followers power.
        /// </summary>
        public void OnGuideFollowersClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.GUIDE_FOLLOWERS);
        }

        /// <summary>
        /// Activates the Earthquake power.
        /// </summary>
        public void OnEarthquakeClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.EARTHQUAKE);
        }

        /// <summary>
        /// Activates the Swamp power.
        /// </summary>
        public void OnSwampClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.SWAMP);
        }

        /// <summary>
        /// Activates the KNIGHT power.
        /// </summary>
        public void OnKnightClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.KNIGHT);
        }

        /// <summary>
        /// Activates the Volcano power.
        /// </summary>
        public void OnVolcanoClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.VOLCANO);
        }

        /// <summary>
        /// Activates the Flood power.
        /// </summary>
        public void OnFloodClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.FLOOD);
        }

        /// <summary>
        /// Activates the Armagheddon power.
        /// </summary>
        public void OnArmagheddonClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.TryActivatePower(Power.ARMAGHEDDON);
        }

        #endregion


        #region Influence Behavior Inputs

        /// <summary>
        /// Activates the Go To Flag unit behavior.
        /// </summary>
        public void OnGoToFlagClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.SetUnitBehavior(UnitBehavior.GO_TO_SYMBOL);
        }

        /// <summary>
        /// Activates the Settle unit behavior.
        /// </summary>
        public void OnSettleClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.SetUnitBehavior(UnitBehavior.SETTLE);
        }

        /// <summary>
        /// Activates the Gather unit behavior.
        /// </summary>
        public void OnGatherClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.SetUnitBehavior(UnitBehavior.GATHER);
        }

        /// <summary>
        /// Activates the FIGHT unit behavior.
        /// </summary>
        public void OnFightClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.SetUnitBehavior(UnitBehavior.FIGHT);
        }

        #endregion


        #region Zoom Inputs

        /// <summary>
        /// Triggers camera to show the object the player is focusing on.
        /// </summary>
        public void OnShowFocusedObjectClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowFocusedObjectServerRpc(PlayerController.Instance.Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's symbol.
        /// </summary>
        public void OnShowFlagClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowTeamSymbolServerRpc(PlayerController.Instance.Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's leader.
        /// </summary>
        public void OnShowLeaderClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowLeaderServerRpc(PlayerController.Instance.Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's settlements.
        /// </summary>
        public void OnShowSettlementsClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowSettlementsServerRpc(PlayerController.Instance.Team);
        }

        /// <summary>
        /// Triggers camera to show the fights currenly happening..
        /// </summary>
        public void OnShowFightsClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowFightsServerRpc(PlayerController.Instance.Team);
        }

        /// <summary>
        /// Triggers camera to show the player's team's knights.
        /// </summary>
        public void OnShowKnightsClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            GameController.Instance.ShowKnightsServerRpc(PlayerController.Instance.Team);
        }

        #endregion


        #endregion


        public void SetIsPointerOnUI(bool isOnUI) => m_IsPointerOnUI = isOnUI;

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

        public void OnMinimapClicked()
        {
            Vector2 minimapBottomLeft = (Vector2)m_Minimap.position - new Vector2(m_Minimap.rect.width / 2, m_Minimap.rect.height / 2);
            float scale = Terrain.Instance.UnitsPerSide / m_Minimap.rect.width;
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector2 positionOnTerrain = new Vector2(mousePosition.x - minimapBottomLeft.x, mousePosition.y - minimapBottomLeft.y) * scale;

            MapPoint closestPoint = new(positionOnTerrain.x, positionOnTerrain.y, getClosestPoint: true);
            CameraController.Instance.SetCameraLookPosition(closestPoint.ToWorldPosition());
        }
    }
}