using System.Collections.Generic;
using UnityEngine;


public class Structure : MonoBehaviour
{
    [SerializeField] protected bool m_DrownToDestroy = false;

    protected Dictionary<MapPoint, int> m_OccupiedPointHeights = new();
    /// <summary>
    /// Gets and sets the occupied points and their heights at the time of the structure's creation.
    /// </summary>
    public Dictionary<MapPoint, int> OccupiedPointHeights { get => m_OccupiedPointHeights; set { m_OccupiedPointHeights = value; } }

    protected (int x, int z) m_OccupiedTile;
    /// <summary>
    /// Gets the index of the tile on the grid that the structure occupies.
    /// </summary>
    public (int x, int z) OccupiedTile { get => m_OccupiedTile; set {  m_OccupiedTile = value; } }


    /// <summary>
    /// Change the height or destroy the structure depending on the change of the height of the terrain beneath it.
    /// </summary>
    public void ReactToTerrainChange()
    {
        bool isTileUnderwater = true;
        bool hasHeightChanged = false;

        foreach ((MapPoint point, int height) in m_OccupiedPointHeights)
        {
            if (m_DrownToDestroy && point.Y > Terrain.Instance.WaterLevel)
                isTileUnderwater = false;

            if (point.Y != height || point.Y <= Terrain.Instance.WaterLevel)
            {
                if (!m_DrownToDestroy)
                {
                    GameController.Instance.DespawnStructure(gameObject);
                    return;
                }
                else
                {
                    hasHeightChanged = true;
                }
            }
        }

        if (!m_DrownToDestroy)
            return;

        if (isTileUnderwater)
            GameController.Instance.DespawnStructure(gameObject);
        else if (hasHeightChanged)
            transform.position = new Vector3(
                transform.position.x, 
                Terrain.Instance.GetTileCenterHeight(m_OccupiedTile), 
                transform.position.z
            );
    }
}