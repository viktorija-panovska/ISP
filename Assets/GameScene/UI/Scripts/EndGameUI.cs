using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// The <c>EndGameUI</c> class controls the behavior of the end game popup UI.
    /// </summary>
    public class EndGameUI : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The UI canvas that the menu is created on.")]
        [SerializeField] private CanvasGroup m_MenuCanvasGroup;

        [Tooltip("The image of the frame that should be shown when the red player wins.")]
        [SerializeField] private GameObject m_RedFrame;

        [Tooltip("The image of the frame that should be shown when the blue player wins.")]
        [SerializeField] private GameObject m_BlueFrame;

        [Tooltip("The textbox that should display the winner's name.")]
        [SerializeField] private TMP_Text m_WinnerName;

        [Tooltip("The raw image that should display the winner's avatar.")]
        [SerializeField] private RawImage m_WinnerAvatar;

        [Tooltip("All the buttons in the menu.")]
        [SerializeField] private Button[] m_Buttons;

        #endregion


        private static EndGameUI m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static EndGameUI Instance { get => m_Instance; }


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
        /// Sets up the winner information and fades in the end game UI.
        /// </summary>
        /// <param name="winner">The <c>Team</c> that won the game.</param>
        public async void ShowEndGameUI(Faction winner)
        {
            PlayerInfo? winnerInfo = GameData.Instance.GetPlayerInfoByFaction(winner);

            if (winnerInfo.HasValue)
            {
                m_WinnerName.text = winnerInfo.Value.SteamName;
                m_WinnerAvatar.texture = await InterfaceUtils.GetSteamAvatar(winnerInfo.Value.SteamId);

                if (winner == Faction.RED)
                    m_RedFrame.SetActive(true);
                else if (winner == Faction.BLUE)
                    m_BlueFrame.SetActive(true);
            }

            InterfaceUtils.FadeMenuIn(m_MenuCanvasGroup);
        }

        #endregion


        #region Button Functionality

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
        /// </summary>
        public void BackToMenu() => ConnectionManager.Instance.Disconnect();

        #endregion


        #region Sounds

        /// <summary>
        /// Calls the <see cref="AudioController"/> to play the button click sound.
        /// </summary>
        public void PlayButtonSound() => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON);

        #endregion
    }
}