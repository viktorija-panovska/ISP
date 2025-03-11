using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


namespace Populous
{
    /// <summary>
    /// The <c>LobbyMenu</c> class handles the behavior of the lobby.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LobbyMenu : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("Set to true if testing the game on a local network")]
        [SerializeField] private bool m_IsTestingLocal;

        [Header("Lobby Info")]
        [Tooltip("The text field that should contain the lobby name.")]
        [SerializeField] private TMP_Text m_LobbyNameField;
        [Tooltip("The text field that should contain the game seed.")]
        [SerializeField] private TMP_Text m_GameSeedField;

        [Header("Player Info")]
        [Tooltip("The images on which the players' Steam avatars should be displayed. Index 0 is for the red player and index 1 is for the blue.")]
        [SerializeField] private RawImage[] m_PlayerAvatar;
        [Tooltip("The text fields on which the players' Steam names should be displayed. Index 0 is for the red player and index 1 is for the blue.")]
        [SerializeField] private TMP_Text[] m_PlayerName;
        [Tooltip("The GameObject that should be enabled to notify that the client is ready to enter the game.")]
        [SerializeField] private GameObject m_ClientReadySignal;

        [Header("Buttons")]
        [Tooltip("The GameObject containing the buttons that should be visible for the host.")]
        [SerializeField] private GameObject m_ServerOnly;
        [Tooltip("The GameObject containing the buttons that should be visible for the client.")]
        [SerializeField] private GameObject m_ClientOnly;
        [Tooltip("The button which, when pressed, allows the host to kick the client out of the lobby.")]
        [SerializeField] private Button m_KickButton;
        [Tooltip("The button which, when pressed, allows the host to start the game..")]
        [SerializeField] private Button m_StartButton;
        [Tooltip("The button which, when pressed, allows the client to notify the host that they are ready to start the game.")]
        [SerializeField] private Toggle m_ReadyButton;

        #endregion


        /// <summary>
        /// A reference to the connection manager used to establish the connection between host and client.
        /// </summary>
        private IConnectionManager m_ConnectionManager;

        /// <summary>
        /// True if the client (non-hosting player) has indicated that they are ready, false otherwise.
        /// </summary>
        private bool m_IsClientReady;


        #region Event Functions

        public void Start() => m_ConnectionManager = m_IsTestingLocal ? LocalConnectionManager.Instance : ConnectionManager.Instance;

        /// <summary>
        /// Called when this <c>NetworkObject</c> is spawned on the network, sets up the lobby.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            Debug.Log("OnNetworkSpawn");

            m_LobbyNameField.text = GameData.Instance.LobbyName;
            m_GameSeedField.text = GameData.Instance.GameSeed.ToString();

            m_ServerOnly.SetActive(false);
            m_ClientOnly.SetActive(false);

            if (NetworkManager.Singleton.IsHost)
                m_ServerOnly.SetActive(true);
            else
                m_ClientOnly.SetActive(true);

            GameData.Instance.SubscribeToPlayersInfoList(OnPlayersInfoListChange);
            GameData.Instance.AddPlayerInfo_ServerRpc(
                NetworkManager.Singleton.LocalClientId,
                SteamClient.SteamId,
                IsHost ? Faction.RED : Faction.BLUE
            );

            // as the player info cards are only populated when a player is added or removed
            // when the client joins the lobby, the host's card won't be populated for them.
            if (!NetworkManager.Singleton.IsHost)
            {
                PlayerInfo? hostInfo = GameData.Instance.GetHostPlayerInfo();
                if (!hostInfo.HasValue) return;
                SetPlayerInfo(hostInfo.Value);
            }
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            GameData.Instance.UnsubscribeFromPlayersInfoList(OnPlayersInfoListChange);
            base.OnDestroy();
        }

        #endregion


        #region Players Display

        /// <summary>
        /// Sets or removes the player data when a player is added or removed from the player information list.
        /// </summary>
        /// <param name="event">The <c>NetworkListEvent</c>, containg the information about the list 
        /// operation that was performed and the <c>PlayerInfo</c></param> that was added or removed.
        private void OnPlayersInfoListChange(NetworkListEvent<PlayerInfo> @event)
        {
            if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.Add)
            {
                SetPlayerInfo(@event.Value);

                if (IsHost && @event.Value.Faction == Faction.BLUE)
                    m_KickButton.interactable = true;
            }

            if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.RemoveAt)
            {
                RemovePlayerInfo(@event.Value.Faction);

                if (IsHost && @event.Value.Faction == Faction.BLUE)
                    m_KickButton.interactable = false;
            }
        }

        /// <summary>
        /// Sets the player data of the player that controls the given faction in the lobby.
        /// </summary>
        /// <param name="playerInfo">The <c>PlayerInfo</c> that needs to be set.</param>
        private async void SetPlayerInfo(PlayerInfo playerInfo)
        {
            int index = (int)playerInfo.Faction;
            m_PlayerName[index].text = playerInfo.SteamName;
            m_PlayerAvatar[index].texture = await InterfaceUtils.GetSteamAvatar(playerInfo.SteamId);
            m_PlayerAvatar[index].gameObject.SetActive(true);
        }

        /// <summary>
        /// Removes the player data of the player that controls the given faction from the lobby.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose player data should removed</param>
        private void RemovePlayerInfo(Faction faction)
        {
            int index = (int)faction;
            m_PlayerName[index].text = "";
            m_PlayerAvatar[index].texture = null;
            m_PlayerAvatar[index].gameObject.SetActive(false);

            if (faction == Faction.BLUE) 
                m_ClientReadySignal.SetActive(false);
        }

        #endregion


        #region Start Game

        /// <summary>
        /// Makes a call to the server, setting the state of the client to ready if it is not and vice versa.
        /// </summary>
        /// <param name="_">Toggle parameter, true if toggled on false otherwise - not in use.</param>
        public void ToggleIsClientReady(bool _)
        {
            m_ReadyButton.GetComponent<Image>().color = m_ReadyButton.isOn ? Color.gray : Color.white;
            m_ClientReadySignal.SetActive(m_ReadyButton.isOn);

            ToggleIsClientReady_ServerRpc(m_ReadyButton.isOn);
        }

        /// <summary>
        /// Sets the state of the client depending on the given parameter.
        /// </summary>
        /// <param name="isReady">True if the client is ready, false otherwise.</param>
        [ServerRpc(RequireOwnership = false)]
        private void ToggleIsClientReady_ServerRpc(bool isReady)
        {
            m_IsClientReady = isReady;
            m_ClientReadySignal.SetActive(isReady);
            m_StartButton.interactable = isReady;
        }

        /// <summary>
        /// Triggers the start of the game, if the client is ready.
        /// </summary>
        public void StartGame()
        {
            if (!NetworkManager.Singleton.IsHost || !m_IsClientReady) return;
            ConnectionManager.Instance.StartGame();
        }

        #endregion


        #region Leave Lobby

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the lobby.
        /// </summary>
        public void LeaveLobby() => m_ConnectionManager.Disconnect();

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to forcibly disconnect the client.
        /// </summary>
        public void KickClient() => m_ConnectionManager.KickClient();

        #endregion
    }
}