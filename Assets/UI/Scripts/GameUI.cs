using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Populous
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Slider m_HealthBar;
        [SerializeField] private Image m_HealthBarFill;

        [SerializeField] private Slider[] m_PopulationBars;

        [SerializeField] private Slider m_MannaBar;

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


        #region Population Bars

        public void UpdatePopulationBar(Team team, int currentPopulation)
            => m_PopulationBars[(int)team].value = currentPopulation / UnitManager.Instance.MaxPopulation;

        #endregion


        #region Manna Bar

        public void UpdateMannaBar(int currentManna, int activePowers) 
        {
            //m_MannaBar.value = currentManna / GameController.Instance.MaxManna;
        
        }
        
        public void NotEnoughManna(Power power)
        {

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
    }
}