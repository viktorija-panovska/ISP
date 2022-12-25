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
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (Mathf.Abs(Mathf.Round(hitPoint.x / Chunk.TileWidth) - hitPoint.x / Chunk.TileWidth) < 0.1 &&
                Mathf.Abs(Mathf.Round(hitPoint.y / Chunk.StepHeight) - hitPoint.y / Chunk.StepHeight) < 0.1 &&
                Mathf.Abs(Mathf.Round(hitPoint.z / Chunk.TileWidth) - hitPoint.z / Chunk.TileWidth) < 0.1)
            {
                Cursor.SetCursor(ClickyCursorTexture, new Vector2(ClickyCursorTexture.width / 2, ClickyCursorTexture.height / 2), CursorMode.Auto);

                if (Input.GetMouseButtonDown(0))
                    WorldMap.UpdateMap(hitPoint, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    WorldMap.UpdateMap(hitPoint, decrease: true);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}
