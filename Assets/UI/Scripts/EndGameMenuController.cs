using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// This class contains methods which define the behavior of the End Game Menu UI.
/// </summary>
public class EndGameMenuController : MonoBehaviour
{
    [SerializeField] private Texture2D m_CursorTexture;
    [SerializeField] private CanvasGroup m_MenuCanvasGroup;
    [SerializeField] private GameObject m_RedFrame;
    [SerializeField] private GameObject m_BlueFrame;
    [SerializeField] private TMP_Text m_WinnerName;
    [SerializeField] private RawImage m_WinnerAvatar;
    [SerializeField] private Button[] m_Buttons;

    private static EndGameMenuController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static EndGameMenuController Instance { get => m_Instance; }


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

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
    /// Fills in the name and avatar of the game winner and fades in the end game menu canvas.
    /// </summary>
    public async void ShowEndGameMenu()
    {
        Cursor.SetCursor(m_CursorTexture, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;

        Team winner = GameController.Instance.Winner;
        PlayerInfo? winnerInfo = GameData.Instance.GetPlayerInfoByTeam(winner);

        if (winnerInfo.HasValue)
        {
            m_WinnerName.text = winnerInfo.Value.SteamName;
            m_WinnerAvatar.texture = await InterfaceUtils.GetSteamAvatar(winnerInfo.Value.SteamId);

            if (winner == Team.RED)
                m_RedFrame.SetActive(true);
            else if (winner == Team.BLUE)
                m_BlueFrame.SetActive(true);
        }

        InterfaceUtils.FadeMenuIn(m_MenuCanvasGroup);
    }


    /// <summary>
    /// Fades out the end game menu canvas.
    /// </summary>
    public void HideEndGameMenu()
    {
        Cursor.visible = false;
        InterfaceUtils.FadeMenuOut(m_MenuCanvasGroup);
    }

    #endregion


    #region Button Functionality

    /// <summary>
    /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
    /// </summary>
    public void BackToMenu()
        => ConnectionManager.Instance.Disconnect();

    #endregion


    #region Sounds

    /// <summary>
    /// Calls the <see cref="AudioController"/> to play the button click sound.
    /// </summary>
    public void PlayButtonSound()
        => AudioController.Instance.PlaySound(SoundType.MENU_BUTTON);

    #endregion
}