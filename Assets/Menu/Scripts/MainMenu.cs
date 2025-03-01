using Steamworks;
using Steamworks.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// This class contains methods which define the behavior of the Main Menu UI.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private Texture2D m_CursorTexture;
        [SerializeField] private Button m_JoinLobbyButton;

        [Header("Screens")]
        [SerializeField] private GameObject m_MainScreen;
        [SerializeField] private GameObject m_HostScreen;
        [SerializeField] private GameObject m_JoinScreen;
        [SerializeField] private GameObject m_PasswordScreen;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField m_LobbyNameInputField;
        [SerializeField] private TMP_InputField m_PasswordInputField;
        [SerializeField] private TMP_InputField m_MapSeedInputField;

        [Header("Lobbies")]
        [SerializeField] private GameObject m_LobbyScrollView;
        [SerializeField] private GameObject m_LobbyEntryPrefab;

        [Header("Password Entry Page")]
        [SerializeField] private TMP_Text m_LobbyName;
        [SerializeField] private TMP_InputField m_PasswordCheckInputField;

        private GameObject m_CurrentScreen;
        private List<GameObject> m_LobbyEntryList;
        private LobbyEntry m_SelectedLobby;


        #region MonoBehavior

        private void Start()
        {
            m_CurrentScreen = m_MainScreen;
            m_LobbyEntryList = new();
            Cursor.SetCursor(m_CursorTexture, Vector2.zero, CursorMode.Auto);

            foreach (Button button in FindObjectsOfType<Button>(true))
                button.onClick.AddListener(() => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON));
        }

        private void OnDestroy()
        {
            foreach (Button button in FindObjectsOfType<Button>(true))
                button.onClick.RemoveAllListeners();
        }

        #endregion


        #region Menu Navigation

        /// <summary>
        /// Switches from any screen to the main menu screen.
        /// </summary>
        public void SwitchToMainScreen()
            => SwitchScreens(m_MainScreen);

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
            m_SelectedLobby = null;
            SwitchScreens(m_JoinScreen);
            FillLobbyList();
        }

        /// <summary>
        /// Switches from any screen to the lobby password entry screen, if a password protected lobby has been selected.
        /// </summary>
        public void SwitchToPasswordScreen()
        {
            if (!m_SelectedLobby || !m_SelectedLobby.HasPassword) return;
            m_LobbyName.text = m_SelectedLobby.LobbyName;
            SwitchScreens(m_PasswordScreen);
        }

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
                AudioController.Instance.PlaySound(SoundType.MENU_WRONG);
                InterfaceUtils.FlashWrong(m_LobbyNameInputField.image);
                return;
            }

            ConnectionManager.Instance.CreateGame(m_LobbyNameInputField.text, m_PasswordInputField.text, m_MapSeedInputField.text);
        }

        private void ClearHostValues()
        {
            m_LobbyNameInputField.text = "";
            m_PasswordInputField.text = "";
            m_MapSeedInputField.text = "";
        }

        #endregion


        #region Joining a lobby

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to join the selected lobby, or switches to the password entry screen 
        /// if the selected lobby is password protected.
        /// </summary>
        public void JoinLobby()
        {
            if (!m_SelectedLobby) return;

            if (m_SelectedLobby.HasPassword)
                SwitchToPasswordScreen();
            else
                EnterLobby();
        }

        /// <summary>
        /// Replaces the displayed list of active lobbies with a more recent list of active lobbies.
        /// </summary>
        public void RefreshLobbyList()
            => FillLobbyList();

        /// <summary>
        /// Deselects the previously selected lobby entry and selects a new one.
        /// </summary>
        /// <param name="entry">Selected lobby entry.</param>
        public void SelectEntry(LobbyEntry entry)
        {
            if (m_SelectedLobby != null)
                m_SelectedLobby.Deselect();

            m_SelectedLobby = entry;
            m_JoinLobbyButton.interactable = true;
        }

        /// <summary>
        /// Deselects the currently selected lobby entry.
        /// </summary>
        public void DeselectEntry()
        {
            m_SelectedLobby = null;
            m_JoinLobbyButton.interactable = false;
        }

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to attempt to join the selected lobby if a password has been entered.
        /// </summary>
        public void SubmitPassword()
        {
            if (m_PasswordInputField.text.Length == 0)
            {
                AudioController.Instance.PlaySound(SoundType.MENU_WRONG);
                InterfaceUtils.FlashWrong(m_PasswordCheckInputField.image);
                return;
            }

            EnterLobby();
        }

        private void EnterLobby()
        {
            ConnectionManager.Instance.JoinGame(m_SelectedLobby.LobbyId);
            m_SelectedLobby = null;
        }

        private async void FillLobbyList()
        {
            foreach (GameObject lobbyObject in m_LobbyEntryList)
            {
                LobbyEntry lobbyEntry = lobbyObject.GetComponent<LobbyEntry>();
                lobbyEntry.OnEntrySelected -= SelectEntry;
                lobbyEntry.OnEntryDeselected -= DeselectEntry;
                Destroy(lobbyObject);
            }

            m_LobbyEntryList = new();


            if (ConnectionManager.Instance.LocalConnection)
            {
                GameObject entryObject = Instantiate(m_LobbyEntryPrefab);
                entryObject.transform.SetParent(m_LobbyScrollView.transform);
                entryObject.transform.localScale = Vector3.one;

                LobbyEntry entry = entryObject.GetComponent<LobbyEntry>();
                entry.Setup(0, "Hello", "" != "", SteamClient.SteamId);
                entry.OnEntrySelected += SelectEntry;
                entry.OnEntryDeselected += DeselectEntry;

                m_LobbyEntryList.Add(entryObject);
                return;
            }


            Lobby[] lobbies = await ConnectionManager.Instance.GetActiveLobbies();

            foreach (Lobby lobby in lobbies)
            {
                //if (lobby.GetData("isISP") == "")
                //    continue;

                GameObject entryObject = Instantiate(m_LobbyEntryPrefab);
                LobbyEntry entry = entryObject.GetComponent<LobbyEntry>();

                entry.Setup(lobby.Id, lobby.GetData("name"), lobby.GetData("password") != "", lobby.Owner.Id);
                entryObject.transform.SetParent(m_LobbyScrollView.transform);
                entryObject.transform.localScale = Vector3.one;

                m_LobbyEntryList.Add(entryObject);
            }
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