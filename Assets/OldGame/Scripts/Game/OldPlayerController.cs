using Unity.Netcode;
using UnityEngine;


public interface IPlayerObject
{
}

public enum Power
{
    /// <summary>
    /// The power to either elevate or lower a point on the terrain.
    /// </summary>
    MOLD_TERRAIN,
    /// <summary>
    /// The power to place a beacon that the followers will flock to.
    /// </summary>
    GUIDE_FOLLOWERS,
    /// <summary>
    /// The power to lower all the points in a set area.
    /// </summary>
    EARTHQUAKE,
    /// <summary>
    /// The power to place a swamp at a point which will destroy any follower that walks into it.
    /// </summary>
    SWAMP,
    /// <summary>
    /// The power to upgrade the leader into a KNIGHT.
    /// </summary>
    KNIGHT,
    /// <summary>
    /// The power to elevate the terrain in a set area and scatter rocks across it.
    /// </summary>
    VOLCANO,
    /// <summary>
    /// The power to increase the water height by one level.
    /// </summary>
    FLOOD,
    /// <summary>
    /// The power to 
    /// </summary>
    ARMAGHEDDON
}

[RequireComponent(typeof(NetworkObject))]
public class OldPlayerController : NetworkBehaviour
{
    public static OldPlayerController Instance;

    public GameObject CameraControllerPrefab;
    public GameObject GameHUDPrefab;

    private OldCameraController cameraController;
    private GameHUD hud;

    private bool isGamePaused = false;
    private Power activePower = Power.MOLD_TERRAIN;

    public Team Team { get; private set; }
    public Camera PlayerCamera { get; private set; }
    public float Mana { get; private set; }

    private int objectsInView = 1;



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

        Team = OwnerClientId == 0 ? Team.RED : Team.BLUE;

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
            if (OldGameController.Instance.HasLeader(Team))
                OldGameController.Instance.SnapToLeaderServerRpc();
            else
                hud.FlashLeaderIcon();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            if (OldGameController.Instance.HasKnight(Team))
                OldGameController.Instance.SnapToKnightServerRpc();
            else
                hud.FlashKnightIcon();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            activePower = Power.MOLD_TERRAIN;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            activePower = Power.GUIDE_FOLLOWERS;
        if (Input.GetKeyDown(KeyCode.Alpha3))
            activePower = Power.EARTHQUAKE;
        if (Input.GetKeyDown(KeyCode.Alpha4))
            activePower = Power.SWAMP;
        if (Input.GetKeyDown(KeyCode.Alpha5))
            activePower = Power.KNIGHT;
        if (Input.GetKeyDown(KeyCode.Alpha6))
            activePower = Power.FLOOD;
        if (Input.GetKeyDown(KeyCode.Alpha7))
            activePower = Power.ARMAGHEDDON;

        if (OldGameController.Instance.PowerActivateLevel[(int)activePower] > Mana)
            activePower = Power.MOLD_TERRAIN;

        switch (activePower)
        {
            case Power.MOLD_TERRAIN:
                MoldTerrain();
                break;

            case Power.GUIDE_FOLLOWERS:
                GuideFollowers();
                break;

            case Power.EARTHQUAKE:
                CauseEarthquake();
                break;

            case Power.SWAMP:
                PlaceSwamp();
                break;

            case Power.KNIGHT:
                SendKnight();
                break;

            case Power.FLOOD:
                CreateFlood();
                break;

            case Power.ARMAGHEDDON:
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

    public void EndGame(Team winner)
    {
        PauseGame();
        hud.OpenEndGameMenu(winner);
    }

    public void RestartGame(WorldLocation cameraStart)
    {
        activePower = Power.MOLD_TERRAIN;
        Mana = 0;
        objectsInView = 1;

        cameraController.ResetCamera(cameraStart);
        hud.ResetHUD();

        ResumeGame();
    }


    #region Camera Controller

    public void SetupCameraController(WorldLocation cameraStart, GameObject viewZone)
    {
        cameraController = Instantiate(CameraControllerPrefab).GetComponent<OldCameraController>();
        PlayerCamera = cameraController.MainCamera;
        cameraController.SetGameHUD(hud);
        cameraController.SetViewZone(viewZone);
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
        if (Mana == OldGameController.MAX_MANA)
            return;
        else if (Mana + manaGain > OldGameController.MAX_MANA)
            Mana = OldGameController.MAX_MANA;
        else
            Mana += manaGain;

        hud.UpdateManaBar(Mana);
    }

    public void RemoveMana(float manaLoss)
    {
        if (Mana == OldGameController.MIN_MANA)
            return;
        else if (Mana - manaLoss < OldGameController.MIN_MANA)
            Mana = OldGameController.MIN_MANA;
        else
            Mana -= manaLoss;

        hud.UpdateManaBar(Mana);


    }


    public void AddObjectInView() => objectsInView++;

    public void RemoveObjectFromView() => objectsInView--;


    #region Powers

    private void MoldTerrain()
    {
        int index = (int)Power.MOLD_TERRAIN;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint) && objectsInView > 0)
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);

                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                    OldGameController.Instance.UpdateMapServerRpc(location, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    OldGameController.Instance.UpdateMapServerRpc(location, decrease: true);
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    private void GuideFollowers()
    {
        int index = (int)Power.GUIDE_FOLLOWERS;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(OldGameController.Instance.PowerCost[index]);
                    OldGameController.Instance.MoveUnitsServerRpc(Team, location);

                    activePower = Power.MOLD_TERRAIN;
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
        int index = (int)Power.EARTHQUAKE;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, false);

                if (Input.GetMouseButtonDown(0))
                {
                    OldGameController.Instance.LowerTerrainInAreaServerRpc(location);
                    RemoveMana(OldGameController.Instance.PowerCost[index]);
                    activePower = Power.MOLD_TERRAIN;
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
        int index = (int)Power.SWAMP;
        hud.SwitchMarker(index);

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(OldGameController.Instance.PowerCost[index]);
                    activePower = Power.MOLD_TERRAIN;
                    OldGameController.Instance.SpawnSwampServerRpc(location);
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
        if (OldGameController.Instance.HasLeader(Team))
        {
            RemoveMana(OldGameController.Instance.PowerCost[(int)Power.KNIGHT]);
            OldGameController.Instance.SendKnightServerRpc(Team);
        }

        activePower = Power.MOLD_TERRAIN;
    }


    private void CreateFlood()
    {
        OldGameController.Instance.IncreaseWaterLevelServerRpc();
        RemoveMana(OldGameController.Instance.PowerCost[(int)Power.FLOOD]);
        activePower = Power.MOLD_TERRAIN;
    }


    private void StartArmageddon()
    {
        OldGameController.Instance.StartArmageddonServerRpc();
        RemoveMana(OldGameController.Instance.PowerCost[(int)Power.ARMAGHEDDON]);
        activePower = Power.MOLD_TERRAIN;
    }

    #endregion
}