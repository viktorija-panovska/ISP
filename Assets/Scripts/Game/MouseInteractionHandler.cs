using UnityEngine;


[RequireComponent(typeof(MapGenerator))]
public class MouseInteractionHandler : MonoBehaviour
{
    private Transform selector;

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (GetComponent<MeshCollider>().Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
        {
            int x = Mathf.FloorToInt(hitInfo.point.x / GetComponent<MapGenerator>().TileSize);
            int z = Mathf.FloorToInt(hitInfo.point.y / GetComponent<MapGenerator>().TileSize);
            //Debug.Log("Tile: " + x + ", " + z);

            selector.transform.position = new Vector3(x, 0, z);
        }
    }
}
