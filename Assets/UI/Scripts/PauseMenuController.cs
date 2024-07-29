using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// This class contains methods which define the behavior of the Pause Menu UI.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private Texture2D m_CursorTexture;
    [SerializeField] private GameObject m_MenuCanvas;
    [SerializeField] private TMP_Text m_MapSeedField;
    [SerializeField] private Button[] m_Buttons;

    private static PauseMenuController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static PauseMenuController Instance { get => m_Instance; }


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    private void Start()
    {
        m_MapSeedField.text = GameData.Instance.MapSeed.ToString();

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
    /// Activates the pause menu canvas.
    /// </summary>
    public void ShowPauseMenu()
    {
        m_MenuCanvas.SetActive(true);
        Cursor.SetCursor(m_CursorTexture, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }

    /// <summary>
    /// Deactivates the pause menu canvas.
    /// </summary>
    public void HidePauseMenu()
    {
        m_MenuCanvas.SetActive(false);
        Cursor.visible = false;
    }

    #endregion


    #region Button Functionality

    /// <summary>
    /// Calls the <see cref="PlayerController"/> to unpause the game.
    /// </summary>
    public void Unpause()
    {
        PlayerController.Instance.HandlePause();
    }

    /// <summary>
    /// Calls the <see cref="ConnectionManager"/> to disconnect the player from the game.
    /// </summary>
    public void LeaveGame()
    {
        ConnectionManager.Instance.Disconnect();
    }

    #endregion


    #region Sounds

    /// <summary>
    /// Calls the <see cref="AudioController"/> to play the button click sound.
    /// </summary>
    public void PlayButtonSound()
    {
        AudioController.Instance.PlaySound(SoundType.MENU_BUTTON);
    }

    #endregion
}
