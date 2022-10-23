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
    public LevelGenerator LevelGen;
    private Powers activePower = Powers.MoldTerrain;

    private List<Unit> activeUnits = new List<Unit>();


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


    public void AddUnit(WorldLocation unitLocation)
    {
        activeUnits.Add(new Unit(unitLocation, Team.Red));
    }


    private void MoldTerrain()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
            {
                Chunk chunk = LevelGen.WorldMap.GetChunkAtPosition(hitInfo.point);
                chunk.ModifyBlock(hitInfo.point, remove: false);
            }
        }       
        else if (Input.GetMouseButtonDown(1))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
            {
                Chunk chunk = LevelGen.WorldMap.GetChunkAtPosition(hitInfo.point);
                chunk.ModifyBlock(hitInfo.point, remove: true);
            }
        }
    }


    private void GuideFollowers()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
            {
                Vector3 point = hitInfo.point;
                Chunk chunk = LevelGen.WorldMap.GetChunkAtPosition(point);
                (int x, int y, int z) = chunk.GetIndicesFromPosition(new Vector3(point.x, point.y - 0.5f, point.z));

                foreach (Unit unit in activeUnits)
                {
                    List<WorldLocation> path = Pathfinding.FindPath(unit.Location, new WorldLocation(chunk, x, y, z));

                    if (path != null)
                        unit.MoveUnit(path);
                }
            }
        }
    }
}
