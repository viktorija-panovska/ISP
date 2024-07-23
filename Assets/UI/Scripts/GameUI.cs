using UnityEngine;

public class GameUI : MonoBehaviour
{
    [SerializeField] private Texture2D m_NormalCursorTexture;
    [SerializeField] private Texture2D m_ClickCursorTexture;

    private static GameUI m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static GameUI Instance { get => m_Instance; }


    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;

        Cursor.SetCursor(m_NormalCursorTexture, Vector2.zero, CursorMode.Auto);
    }
}