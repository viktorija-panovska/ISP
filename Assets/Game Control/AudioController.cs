using UnityEngine;

namespace Populous
{
    /// <summary>
    /// Types of <c>AudioClips</c> that can be played.
    /// </summary>
    public enum SoundType
    {
        /// <summary>
        /// Background music for the main menu and lobby.
        /// </summary>
        MENU_MUSIC,
        /// <summary>
        /// Sound for the press of a menu button.
        /// </summary>
        MENU_BUTTON,
        /// <summary>
        /// Sound to notify of a missing or incorrect input.
        /// </summary>
        MENU_WRONG,
        /// <summary>
        /// Sound to notify of the blue player ready button being selected.
        /// </summary>
        BLUE_READY,
        /// <summary>
        /// Sound to notify of the blue player ready button being deselected.
        /// </summary>
        BLUE_NOT_READY,
        /// <summary>
        /// Sound to notify of the blue player connecting to the game.
        /// </summary>
        BLUE_CONNECT,
        /// <summary>
        /// Sound to notify of the blue player disconnecting from the game.
        /// </summary>
        BLUE_DISCONNECT
    }


    [RequireComponent(typeof(AudioSource))]
    public class AudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip[] m_SoundClips;

        private static AudioController m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static AudioController Instance { get => m_Instance; }

        private AudioSource m_AudioSource;


        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);

            m_AudioSource = GetComponent<AudioSource>();
        }

        #endregion


        /// <summary>
        /// Plays the <c>AudioClip</c> at a given volume.
        /// </summary>
        /// <param name="sound">The <c>AudioClip</c> to be played.</param>
        /// <param name="volume">The volume at which the sound should play.</param>
        public void PlaySound(SoundType sound, float volume = 1f)
        {
            m_AudioSource.PlayOneShot(m_SoundClips[(int)sound], volume);
        }
    }
}