using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Populous
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Slider m_HealthBar;
        [SerializeField] private Image m_HealthBarFill;

        private static GameUI m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static GameUI Instance { get => m_Instance; }


        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        #endregion


        #region HealthBar

        public void ToggleHealthBar(bool show, int maxHealth, int currentHealth, Color teamColor, Vector3 worldPosition)
        {
            if (!show)
            {
                m_HealthBar.transform.DOScale(0, 0.25f);
                return;
            }

            m_HealthBar.value = currentHealth / maxHealth;
            m_HealthBarFill.color = teamColor;
            m_HealthBar.transform.position = Camera.main.WorldToScreenPoint(worldPosition);
            m_HealthBar.transform.DOScale(1, 0.25f);
        }

        public void UpdateHealthBar(int maxHealth, int currentHealth)
        {
            m_HealthBar.value = currentHealth / maxHealth;
        }

        #endregion
    }
}