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
    private Powers activePower = Powers.MoldTerrain;


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
        }
    }


    private void MoldTerrain()
    {
        // spawn a clicky grid
        // if clicky on vertex, get chunk from WorldMap and then call UpdateChunkAtVertex
    }
}
