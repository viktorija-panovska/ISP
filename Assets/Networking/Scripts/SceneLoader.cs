using System.Collections.Generic;
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
        /// The Game scene
        /// </summary>
        GAME_SCENE
    }



    [RequireComponent(typeof(NetworkObject))]
    public class SceneLoader : NetworkBehaviour
    {
        private static SceneLoader m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static SceneLoader Instance { get => m_Instance; }

        private readonly Dictionary<ulong, Scene> m_ClientInScene = new();


        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);
        }


        /// <summary>
        /// Loads the destination scene in Single mode.
        /// </summary>
        /// <param name="scene">The destination scene.</param>
        public void SwitchToScene(Scene scene)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(scene.ToString(), LoadSceneMode.Single);
        }


        /// <summary>
        /// Processes all scene events types, for both the host and the clients.
        /// </summary>
        /// <param name="sceneEvent">The <c>SceneEvent</c> to be processed.</param>
        public void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete || sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
            {
                Debug.Log("Handle Scene Event");

                switch (sceneEvent.SceneName)
                {
                    case "MAIN_MENU":
                        m_ClientInScene[sceneEvent.ClientId] = Scene.MAIN_MENU;
                        break;

                    case "LOBBY":
                        m_ClientInScene[sceneEvent.ClientId] = Scene.LOBBY;
                        break;

                    case "GAME_SCENE":
                        m_ClientInScene[sceneEvent.ClientId] = Scene.GAME_SCENE;
                        break;

                    default:
                        m_ClientInScene[sceneEvent.ClientId] = Scene.NONE;
                        break;
                }

                ScreenFader.Instance.FadeIn();

            }
        }

        public Scene GetClientScene(ulong clientId)
            => m_ClientInScene[clientId];
    }
}