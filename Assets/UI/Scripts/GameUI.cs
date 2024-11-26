using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.InputSystem;
using System;

namespace Populous
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Slider m_HealthBar;
        [SerializeField] private Image m_HealthBarFill;

        [SerializeField] private Slider[] m_PopulationBars;

        [SerializeField] private Slider m_MannaBar;
        [SerializeField] private Button[] m_PowerIcons;
        [SerializeField] private Button[] m_BehaviorIcons;
        [SerializeField] private Button[] m_CameraSnapIcons;

        private static GameUI m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static GameUI Instance { get => m_Instance; }

        private int m_MaxPopulation = -1;
        private int m_MaxManna = -1;
        private int m_MaxUnitStrength = -1;


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


        #region Unit UI

        public void ToggleUnitUI(bool show, int maxStrength, int currentStrength, Color teamColor, Vector3 worldPosition)
        {
            if (!show)
            {
                m_HealthBar.transform.DOScale(0, 0.25f);
                return;
            }

            m_HealthBar.value = currentStrength / maxStrength;
            m_HealthBarFill.color = teamColor;
            m_HealthBar.transform.position = Camera.main.WorldToScreenPoint(worldPosition);
            m_HealthBar.transform.DOScale(1, 0.25f);
        }

        public void UpdateUnitUI(int maxStrength, int currentStrength) => m_HealthBar.value = currentStrength / maxStrength;

        public void ToggleFightUI(bool show, int redStrength, int blueStrength)
        {

        }

        public void UpdateFightUI(int redStrength, int blueStrength)
        {

        }

        #endregion


        #region Settlement UI

        public void ToggleSettlementUI(bool show, int settlementIndex, int maxCapacity, int currentCapacity, Color teamColor)
        {

        }

        public void UpdateSettlementUI(int settlementIndex, int maxCapacity, int currentCapacity)
        {

        }

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
        /// Activates the Knight power.
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
        /// Activates the Fight unit behavior.
        /// </summary>
        public void OnFightClicked()
        {
            if (!PlayerController.Instance.InputEnabled) return;
            PlayerController.Instance.SetUnitBehavior(UnitBehavior.FIGHT);
        }

        #endregion


        #region Zoom Inputs

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
    }
}