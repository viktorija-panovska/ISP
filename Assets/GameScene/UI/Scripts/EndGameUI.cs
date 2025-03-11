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

        [Tooltip("The UI canvas that the menu is created on, parented to the GameObject of this component.")]
        [SerializeField] private CanvasGroup m_MenuCanvasGroup;
        [Tooltip("The image of the frame that should be shown when the red player wins.")]
        [SerializeField] private GameObject m_RedFrame;
        [Tooltip("The image of the frame that should be shown when the blue player wins.")]
        [SerializeField] private GameObject m_BlueFrame;
        [Tooltip("The textbox that should display the winner's name.")]
        [SerializeField] private TMP_Text m_WinnerName;
        [Tooltip("The raw image that should display the winner's avatar.")]
        [SerializeField] private RawImage m_WinnerAvatar;

        #endregion


        private static EndGameUI m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static EndGameUI Instance { get => m_Instance; }


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }


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

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
        /// </summary>
        public void BackToMenu() => ConnectionManager.Instance.Disconnect();
    }
}