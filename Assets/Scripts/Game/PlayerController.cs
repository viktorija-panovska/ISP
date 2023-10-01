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
    private Powers activePower = Powers.MoldTerrain;

    public Teams Team { get; private set; }
    public Camera PlayerCamera { get; private set; }
    public float Mana { get; private set; }

    private int objectsInView =1;



    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Team = OwnerClientId == 0 ? Teams.Red : Teams.Blue;

        // Set HUD
        GameObject HUD = Instantiate(GameHUDPrefab);
        hud = HUD.GetComponent<GameHUD>();
        hud.SetController(this);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isGamePaused || cameraController == null) return;

        if (Input.GetKeyDown(KeyCode.L))
        {
            if (GameController.Instance.HasLeader(Team))
                GameController.Instance.SnapToLeaderServerRpc();
            else
                hud.FlashLeaderIcon();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            if (GameController.Instance.HasKnight(Team))
                GameController.Instance.SnapToKnightServerRpc();
            else
                hud.FlashKnightIcon();
        }

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

        if (GameController.Instance.PowerActivateLevel[(int)activePower] > Mana)
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

    public void EndGame(Teams winner)
    {
        PauseGame();
        hud.OpenEndGameMenu(winner);
    }

    public void RestartGame(WorldLocation cameraStart)
    {
        activePower = Powers.MoldTerrain;
        Mana = 0;
        objectsInView = 1;

        cameraController.ResetCamera(cameraStart);
        hud.ResetHUD();

        ResumeGame();
    }


    #region Camera Controller

    public void SetupCameraController(WorldLocation cameraStart)
    {
        cameraController = Instantiate(CameraControllerPrefab).GetComponent<CameraController>();
        PlayerCamera = cameraController.MainCamera;
        cameraController.SetGameHUD(hud);
        SetCameraLocation(cameraStart);
    }

    public void SetCameraLocation(WorldLocation location)
    {
        cameraController.SetLocation(location);
    }

    public void SwitchCameras(bool isMapCamera)
    {
        if (isMapCamera)
            PauseGame();
        else
            ResumeGame();

        cameraController.SwitchCameras(isMapCamera);
    }

    #endregion



    public void AddMana(float manaGain)
    {
        if (Mana == GameController.MAX_MANA)
            return;
        else if (Mana + manaGain > GameController.MAX_MANA)
            Mana = GameController.MAX_MANA;
        else
            Mana += manaGain;

        hud.UpdateManaBar(Mana);
    }

    public void RemoveMana(float manaLoss)
    {
        if (Mana == GameController.MIN_MANA)
            return;
        else if (Mana - manaLoss < GameController.MIN_MANA)
            Mana = GameController.MIN_MANA;
        else
            Mana -= manaLoss;

        hud.UpdateManaBar(Mana);


    }


    public void AddObjectInView() => objectsInView++;

    public void RemoveObjectFromView() => objectsInView--;



    #region Powers

    private void MoldTerrain()
    {
        int index = (int)Powers.MoldTerrain;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitInfo) && objectsInView > 0)
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
                    GameController.Instance.MoveUnitsServerRpc(Team, location);

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
        if (GameController.Instance.HasLeader(Team))
        {
            RemoveMana(GameController.Instance.PowerCost[(int)Powers.Crusade]);
            GameController.Instance.SendKnightServerRpc(Team);
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

    #endregion
}