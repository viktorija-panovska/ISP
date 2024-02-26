using Steamworks;
using TMPro;
using UnityEngine;

using Image = UnityEngine.UI.Image;



public class LobbyEntry : MonoBehaviour
{
    public SteamId Id { get; private set; }
    public bool HasPassword { get; private set; }

    public GameObject DeselectedBackground;
    public GameObject SelectedBackground;
    public TMP_Text ServerName;
    public Image Lock;

    private bool isOn = false;


    public void Setup(SteamId id, string serverName, bool hasPassword)
    {
        Id = id;
        HasPassword = hasPassword;

        ServerName.text = serverName;
        Lock.gameObject.SetActive(hasPassword);        
    }


    public void OnSelected(bool _)
    {
        if (!isOn)
        {
            SteamMainMenu.Instance.SelectEntry(this);
            Select();
        }
        else
        {
            SteamMainMenu.Instance.DeselectEntry();
            Deselect();
        }
    }

    public void Select()
    {
        DeselectedBackground.SetActive(false);
        SelectedBackground.SetActive(true);
        isOn = true;
    }

    public void Deselect()
    {
        SelectedBackground.SetActive(false);
        DeselectedBackground.SetActive(true);
        isOn = false;
    }
}
