using Steamworks.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// The <c>MainMenu</c> class represents the starting menu, where the player selects whether they want to host or join a game.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Screens")]
        [Tooltip("The GameObject representing the main screen, where the player selects whether to host or join a game.")]
        [SerializeField] private GameObject m_MainScreen;
        [Tooltip("The GameObject represening the host screen, where the player enters the lobby information and created a game.")]
        [SerializeField] private GameObject m_HostScreen;
        [Tooltip("The GameObject representing the join screen, where the list of available lobbies is shown and the player can join one.")]
        [SerializeField] private GameObject m_JoinScreen;
        [Tooltip("The GameObject representing the password screen, where the player is prompted to enter a password to join a lobby.")]
        [SerializeField] private GameObject m_PasswordScreen;

        [Header("Host Lobby")]
        [Tooltip("The input field that the player should enter the lobby name in (required).")]
        [SerializeField] private TMP_InputField m_LobbyNameInputField;
        [Tooltip("The input field that the player should (optionally) enter the lobby password in.")]
        [SerializeField] private TMP_InputField m_PasswordInputField;
        [Tooltip("The input field that the player should (optionally) enter the game seed in.")]
        [SerializeField] private TMP_InputField m_GameSeedInputField;

        [Header("Join Lobby")]
        [Tooltip("The prefab from which the lobby entries in the lobby list should be spawned.")]
        [SerializeField] private GameObject m_LobbyEntryPrefab;
        [Tooltip("The transform that all lobby entries should be parented to to appear properly in the lobby list.")]
        [SerializeField] private Transform m_LobbyListContent;
        [Tooltip("The button the player should press to join the lobby.")]
        [SerializeField] private Button m_JoinLobbyButton;

        [Header("Password Entry")]
        [Tooltip("The text field that the lobby's name should appear in.")]
        [SerializeField] private TMP_Text m_LobbyName;
        [Tooltip("The input field that the player should enter the password in.")]
        [SerializeField] private TMP_InputField m_PasswordCheckInputField;

        #endregion

        private static MainMenu m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class;
        /// </summary>
        public static MainMenu Instance { get => m_Instance; }

        /// <summary>
        /// The menu screen that is currently active.
        /// </summary>
        private GameObject m_CurrentScreen;
        /// <summary>
        /// The list of active lobbies that is displayed.
        /// </summary>
        private List<LobbyListEntry> m_LobbyEntryList;
        /// <summary>
        /// The lobby entry that is currently selected;
        /// </summary>
        private LobbyListEntry m_SelectedLobbyEntry;


        #region MonoBehavior

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
            m_CurrentScreen = m_MainScreen;
            m_LobbyEntryList = new();
        }

        #endregion


        #region Menu Navigation

        /// <summary>
        /// Switches from any screen to the main menu screen.
        /// </summary>
        public void SwitchToMainScreen() => SwitchScreens(m_MainScreen);

        /// <summary>
        /// Switches from any screen to the lobby creation screen.
        /// </summary>
        public void SwitchToHostScreen()
        {
            ClearHostValues();
            SwitchScreens(m_HostScreen);
        }

        /// <summary>
        /// Switches from any screen to the lobby selection screen.
        /// </summary>
        public void SwitchToJoinScreen()
        {
            m_SelectedLobbyEntry = null;
            SwitchScreens(m_JoinScreen);
            FillLobbyList();
        }

        /// <summary>
        /// Switches from any screen to the lobby password entry screen.
        /// </summary>
        public void SwitchToPasswordScreen()
        {
            if (!m_SelectedLobbyEntry || !m_SelectedLobbyEntry.HasPassword) return;
            m_LobbyName.text = m_SelectedLobbyEntry.LobbyName;
            SwitchScreens(m_PasswordScreen);
        }

        /// <summary>
        /// Switches from the currently active screen to the given screen.
        /// </summary>
        /// <param name="screen">The <c>GameObject</c> representing the screen of the main menu that should be enabled.</param>
        private void SwitchScreens(GameObject screen)
        {
            m_CurrentScreen.SetActive(false);
            screen.SetActive(true);
            m_CurrentScreen = screen;
        }

        #endregion


        #region Hosting a lobby

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to start hosting a lobby if a lobby name has been entered.
        /// </summary>
        public void HostLobby()
        {
            if (m_LobbyNameInputField.text.Length == 0)
            {
                InterfaceUtils.FlashWrong(m_LobbyNameInputField.image);
                return;
            }

            ConnectionManager.Instance.CreateLobby(m_LobbyNameInputField.text, m_PasswordInputField.text, m_GameSeedInputField.text);
        }

        /// <summary>
        /// Resets the values of the fields on the lobby creation screen.
        /// </summary>
        private void ClearHostValues()
        {
            m_LobbyNameInputField.text = "";
            m_PasswordInputField.text = "";
            m_GameSeedInputField.text = "";
        }

        #endregion


        #region Joining a lobby

        /// <summary>
        /// Replaces the displayed list of active lobbies with a more recent list of active lobbies.
        /// </summary>
        public void RefreshLobbyList() => FillLobbyList();

        /// <summary>
        /// Creates list entries for all active lobbies such that the player can select them.
        /// </summary>
        private async void FillLobbyList()
        {
            foreach (LobbyListEntry lobbyEntry in m_LobbyEntryList)
                Destroy(lobbyEntry);

            m_LobbyEntryList = new();

            Lobby[] lobbies = await ConnectionManager.Instance.GetActiveLobbies();

            foreach (Lobby lobby in lobbies)
            {
                // to filter out lobbies from Spacewar that aren't from this project
                if (lobby.GetData("isPopulous") == "")
                    continue;

                LobbyListEntry lobbyEntry = Instantiate(m_LobbyEntryPrefab).GetComponent<LobbyListEntry>();

                lobbyEntry.Setup(lobby);
                lobbyEntry.transform.SetParent(m_LobbyListContent);
                lobbyEntry.transform.localScale = Vector3.one;

                m_LobbyEntryList.Add(lobbyEntry);
            }
        }

        /// <summary>
        /// Sets the given lobby entry as the currently selected lobby.
        /// </summary>
        /// <param name="lobbyEntry">The <c>LobbyEntry</c> that is selected, null if all entries should be deselected.</param>
        public void SetSelectedEntry(LobbyListEntry lobbyEntry)
        {
            if (m_SelectedLobbyEntry)
                m_SelectedLobbyEntry.Deselect(updateMenu: false);

            m_SelectedLobbyEntry = lobbyEntry;
            m_JoinLobbyButton.interactable = lobbyEntry;
        }

        /// <summary>
        /// if the lobby has a password, switches to the password entry screen, otherwise enters the lobby.
        /// </summary>
        public void JoinLobby()
        {
            if (!m_SelectedLobbyEntry) return;

            if (m_SelectedLobbyEntry.HasPassword)
                SwitchToPasswordScreen();
            else
                EnterLobby();
        }

        /// <summary>
        /// If the player has entered a password, attempts to join the lobby.
        /// </summary>
        public void SubmitPassword()
        {
            if (m_PasswordInputField.text.Length == 0)
            {
                InterfaceUtils.FlashWrong(m_PasswordCheckInputField.image);
                return;
            }

            EnterLobby();
        }

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to attempt to join the selected lobby.
        /// </summary>
        private void EnterLobby()
        { 
            ConnectionManager.Instance.JoinLobby(m_SelectedLobbyEntry.Lobby);
            m_SelectedLobbyEntry = null;
        }

        #endregion


        #region Exiting the game

        /// <summary>
        /// Shuts down the game, called when the in-game exit button is pressed.
        /// </summary>
        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }

        #endregion
    }
}