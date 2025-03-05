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

        [SerializeField] private GameObject m_ServerOnly;
        [SerializeField] private GameObject m_ClientOnly;

        [Header("Server Info")]
        [SerializeField] private TMP_Text m_LobbyNameField;
        [SerializeField] private TMP_Text m_LobbyPasswordField;
        [SerializeField] private TMP_Text m_MapSeedField;

        [Header("Player Info")]
        [SerializeField] private RawImage[] m_PlayerAvatar;
        [SerializeField] private TMP_Text[] m_PlayerName;
        [SerializeField] private GameObject m_BluePlayerReadySignal;

        [Header("Buttons")]
        [SerializeField] private Button m_KickButton;
        [SerializeField] private Button m_StartButton;
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

            //m_LobbyNameField.text = GameData.Instance.LobbyName;
            //m_LobbyPasswordField.text = GameData.Instance.LobbyPassword;
            m_MapSeedField.text = GameData.Instance.GameSeed.ToString();

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

            //// as the player info cards are only populated when a player is added or removed
            //// when the client joins the lobby, the host's card won't be populated for them.
            if (!NetworkManager.Singleton.IsHost)
            {
                PlayerInfo? hostInfo = GameData.Instance.GetHostPlayerInfo();
                if (!hostInfo.HasValue) return;
                SetPlayerInfo(hostInfo.Value);
            }

            Debug.Log(GameData.Instance.GetClientPlayerInfo());
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            base.OnDestroy();
            GameData.Instance.UnsubscribeFromPlayersInfoList(OnPlayersInfoListChange);
        }

        #endregion


        #region Players Display

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event">The <c>NetworkListEvent</c>, containg the information about the list operation that was performed
        /// and the <c>PlayerInfo</c></param> that was added or removed.
        private void OnPlayersInfoListChange(NetworkListEvent<PlayerInfo> @event)
        {
            Debug.Log("OnPlayersInfoListChange: " + @event.Value);

            if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.Add)
            {
                SetPlayerInfo(@event.Value);

                if (IsHost && @event.Value.Faction == Faction.BLUE)
                    m_KickButton.interactable = true;
            }

            if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.RemoveAt)
            {
                UnsetPlayerInfo(@event.Value.Faction);

                if (IsHost && @event.Value.Faction == Faction.BLUE)
                    m_KickButton.interactable = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="playerInfo"></param>
        private async void SetPlayerInfo(PlayerInfo playerInfo)
        {
            Debug.Log("Set Player Info");
            int index = (int)playerInfo.Faction;
            m_PlayerName[index].text = playerInfo.SteamName;
            m_PlayerAvatar[index].texture = await InterfaceUtils.GetSteamAvatar(playerInfo.SteamId);
            m_PlayerAvatar[index].gameObject.SetActive(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        private void UnsetPlayerInfo(Faction faction)
        {
            int index = (int)faction;
            m_PlayerName[index].text = "";
            m_PlayerAvatar[index].texture = null;
            m_PlayerAvatar[index].gameObject.SetActive(false);

            if (index == 1)
                m_BluePlayerReadySignal.SetActive(false);
        }

        #endregion


        #region Start Game

        /// <summary>
        /// Makes a call to the server, setting the state of the blue player to ready if it is not and vice versa.
        /// </summary>
        /// <param name="_">Toggle parameter, true if toggled on false otherwise - not in use.</param>
        public void ToggleIsClientReady(bool _)
        {
            m_ReadyButton.GetComponent<Image>().color = m_ReadyButton.isOn ? Color.gray : Color.white;
            m_BluePlayerReadySignal.SetActive(m_ReadyButton.isOn);

            ToggleIsClientReady_ServerRpc(m_ReadyButton.isOn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isReady"></param>
        [ServerRpc(RequireOwnership = false)]
        private void ToggleIsClientReady_ServerRpc(bool isReady)
        {
            m_IsClientReady = isReady;
            m_BluePlayerReadySignal.SetActive(isReady);
            m_StartButton.interactable = isReady;
        }

        /// <summary>
        /// Calls the <see cref="IConnectionManager"/> to start the game, if called by the host and the client is ready.
        /// </summary>
        public void StartGame()
        {
            if (!NetworkManager.Singleton.IsHost || !m_IsClientReady) return;
            StartGame_ClientRpc();
        }

        [ClientRpc]
        private void StartGame_ClientRpc() => m_ConnectionManager.StartGame();

        #endregion


        #region Leave Lobby

        /// <summary>
        /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the lobby.
        /// </summary>
        public void LeaveLobby() => m_ConnectionManager.Disconnect();

        /// <summary>
        /// Forcibly disconnects the client from the game, if called by the host.
        /// </summary>
        public void KickClient()
        {
            if (!NetworkManager.Singleton.IsHost) return;

            Debug.Log("KickClient");
            PlayerInfo? clientInfo = GameData.Instance.GetClientPlayerInfo();
            if (!clientInfo.HasValue) return;
            Debug.Log(clientInfo.Value.Faction);


            foreach (var player in GameData.Instance.GetPlayerInfoList())
            {
                Debug.Log($"NetworkID: {player.NetworkId}. SteamName: {player.SteamName}. Faction: {player.Faction}");
            }


            LeaveLobby_ClientRpc(GameUtils.GetClientParams(clientInfo.Value.NetworkId));
        }

        [ClientRpc]
        private void LeaveLobby_ClientRpc(ClientRpcParams _ = default) => LeaveLobby();

        #endregion
    }

}