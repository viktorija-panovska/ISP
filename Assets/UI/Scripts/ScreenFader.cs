using DG.Tweening;
using System;
using UnityEngine;


/// <summary>
/// This class contains methods for fading the entire screen to black, and fading the screen from black.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_BlackScreen;

    private static ScreenFader m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static ScreenFader Instance { get => m_Instance; }

    /// <summary>
    /// Called when the screen has completely faded out.
    /// </summary>
    public Action OnFadeOutComplete;


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion


    /// <summary>
    /// Fades screen to black.
    /// </summary>
    public void FadeOut(float duration = 1f)
    {
        m_BlackScreen.blocksRaycasts = true;
        m_BlackScreen.DOFade(1, duration).OnComplete(() => OnFadeOutComplete?.Invoke());
    }


    /// <summary>
    /// Fades screen in from black.
    /// </summary>
    public void FadeIn(float duration = 1f)
    {
        m_BlackScreen.DOFade(0, duration).OnComplete(() => m_BlackScreen.blocksRaycasts = false);
    }
}