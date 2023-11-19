using UnityEngine;
using Image = UnityEngine.UI.Image;
using TMPro;
using Steamworks;

public class LobbyEntry : MonoBehaviour
{
    public SteamId Id { get; private set; }

    public GameObject DeselectedBackground;
    public GameObject SelectedBackground;
    public TMP_Text ServerName;
    public TMP_Text OwnerName;
    public Image Lock;

    private bool isOn = false;


    public void Setup(SteamId id, string serverName, string ownerName, bool hasPassword)
    {
        Id = id;
        ServerName.text = serverName;
        OwnerName.text = ownerName;
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
