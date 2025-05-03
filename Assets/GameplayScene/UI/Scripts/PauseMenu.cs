using TMPro;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>PauseMenu</c> class controls the behavior of the pause menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The UI canvas that the menu is created on, parented to the GameObject of this component.")]
        [SerializeField] private GameObject m_MenuCanvas;

        [Tooltip("The textbox that should contain the game seed.")]
        [SerializeField] private TMP_Text m_GameSeedField;

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

        private void Start() => m_GameSeedField.text = GameData.Instance ? GameData.Instance.GameSeed.ToString() : "";

        #endregion


        /// <summary>
        /// Shows or hides the pause menu.
        /// </summary>
        /// <param name="show">True if the pause menu should be activated, false otherwise.</param>
        public void TogglePauseMenu(bool show) => m_MenuCanvas.SetActive(show);

        /// <summary>
        /// Calls the <see cref="GameController"/> to unpause the game.
        /// </summary>
        public void Unpause() => GameController.Instance.SetPause_ServerRpc(isPaused: false);

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
        /// </summary>
        public void LeaveGame() => GameController.Instance.QuitGameFromPause_ServerRpc();
    }
}