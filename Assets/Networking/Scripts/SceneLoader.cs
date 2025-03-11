using DG.Tweening;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Populous
{
    /// <summary>
    /// Scenes in the game.
    /// </summary>
    public enum Scene
    {
        /// <summary>
        /// Placeholder scene.
        /// </summary>
        NONE,
        /// <summary>
        /// The Main Menu scene
        /// </summary>
        MAIN_MENU,
        /// <summary>
        /// The Lobby scene
        /// </summary>
        LOBBY,
        /// <summary>
        /// The Gameplay scene
        /// </summary>
        GAMEPLAY
    }

    [RequireComponent(typeof(NetworkObject))]
    public class SceneLoader : NetworkBehaviour
    {
        [Tooltip("The Canvas that is parented to the GameObject of this script.")]
        [SerializeField] private CanvasGroup m_BlackScreen;


        private static SceneLoader m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static SceneLoader Instance { get => m_Instance; }

        /// <summary>
        /// Stores the scene the host is currently in.
        /// </summary>
        private Scene m_HostScene;

        /// <summary>
        /// Called when the screen has completely faded out.
        /// </summary>
        public Action OnFadeOutComplete;


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
                Destroy(m_Instance.gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);
        }


        #region Switch Scenes

        /// <summary>
        /// Loads the destination scene in Single mode through the network.
        /// </summary>
        /// <param name="scene">The destination scene.</param>
        public void SwitchToScene_Network(Scene scene)
        {
            if (!IsHost) return;
            NetworkManager.Singleton.SceneManager.LoadScene(scene.ToString(), LoadSceneMode.Single);
            m_HostScene = scene;
        }

        /// <summary>
        /// Loads the destination scene in Single mode locally.
        /// </summary>
        /// <param name="scene">The destination scene.</param>
        public void SwitchToScene_Local(Scene scene) => SceneManager.LoadScene(scene.ToString(), LoadSceneMode.Single);

        /// <summary>
        /// Gets the scene the host is currently in.
        /// </summary>
        /// <returns>Returns the <c>Scene</c> the host is currently in.</returns>
        public Scene GetHostScene() => m_HostScene;


        /// <summary>
        /// Processes all scene events types, for both the host and the clients.
        /// </summary>
        /// <param name="sceneEvent">The <c>SceneEvent</c> to be processed.</param>
        public void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId || 
               (sceneEvent.SceneEventType != SceneEventType.LoadComplete && 
                sceneEvent.SceneEventType != SceneEventType.SynchronizeComplete))
                return;

            FadeIn();
        }

        #endregion


        #region Fade in/out

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
            => m_BlackScreen.DOFade(0, duration).OnComplete(() => m_BlackScreen.blocksRaycasts = false);

        #endregion
    }
}