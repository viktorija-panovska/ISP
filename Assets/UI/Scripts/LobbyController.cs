using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// This class contains methods which define the behavior of the Lobby UI.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class LobbyController : NetworkBehaviour
{
    [SerializeField] private GameObject m_ServerOnly;
    [SerializeField] private GameObject m_ClientOnly;
    [SerializeField] private Texture2D m_CursorTexture;

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

    private NetworkVariable<bool> m_IsClientReady;


    #region MonoBehavior

    private void Awake()
    {
        m_IsClientReady = new NetworkVariable<bool>();
        Cursor.SetCursor(m_CursorTexture, Vector2.zero, CursorMode.Auto);
    }

    #endregion


    #region NetworkBehavior

    /// <summary>
    /// Sets up the menus of each player once they are spawned into the lobby.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        foreach (Button button in FindObjectsOfType<Button>(true))
            button.onClick.AddListener(() => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON));

        m_LobbyNameField.text = GameData.Instance.CurrentLobbyInfo.LobbyName;
        m_LobbyPasswordField.text = GameData.Instance.CurrentLobbyInfo.LobbyPassword;
        m_MapSeedField.text = GameData.Instance.MapSeed.ToString();

        if (NetworkManager.Singleton.IsHost)
            m_ServerOnly.SetActive(true);
        else
            m_ClientOnly.SetActive(true);

        GameData.Instance.SubscribeToPlayersInfoList(OnPlayersInfoListChange);
        m_IsClientReady.OnValueChanged += OnBluePlayerReady;

        PlayerInfo? redPlayerInfo = GameData.Instance.GetPlayerInfoByTeam(Team.RED);
        if (redPlayerInfo.HasValue) SetupPlayerInfo(redPlayerInfo.Value);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        GameData.Instance.UnsubscribeFromPlayersInfoList(OnPlayersInfoListChange);

        foreach (Button button in FindObjectsOfType<Button>(true))
            button.onClick.RemoveAllListeners();
    }

    #endregion


    #region Player Display

    private void OnPlayersInfoListChange(NetworkListEvent<PlayerInfo> @event)
    {
        if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.Add)
        {
            SetupPlayerInfo(@event.Value);

            if (IsHost && @event.Value.Team == Team.BLUE)
            {
                m_KickButton.interactable = true;
                AudioController.Instance.PlaySound(SoundType.BLUE_CONNECT);
            }
        }

        if (@event.Type == NetworkListEvent<PlayerInfo>.EventType.RemoveAt)
        {
            UnsetPlayerInfo(@event.Index);

            if (IsHost && @event.Index == 1)
            {
                m_KickButton.interactable = false;
                AudioController.Instance.PlaySound(SoundType.BLUE_DISCONNECT);
            }
        }
    }

    private async void SetupPlayerInfo(PlayerInfo playerInfo)
    {
        int index = (int)playerInfo.Team;
        m_PlayerName[index].text = playerInfo.SteamName;
        m_PlayerAvatar[index].texture = await InterfaceUtils.GetSteamAvatar(playerInfo.SteamId);
        m_PlayerAvatar[index].gameObject.SetActive(true);
    }

    private void UnsetPlayerInfo(int index)
    {
        m_PlayerName[index].text = "";
        m_PlayerAvatar[index].texture = null;
        m_PlayerAvatar[index].gameObject.SetActive(false);

        if (index == 1)
            m_BluePlayerReadySignal.SetActive(false);
    }

    #endregion


    #region Start Game

    /// <summary>
    /// Calls the <see cref="ConnectionManager"/> to start the game, if called by the host and the client is ready.
    /// </summary>
    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost || !m_IsClientReady.Value) return;
        ConnectionManager.Instance.StartGame();
    }

    /// <summary>
    /// Makes a call to the server, setting the blue player to ready if it is not and vice versa.
    /// </summary>
    /// <param name="_">Toggle parameter, true if toggled on false otherwise - not in use</param>
    public void ToggleIsBluePlayerReady(bool _)
    {
        m_ReadyButton.GetComponent<Image>().color = m_ReadyButton.isOn ? Color.gray : Color.white;
;       ToggleIsBluePlayerReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleIsBluePlayerReadyServerRpc()
        => m_IsClientReady.Value = !m_IsClientReady.Value;

    private void OnBluePlayerReady(bool _, bool __)
    {
        m_BluePlayerReadySignal.SetActive(m_IsClientReady.Value);
        AudioController.Instance.PlaySound(m_IsClientReady.Value ? SoundType.BLUE_READY : SoundType.BLUE_NOT_READY);

        if (NetworkManager.Singleton.IsHost) 
            m_StartButton.interactable = m_IsClientReady.Value;
    }

    #endregion


    #region Leave Lobby

    /// <summary>
    /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
    /// </summary>
    public void LeaveGame()
        => ConnectionManager.Instance.Disconnect();

    /// <summary>
    /// Forcibly disconnects the client from the game, if called by the host.
    /// </summary>
    public void KickClient()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        ConnectionManager.Instance.KickClient();
    }

    #endregion
}
