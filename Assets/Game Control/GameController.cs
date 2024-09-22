using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Timeline;
using Random = System.Random;


/// <summary>
/// Powers available to each player.
/// </summary>
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
    /// The power to upgrade the leader into a Knight.
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


/// <summary>
/// The <c>GameController</c> class 
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class GameController : NetworkBehaviour
{
    [Header("Manna")]
    [SerializeField] private int m_MaxManna = 100;
    [SerializeField] private int[] m_PowerActivationThreshold = new int[Enum.GetNames(typeof(Power)).Length];
    [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];

    [Header("Powers")]
    [SerializeField] private int m_EarthquakeRadius = 3;
    [SerializeField] private int m_SwampRadius = 3;
    [SerializeField] private GameObject m_SwampPrefab;
    [SerializeField] private int m_VolcanoRadius = 3;
    [SerializeField, Range(0, 1)] private float m_VolcanoRockDensity = 0.4f;

    private static GameController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static GameController Instance { get => m_Instance; }

    public bool IsPlayerHosting { get => IsHost; }

    public int MaxManna { get => m_MaxManna; }
    public int[] PowerActivationThreshold { get => m_PowerActivationThreshold; }
    public int[] PowerMannaCost { get => m_PowerMannaCost; }
    public int EarthquakeRadius { get => m_EarthquakeRadius; }
    public int SwampRadius { get => m_SwampRadius; }
    public int VolcanoRadius { get => m_VolcanoRadius; }


    /// <summary>
    /// Action to be called when an event happens that might move or destroy structures i.e. modifying the terrain or raising the water level.
    /// </summary>
    public Action OnStructureTerrainDisturbed;


    public Team Winner;  // TODO: Remove - for testing only



    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    private void Start()
    {
        Vector3 size = m_SwampPrefab.GetComponent<Renderer>().bounds.size;
        float newSize = Terrain.Instance.UnitsPerTileSide;

        Vector3 scale = m_SwampPrefab.transform.localScale;
        scale.x = newSize * scale.x / size.x;
        scale.z = newSize * scale.z / size.z;
        m_SwampPrefab.transform.localScale = scale;
    }

    #endregion



    #region Structures

    /// <summary>
    /// Creates a game object in the world for a structure and sets up its occupied points.
    /// </summary>
    /// <param name="prefab">The prefab of the structure that should be spawned.</param>
    /// <param name="occupiedPoints">A <c>List</c> of the <c>MapPoint</c>s that the structure occupies.</param>
    public void SpawnStructure(GameObject prefab, (int x, int z) tile, List<MapPoint> occupiedPoints)
    {
        if (!IsServer) return;

        GameObject structureObject = Instantiate(
            prefab,
            new Vector3(
                (tile.x + 0.5f) * Terrain.Instance.UnitsPerTileSide,
                prefab.transform.position.y + Terrain.Instance.GetTileCenterHeight(tile),
                (tile.z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
            Quaternion.identity
        );

        structureObject.GetComponent<NetworkObject>().Spawn();

        Terrain.Instance.SetOccupiedTile(tile, true);

        Structure structure = structureObject.GetComponent<Structure>();
        structure.OccupiedPointHeights = occupiedPoints.ToDictionary(x => x, x => x.Y);
        structure.OccupiedTile = tile;
        OnStructureTerrainDisturbed += structure.ReactToTerrainChange;
    }

    /// <summary>
    /// Destroys a structure and cleans up references to it in the terrain.
    /// </summary>
    /// <param name="structureObject">The structure object to be destroyed.</param>
    public void DespawnStructure(GameObject structureObject)
    {
        if (!IsServer) return;

        Structure structure = structureObject.GetComponent<Structure>();
        OnStructureTerrainDisturbed -= structure.ReactToTerrainChange;

        Terrain.Instance.SetOccupiedTile(structure.OccupiedTile, false);

        structure.GetComponent<NetworkObject>().Despawn();
        Destroy(structureObject);
    }

    #endregion



    #region Powers

    #region MoldTerrain

    /// <summary>
    /// Executes the Mold Terrain power from the server.
    /// </summary>
    /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
    /// <param name="lower">Whether the point should be lowered or elevated.</param>
    public void MoldTerrain(MapPoint point, bool lower)
    {
        //Terrain.Instance.ModifyTerrain(point, lower);

        MoldTerrainServerRpc(point, lower);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoldTerrainServerRpc(MapPoint point, bool lower)
        => MoldTerrainClientRpc(point, lower);

    [ClientRpc]
    private void MoldTerrainClientRpc(MapPoint point, bool lower)
        => Terrain.Instance.ModifyTerrain(point, lower);

    #endregion


    #region Earthquake

    /// <summary>
    /// Executes the Earthquake power on server.
    /// </summary>
    /// <param name="point">The <c>MapPoint</c> at the center of the earthquake.</param>
    [ServerRpc(RequireOwnership = false)]
    public void EarthquakeServerRpc(MapPoint point)
    {
        EarthquakeClientRpc(point, new Random().Next());
    }

    [ClientRpc]
    private void EarthquakeClientRpc(MapPoint point, int randomizerSeed)
        => Terrain.Instance.CauseEarthquake(point, m_EarthquakeRadius, randomizerSeed);

    #endregion


    #region Swamp

    /// <summary>
    /// Executes the Swamp power on the server.
    /// </summary>
    /// <param name="tile">The <c>MapPoint</c> at the center of the area affected by the Swamp power.</param>
    [ServerRpc(RequireOwnership = false)]
    public void SwampServerRpc(MapPoint tile)
    {
        List<(int, int)> flatTiles = new();
        for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                if (tile.X + x >= 0 && tile.X + x < Terrain.Instance.TilesPerSide &&
                    tile.Z + z >= 0 && tile.Z + z < Terrain.Instance.TilesPerSide &&
                    Terrain.Instance.IsTileFlat((tile.X + x, tile.Z + z)))
                    flatTiles.Add((tile.X + x, tile.Z + z));


        Random random = new();
        List<int> tiles = Enumerable.Range(0, flatTiles.Count).ToList();
        int swampTiles = random.Next(Mathf.RoundToInt(flatTiles.Count * 0.5f), flatTiles.Count);

        int count = tiles.Count;
        foreach ((int x, int z) flatTile in flatTiles)
        {
            count--;
            int randomIndex = random.Next(count + 1);
            (tiles[count], tiles[randomIndex]) = (tiles[randomIndex], tiles[count]);

            if (tiles[count] <= swampTiles)
                SpawnStructure(m_SwampPrefab, flatTile, Terrain.Instance.GetTilePoints(flatTile));
        }
    }

    #endregion


    #region Volcano

    /// <summary>
    /// Executes the Volcano power on server.
    /// </summary>
    /// <param name="point">The <c>MapPoint</c> at the center of the volcano.</param>
    [ServerRpc(RequireOwnership = false)]
    public void VolcanoServerRpc(MapPoint point)
    {
        VolcanoClientRpc(point);
        Terrain.Instance.PlaceTreesAndRocks(0, m_VolcanoRockDensity, 0);
    }

    [ClientRpc]
    private void VolcanoClientRpc(MapPoint point)
        => Terrain.Instance.CauseVolcano(point, m_VolcanoRadius);

    #endregion


    #region Flood

    /// <summary>
    /// Executes the Flood power on server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void FloodServerRpc()
    {
        if (Terrain.Instance.WaterLevel == Terrain.Instance.MaxHeight)
            return;

        FloodClientRpc();
    }

    [ClientRpc]
    private void FloodClientRpc()
    {
        Terrain.Instance.RaiseWaterLevel();
        Water.Instance.Raise();

        if (IsHost)
            OnStructureTerrainDisturbed?.Invoke();
    }

    #endregion


    #endregion
}