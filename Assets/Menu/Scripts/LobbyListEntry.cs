using Steamworks.Data;
using TMPro;
using UnityEngine;

using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;


namespace Populous
{
    /// <summary>
    /// The <c>LobbyListEntry</c> class represents an object in the lobby list in the main menu that represents a lobby.
    /// </summary>
    public class LobbyListEntry : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The image in the background of the entry.")]
        [SerializeField] private Image m_Background;
        [Tooltip("The color of the background image when the entry is selected.")]
        [SerializeField] private Color m_SelectedColor;
        [Tooltip("The text field in which the name of the lobby should be shown.")]
        [SerializeField] private TMP_Text m_LobbyNameField;
        [Tooltip("The text field in which the name of the player hosting the lobby should be shown.")]
        [SerializeField] private TMP_Text m_OwnerNameField;

        #endregion


        private Lobby m_Lobby;
        /// <summary>
        /// Gets the lobby represented by this lobby entry.
        /// </summary>
        public Lobby Lobby { get => m_Lobby; }

        private string m_LobbyName;
        /// <summary>
        /// Gets the name of the lobby represented by this entry.
        /// </summary>
        public string LobbyName { get => m_LobbyName; }

        /// <summary>
        /// The color of the lobby entry when it hasn't been selected.
        /// </summary>
        private Color m_DeselectedColor;

        /// <summary>
        /// True if the entry has been selected, false if it hasn't.
        /// </summary>
        private bool m_IsSelected;


        /// <summary>
        /// Setup this lobby entry to reflect the lobby it represents.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> this lobby entry represents.</param>
        public void Setup(Lobby lobby)
        {
            m_Lobby = lobby;
            m_LobbyName = lobby.GetData("name");

            m_LobbyNameField.text = m_LobbyName;
            m_OwnerNameField.text = m_Lobby.GetData("owner");

            m_DeselectedColor = m_Background.color;
        }


        #region Entry Selection

        /// <summary>
        /// Selects this entry if it hasn't been selected, deselects it if it has.
        /// </summary>
        /// <param name="_">Toggle parameter - not in use</param>
        public void OnSelected(bool _)
        {
            if (!m_IsSelected)
                Select();
            else
                Deselect(updateMenu: true);
        }

        /// <summary>
        /// Selects the lobby entry.
        /// </summary>
        private void Select()
        {
            Debug.Log("Select");
            m_IsSelected = true;
            InterfaceUtils.SwitchColor(m_Background, m_SelectedColor);
            Debug.Log(m_Background.color);
            MainMenu.Instance.SetSelectedEntry(this);
        }

        /// <summary>
        /// Deselects the lobby entry.
        /// </summary>
        public void Deselect(bool updateMenu)
        {
            m_IsSelected = false;
            InterfaceUtils.SwitchColor(m_Background, m_DeselectedColor);

            if (updateMenu)
                MainMenu.Instance.SetSelectedEntry(null);
        }

        #endregion
    }
}