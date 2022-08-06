using UnityEngine;


[RequireComponent(typeof(MapGenerator))]
public class MouseInteractionHandler : MonoBehaviour
{
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Input.GetMouseButtonDown(1))
            Debug.Log("HI");

        if (Input.GetMouseButtonDown(1) && GetComponent<MeshCollider>().Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
        {
            int x = Mathf.FloorToInt(hitInfo.point.x / GetComponent<MapGenerator>().TileSize);
            int z = Mathf.FloorToInt(hitInfo.point.y / GetComponent<MapGenerator>().TileSize);
            Debug.Log("Tile: " + x + ", " + z);
        }
    }
}
