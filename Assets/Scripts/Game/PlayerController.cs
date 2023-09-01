using Unity.Netcode;
using UnityEngine;


public interface IPlayerObject
{
}


[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    public static PlayerController Instance;

    public GameObject CameraControllerPrefab;
    public GameObject GameHUDPrefab;

    private CameraController cameraController;
    private GameHUD hud;

    private bool isGamePaused = false;
    private Teams team = Teams.None;
    private Powers activePower = Powers.MoldTerrain;

    public Camera PlayerCamera { get; private set; }
    public int Mana { get; private set; }



    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Instance = this;

        team = OwnerClientId == 0 ? Teams.Red : Teams.Blue;

        // Set camera controller
        cameraController = Instantiate(CameraControllerPrefab).GetComponent<CameraController>();
        cameraController.SetStart(OwnerClientId);
        PlayerCamera = cameraController.MainCamera;

        // Set HUD
        GameObject HUD = Instantiate(GameHUDPrefab);
        hud = HUD.GetComponent<GameHUD>();
        hud.SetController(this);
        cameraController.SetGameHUD(hud);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isGamePaused) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            activePower = Powers.MoldTerrain;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            activePower = Powers.GuideFollowers;
        if (Input.GetKeyDown(KeyCode.Alpha3))
            activePower = Powers.Earthquake;
        if (Input.GetKeyDown(KeyCode.Alpha4))
            activePower = Powers.Swamp;
        if (Input.GetKeyDown(KeyCode.Alpha5))
            activePower = Powers.Crusade;
        if (Input.GetKeyDown(KeyCode.Alpha6))
            activePower = Powers.Flood;
        if (Input.GetKeyDown(KeyCode.Alpha7))
            activePower = Powers.Armageddon;

        if (GameController.Instance.PowerCost[(int)activePower] > Mana)
            activePower = Powers.MoldTerrain;

        switch (activePower)
        {
            case Powers.MoldTerrain:
                MoldTerrain();
                break;

            case Powers.GuideFollowers:
                GuideFollowers();
                break;

            case Powers.Earthquake:
                CauseEarthquake();
                break;

            case Powers.Swamp:
                PlaceSwamp();
                break;

            case Powers.Crusade:
                SendKnight();
                break;

            case Powers.Flood:
                CreateFlood();
                break;

            case Powers.Armageddon:
                StartArmageddon();
                break;
        }
    }

    public void PauseGame()
    {
        isGamePaused = true;
    }

    public void ResumeGame()
    {
        isGamePaused = false;
    }



    public void AddMana(int manaGain)
    {
        if (Mana == GameController.MAX_MANA)
            return;
        else if (Mana + manaGain > GameController.MAX_MANA)
            Mana = GameController.MAX_MANA;
        else
            Mana += manaGain;

        hud.UpdateManaBar(Mana);
    }

    private void RemoveMana(int manaLoss)
    {
        if (Mana == GameController.MIN_MANA)
            return;
        else if (Mana - manaLoss < GameController.MIN_MANA)
            Mana = GameController.MIN_MANA;
        else
            Mana -= manaLoss;

        hud.UpdateManaBar(Mana);


    }

    public void SwitchCameras(bool isMapCamera)
    {
        if (isMapCamera)
            PauseGame();
        else
            ResumeGame();

        cameraController.SwitchCameras(isMapCamera);
    }



    private void MoldTerrain()
    {
        int index = (int)Powers.MoldTerrain;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitInfo))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);

                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                    GameController.Instance.UpdateMapServerRpc(location, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    GameController.Instance.UpdateMapServerRpc(location, decrease: true);
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    private void GuideFollowers()
    {
        int index = (int)Powers.GuideFollowers;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitInfo))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(GameController.Instance.PowerCost[index]);
                    GameController.Instance.MoveUnitsServerRpc(team, location);

                    activePower = Powers.MoldTerrain;
                    hud.SwitchMarker(0);
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    private void CauseEarthquake()
    {
        int index = (int)Powers.Earthquake;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitInfo))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, false);

                if (Input.GetMouseButtonDown(0))
                {
                    GameController.Instance.LowerTerrainInAreaServerRpc(location);
                    RemoveMana(GameController.Instance.PowerCost[index]);
                    activePower = Powers.MoldTerrain;
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index, false);
            }
        }
    }


    private void PlaceSwamp()
    {
        int index = (int)Powers.Swamp;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitInfo))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(GameController.Instance.PowerCost[index]);
                    activePower = Powers.MoldTerrain;
                    GameController.Instance.SpawnSwampServerRpc(location);
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    private void SendKnight()
    {
        if (GameController.Instance.HasLeader(team))
        {
            RemoveMana(GameController.Instance.PowerCost[(int)Powers.Crusade]);
            GameController.Instance.SendKnightServerRpc(team);
        }

        activePower = Powers.MoldTerrain;
    }


    private void CreateFlood()
    {
        GameController.Instance.IncreaseWaterLevelServerRpc();
        RemoveMana(GameController.Instance.PowerCost[(int)Powers.Flood]);
        activePower = Powers.MoldTerrain;
    }


    private void StartArmageddon()
    {
        GameController.Instance.StartArmageddonServerRpc();
        RemoveMana(GameController.Instance.PowerCost[(int)Powers.Armageddon]);
        activePower = Powers.MoldTerrain;
    }
}