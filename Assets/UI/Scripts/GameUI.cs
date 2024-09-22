using UnityEngine;

public class GameUI : MonoBehaviour
{
    private static GameUI m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static GameUI Instance { get => m_Instance; }


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;

        //Cursor.visible = false;
    }

    #endregion
}