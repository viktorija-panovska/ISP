using DG.Tweening;
using Steamworks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;


/// <summary>
/// This class represents an entry in the lobby selection list in the Main Menu UI.
/// </summary>
public class LobbyEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text m_LobbyNameField;
    [SerializeField] private RawImage m_OwnerAvatar;
    [SerializeField] private Image m_Background;
    [SerializeField] private Image m_Lock;

    private SteamId m_LobbyId;
    private string m_LobbyName;
    private bool m_HasPassword;
    private bool m_IsOn;

    /// <summary>
    /// Gets the SteamID of the lobby.
    /// </summary>
    public SteamId LobbyId { get => m_LobbyId; }
    /// <summary>
    /// Gets the lobby name.
    /// </summary>
    public string LobbyName { get => m_LobbyName; }
    /// <summary>
    /// Gets a value indicating whether the lobby is password protected.
    /// </summary>
    public bool HasPassword { get => m_HasPassword; }

    /// <summary>
    /// Called when entry is selected.
    /// </summary>
    public Action<LobbyEntry> OnEntrySelected;
    /// <summary>
    /// Called when entry is deselected.
    /// </summary>
    public Action OnEntryDeselected;


    /// <summary>
    /// Sets lobby ID and password protection status, and fills in lobby name and lobby owner avatar.
    /// </summary>
    /// <param name="lobbyId">The SteamID of the lobby.</param>
    /// <param name="lobbyName">The name of the lobby</param>
    /// <param name="hasPassword">The value indicating whether the lobby is password protected.</param>
    /// <param name="owner">The SteamID of the owner of the lobby.</param>
    public async void Setup(SteamId lobbyId, string lobbyName, bool hasPassword, SteamId ownerId)
    {
        m_LobbyId = lobbyId;
        m_LobbyName = lobbyName;
        m_HasPassword = hasPassword;

        m_LobbyNameField.text = lobbyName;
        m_OwnerAvatar.texture = await InterfaceUtils.GetSteamAvatar(ownerId);
        m_Lock.gameObject.SetActive(hasPassword);
    }

    /// <summary>
    /// Deselects entry if selected, selects entry if deselected.
    /// </summary>
    /// <param name="_">Toggle parameter - not in use</param>
    public void OnSelected(bool _)
    {
        if (!m_IsOn)
            Select();
        else
            Deselect();
    }

    /// <summary>
    /// Selects entry and notifies listeners of selection.
    /// </summary>
    public void Select()
    {
        OnEntrySelected?.Invoke(this);
        InterfaceUtils.SwitchColor(m_Background, Color.green);
        m_IsOn = true;
    }

    /// <summary>
    /// Deselects entry and notifies listeners of deselection.
    /// </summary>
    public void Deselect()
    {
        OnEntryDeselected?.Invoke();
        InterfaceUtils.SwitchColor(m_Background, Color.gray);
        m_IsOn = false;
    }
}
