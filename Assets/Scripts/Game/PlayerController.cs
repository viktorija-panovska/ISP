using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    public static PlayerController Instance;

    public GameObject CameraControllerPrefab;
    public GameObject GameHUDPrefab;

    private CameraController cameraController;
    private Camera playerCamera;
    private GameHUD hud;

    private bool isGamePaused = false;
    private Teams team = Teams.None;
    private Powers activePower = Powers.MoldTerrain;
    private Unit leader = null;

    public int Mana { get; private set; }

    private WorldLocation lastClickedVertex = new(-1, -1);
    private WorldLocation? flagLocation = null;



    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Instance = this;

        team = OwnerClientId == 0 ? Teams.Red : Teams.Blue;

        // Set camera controller
        cameraController = Instantiate(CameraControllerPrefab).GetComponent<CameraController>();
        playerCamera = cameraController.MainCamera;

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

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);

                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    GameController.Instance.UpdateMapServerRpc(location, decrease: false);
                    GameController.Instance.AdjustUnitHeightsServerRpc();
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    GameController.Instance.UpdateMapServerRpc(location, decrease: true);
                    GameController.Instance.AdjustUnitHeightsServerRpc();
                }
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

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint) /*&& (leader != null || flagLocation != null)*/)
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    if (location.X == lastClickedVertex.X && location.Z == lastClickedVertex.Z)
                        return;

                    lastClickedVertex = location;
                    flagLocation = location;


                    RemoveMana(GameController.Instance.PowerCost[index]);
                    GameController.Instance.SpawnFlagServerRpc(location);
                    activePower = Powers.MoldTerrain;
                    hud.SwitchMarker(0);

                    GameController.Instance.MoveUnitsServerRpc(OwnerClientId, location);
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

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, false);

                if (Input.GetMouseButtonDown(0))
                {
                    GameController.Instance.LowerTerrainInAreaServerRpc(location);
                    RemoveMana(GameController.Instance.PowerCost[index]);
                    activePower = Powers.MoldTerrain;
                    GameController.Instance.AdjustUnitHeightsServerRpc();
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

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
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
}
