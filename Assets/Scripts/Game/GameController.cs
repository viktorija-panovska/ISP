using System.Collections.Generic;
using UnityEngine;


public enum Powers
{
    MoldTerrain,
    GuideFollowers,
    Earthquake,
    Swamp,
    Crusade,
    Volcano,
    Flood,
    Armageddon
}


public class GameController : MonoBehaviour
{
    public Texture2D ClickyCursorTexture;

    private Powers activePower = Powers.MoldTerrain;
    private readonly List<Unit> activeUnits = new();


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            activePower = Powers.MoldTerrain;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            activePower = Powers.GuideFollowers;

        switch (activePower)
        {
            case Powers.MoldTerrain:
                MoldTerrain();
                break;

            case Powers.GuideFollowers:
                GuideFollowers();
                break;
        }
    }


    public void AddUnit(GameObject unitObject, WorldLocation worldPosition)
    {
        activeUnits.Add(new Unit(unitObject, worldPosition, Team.Red));
    }


    private bool IsClickable(Vector3 hitPoint)
    {
        if (Mathf.Abs(Mathf.Round(hitPoint.x / Chunk.TileWidth) - hitPoint.x / Chunk.TileWidth) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.y / Chunk.StepHeight) - hitPoint.y / Chunk.StepHeight) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.z / Chunk.TileWidth) - hitPoint.z / Chunk.TileWidth) < 0.1)
        {
            Cursor.SetCursor(
                ClickyCursorTexture,
                new Vector2(ClickyCursorTexture.width / 2, ClickyCursorTexture.height / 2),
                CursorMode.Auto
            );

            return true;
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return false;
        }
    }


    private void MoldTerrain()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);

                if (Input.GetMouseButtonDown(0))
                    WorldMap.UpdateMap(location, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    WorldMap.UpdateMap(location, decrease: true);
            }
        }
    }


    private void GuideFollowers()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (IsClickable(hitPoint))
            {
                WorldLocation endLocation = new(hitPoint.x, hitPoint.z);

                if (Input.GetMouseButtonDown(0))
                {
                    foreach (Unit unit in activeUnits)
                    {
                        List<WorldLocation> path = Pathfinding.FindPath(unit.PositionInWorldMap, endLocation);

                        if (path != null && path.Count > 0)
                            unit.MoveUnit(path);
                    }
                }
            }
        }
    }
}
