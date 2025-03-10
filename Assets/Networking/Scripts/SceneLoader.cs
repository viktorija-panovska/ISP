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
        GAMEPLAY_SCENE
    }


    [RequireComponent(typeof(NetworkObject))]
    public class SceneLoader : NetworkBehaviour
    {
        [SerializeField] private CanvasGroup m_BlackScreen;


        private static SceneLoader m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static SceneLoader Instance { get => m_Instance; }

        /// <summary>
        /// Stores the scenes each player is currently in. Index 0 is the host and index 1 is the client.
        /// </summary>
        private readonly Scene[] m_PlayerInScene = new Scene[2];

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


        /// <summary>
        /// Loads the destination scene in Single mode through the network.
        /// </summary>
        /// <param name="scene">The destination scene.</param>
        public void SwitchToScene_Network(Scene scene)
        {
            if (!IsHost) return;
            NetworkManager.Singleton.SceneManager.LoadScene(scene.ToString(), LoadSceneMode.Single);
        }

        /// <summary>
        /// Loads the destination scene in Single mode locally.
        /// </summary>
        /// <param name="scene">The destination scene.</param>
        public void SwitchToScene_Local(Scene scene) => SceneManager.LoadScene(scene.ToString(), LoadSceneMode.Single);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Scene GetCurrentScene()
            => IsHost ? m_PlayerInScene[0] : m_PlayerInScene[1];

        /// <summary>
        /// Processes all scene events types, for both the host and the clients.
        /// </summary>
        /// <param name="sceneEvent">The <c>SceneEvent</c> to be processed.</param>
        public void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId || 
               (sceneEvent.SceneEventType != SceneEventType.LoadComplete && sceneEvent.SceneEventType != SceneEventType.SynchronizeComplete))
                return;

            Debug.Log("Handle Scene Event");

            Scene scene = Scene.NONE;
            switch (sceneEvent.SceneName)
            {
                case "MAIN_MENU": scene = Scene.MAIN_MENU; break;
                case "LOBBY": scene = Scene.LOBBY; break;
                case "GAME_SCENE": scene = Scene.GAMEPLAY_SCENE; break;
            }

            m_PlayerInScene[IsHost ? 0 : 1] = scene;
            FadeIn();
        }


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
    }
}