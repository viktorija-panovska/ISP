using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// The <c>PauseMenu</c> class controls the behavior of the pause menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The UI canvas that the menu is created on.")]
        [SerializeField] private GameObject m_MenuCanvas;

        [Tooltip("The textbox that should contain the game seed.")]
        [SerializeField] private TMP_Text m_GameSeedField;

        [Tooltip("All the buttons in the menu.")]
        [SerializeField] private Button[] m_Buttons;

        #endregion

        private static PauseMenu m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static PauseMenu Instance { get => m_Instance; }


        #region Event Functions

        private void Awake()
        {
            if (m_Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start()
        {
            m_GameSeedField.text = GameData.Instance ? GameData.Instance.MapSeed.ToString() : "";

            foreach (Button button in m_Buttons)
                button.onClick.AddListener(() => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON));
        }

        private void OnDestroy()
        {
            foreach (Button button in FindObjectsOfType<Button>(true))
                button.onClick.RemoveAllListeners();
        }

        #endregion


        #region Show/Hide

        /// <summary>
        /// Shows or hides the pause menu.
        /// </summary>
        /// <param name="show">True if the pause menu should be activated, false otherwise.</param>
        public void TogglePauseMenu(bool show) => m_MenuCanvas.SetActive(show);

        #endregion


        #region Button Functionality

        /// <summary>
        /// Calls the <see cref="GameController"/> to unpause the game.
        /// </summary>
        public void Unpause() => GameController.Instance.SetPause_ServerRpc(isPaused: false);

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
        /// </summary>
        public void LeaveGame() => ConnectionManager.Instance.Disconnect();

        #endregion


        #region Sounds

        /// <summary>
        /// Calls the <see cref="AudioController"/> to play the button click sound.
        /// </summary>
        public void PlayButtonSound() => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON);

        #endregion
    }
}