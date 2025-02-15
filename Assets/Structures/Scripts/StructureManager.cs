using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>StructureManager</c> class is a <c>MonoBehavior</c> which manages all the structures in the game.
    /// </summary>
    public class StructureManager : NetworkBehaviour
    {
        [SerializeField] private GameObject[] m_MagnetPrefabs;
        [SerializeField] private GameObject m_SwampPrefab;
        [SerializeField] private GameObject m_FieldPrefab;

        [Header("Settlements")]
        [SerializeField] private GameObject m_SettlementPrefab;

        [Header("Trees and Rocks Properties")]
        [SerializeField, Range(0, 1)] private float m_TreePercentage;
        [SerializeField, Range(0, 1)] private float m_BlackRockPercentage;
        [SerializeField, Range(0, 1)] private float m_WhiteRockPercentage;
        [SerializeField, Range(0, 1)] private float m_VolcanoRockPercentage;
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject m_BlackRockPrefab;
        [SerializeField] private GameObject m_WhiteRockPrefab;

        private static StructureManager m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static StructureManager Instance { get => m_Instance; }

        /// <summary>
        /// A list of the team symbols for each team.
        /// </summary>
        private UnitMagnet[] m_UnitMagnets;
        /// <summary>
        /// An array of lists of the tiles occupied by settlements for each team.
        /// </summary>
        private readonly List<Vector2>[] m_SettlementLocations = new List<Vector2>[] { new(), new() };

        /// <summary>
        /// Action to be called when a settlement is despawned to remove references to it from other objects.
        /// </summary>
        public Action<Settlement> OnRemoveReferencesToSettlement;

        /// <summary>
        /// An array of the leaders in each team that are part of a settlement, null if the team's leader is not in a settlement.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly Settlement[] m_LeaderSettlements = new Settlement[2];

        /// <summary>
        /// Each cell the <c>Structure</c> occupying the tile with corresponding coordinates, or null if there isn't one.
        /// </summary>
        private Structure[,] m_StructureOnTile;



        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            m_StructureOnTile = new Structure[Terrain.Instance.TilesPerSide, Terrain.Instance.TilesPerSide];
            GameUtils.ResizeGameObject(m_SwampPrefab, Terrain.Instance.UnitsPerTileSide);

            foreach (GameObject flag in m_MagnetPrefabs)
                GameUtils.ResizeGameObject(flag, 10, scaleY: true);
        }


        #region Structure Location

        /// <summary>
        /// Checks whether there is a structure on the given tile.
        /// </summary>
        /// <param name="tile">The coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is occupied by a structure, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) tile) => m_StructureOnTile[tile.z, tile.x];

        /// <summary>
        /// Gets the <c>Structure</c> occupying the given tile.
        /// </summary>
        /// <param name="tile">The coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <returns>The <c>Structure</c> occupying the given tile, or <c>null</c> if the tile is unoccupied.</returns>
        public Structure GetStructureOnTile((int x, int z) tile) => m_StructureOnTile[tile.z, tile.x];

        /// <summary>
        /// Sets the given structure to occupy the given tile.
        /// </summary>
        /// <param name="tile">The coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <param name="structure">The <c>Structure</c> that should be placed on the tile.</param>
        public void SetOccupiedTile((int x, int z) tile, Structure structure) => m_StructureOnTile[tile.z, tile.x] = structure;

        public bool HasTileSettlement((int x, int z) tile) => GetStructureOnTile(tile).GetType() == typeof(Settlement);

        #endregion


        #region Spawn / Despawn

        /// <summary>
        /// Creates a structure of the given team on the given tile and spawns it on the network.
        /// </summary>
        /// <param name="prefab">The <c>GameObject</c> for the structure that should be created.</param>
        /// <param name="tile">The tile the created structure should occupy.</param>
        /// <param name="occupiedPoints">A list of the points that the created structure should occupy.</param>
        /// <param name="team">The team the created structure should belong to.</param>
        /// <returns>The <c>GameObject</c> of the created structure.</returns>
        public GameObject SpawnStructure(GameObject prefab, (int x, int z) tile, List<TerrainPoint> occupiedPoints, Team team = Team.NONE)
        {
            //if (!IsServer) return null;

            GameObject structureObject = Instantiate(
                prefab,
                new Vector3(
                    (tile.x + 0.5f) * Terrain.Instance.UnitsPerTileSide,
                    prefab.transform.position.y + Terrain.Instance.GetTileCenterHeight(tile),
                    (tile.z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            //structureObject.GetComponent<NetworkObject>().Spawn(true);

            // Structure properties
            Structure structure = structureObject.GetComponent<Structure>();
            structure.Team = team;
            structure.OccupiedPointHeights = occupiedPoints.ToDictionary(x => x, x => x.Y);
            structure.OccupiedTile = new TerrainPoint(tile.x, tile.z);
            StructureManager.Instance.SetOccupiedTile(tile, structure);
            GameController.Instance.OnTerrainModified += structure.ReactToTerrainChange;
            GameController.Instance.OnFlood += structure.ReactToTerrainChange;

            // SETTLEMENT properties
            if (structure.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)structure;
                settlement.SetSettlementType();
                AddSettlementPosition(new Vector2(settlement.transform.position.x, settlement.transform.position.z), team);
                //SetupSettlementClientRpc(structureObject.GetComponent<NetworkObject>().NetworkObjectId, $"{team} SETTLEMENT", LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]));
                structureObject.name = $"{team} Settlement";
                structureObject.layer = LayerData.TeamLayers[(int)team];
                GameController.Instance.OnArmageddon += settlement.DestroyIndividualSettlement;
                settlement.StartFillingSettlement();
            }

            return structureObject;
        }

        /// <summary>
        /// Sets up some <c>GameObject</c> properties for the given settlement on each client.
        /// </summary>
        /// <param name="settlementNetworkId">The <c>NetworkObjectId</c> of the settlement.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the settlement.</param>
        /// <param name="layer">An <c>int</c> representing the layer the settlement should be on.</param>
        [ClientRpc]
        private void SetupSettlementClientRpc(ulong settlementNetworkId, string name, int layer)
        {
            GameObject settlementObject = GetNetworkObject(settlementNetworkId).gameObject;
            settlementObject.name = name;
            settlementObject.layer = layer;
        }

        /// <summary>
        /// Despawns the given structure from the network and destroys is.
        /// </summary>
        /// <param name="structureObject">The <c>GameObject</c> of the structrue to be destroyed.</param>
        public void DespawnStructure(GameObject structureObject)
        {
            //if (!IsServer) return;

            Structure structure = structureObject.GetComponent<Structure>();

            if (!StructureManager.Instance.IsTileOccupied((structure.OccupiedTile.GridX, structure.OccupiedTile.GridZ)))
                return;

            if (GameController.Instance.OnTerrainModified != null)
                GameController.Instance.OnTerrainModified -= structure.ReactToTerrainChange;

            if (GameController.Instance.OnFlood != null)
                GameController.Instance.OnFlood -= structure.ReactToTerrainChange;

            StructureManager.Instance.SetOccupiedTile((structure.OccupiedTile.GridX, structure.OccupiedTile.GridZ), null);

            if (structure.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)structure;
                RemoveSettlementPosition(new Vector2(settlement.transform.position.x, settlement.transform.position.z), settlement.Team == Team.NONE ? settlement.PreviousTeam : settlement.Team);
                OnRemoveReferencesToSettlement?.Invoke(settlement);
                GameController.Instance.OnArmageddon -= settlement.DestroyIndividualSettlement;
                //GameController.Instance.RemoveVisibleObject/*ClientRpc*/(settlement.GetInstanceID()//, new ClientRpcParams
                ////{
                ////    Send = new ClientRpcSendParams
                ////    {
                ////        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(unit.Team) }
                ////    }
                ////}
                //);
                GameController.Instance.RemoveInspectedObject(settlement);
            }

            structure.Cleanup();
            //structure.GetComponent<NetworkObject>().Despawn();
            Destroy(structureObject);
        }

        #endregion


        #region Trees and Rocks

        /// <summary>
        /// Populates the terrain with trees and rocks.
        /// </summary>
        public void PlaceTreesAndRocks()
            => PlaceTreesAndRocks(m_TreePercentage, m_BlackRockPercentage, m_WhiteRockPercentage, 0, Terrain.Instance.TilesPerSide);

        public void PlaceVolcanoRocks(TerrainPoint center, int radius) 
            => PlaceTreesAndRocks(0, 0, m_VolcanoRockPercentage, center.GridX - radius, center.GridX + radius);

        private void PlaceTreesAndRocks(float treePercent, float blackRockPercent, float whiteRockPercent, int areaStart, int areaEnd)
        {
            Random random = new(!GameData.Instance ? 0 : GameData.Instance.MapSeed);

            int totalTiles = (areaEnd - areaStart) * (areaEnd - areaStart);

            int lastTreeIndex = Mathf.RoundToInt(totalTiles * treePercent);
            int lastBlackRockIndex = lastTreeIndex + Mathf.RoundToInt(totalTiles * blackRockPercent);
            int lastWhiteRockIndex = lastBlackRockIndex + Mathf.RoundToInt(totalTiles * whiteRockPercent);

            List<int> spawnIndices = Enumerable.Range(0, totalTiles).ToList();
            int count = spawnIndices.Count;

            for (int i = 0; i < totalTiles; ++i)
            {
                (int x, int z) tile = (areaStart + i % (areaEnd - areaStart), areaStart + Mathf.FloorToInt((float)i / (areaEnd - areaStart)));

                count--;
                int randomIndex = random.Next(count + 1);
                (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                if (Terrain.Instance.IsTileUnderwater(tile))
                    continue;

                if (spawnIndices[count] < lastTreeIndex)
                    SpawnStructure(m_TreePrefab, tile, Terrain.Instance.GetTileCorners(tile));

                else if (spawnIndices[count] < lastBlackRockIndex)
                    SpawnStructure(m_BlackRockPrefab, tile, Terrain.Instance.GetTileCorners(tile));

                else if (spawnIndices[count] < lastWhiteRockIndex)
                    SpawnStructure(m_WhiteRockPrefab, tile, Terrain.Instance.GetTileCorners(tile));
            }
        }

        #endregion


        #region Unit Magnets

        /// <summary>
        /// Creates the unit magnets for all the teams.
        /// </summary>
        public void SpawnUnitMagnets()
        {
            if (!IsServer) return;

            m_UnitMagnets = new UnitMagnet[m_MagnetPrefabs.Length];
            for (int i = 0; i < m_MagnetPrefabs.Length; ++i)
            {
                GameObject magnetObject = Instantiate(m_MagnetPrefabs[i], Vector3.zero, Quaternion.identity);
                magnetObject.GetComponent<NetworkObject>().Spawn(true);
                UnitMagnet magnet = magnetObject.GetComponent<UnitMagnet>();
                m_UnitMagnets[i] = magnet;

                magnet.Team = i == 0 ? Team.RED : Team.BLUE;
                magnetObject.transform.Rotate(new Vector3(1, -90, 1));
                magnetObject.transform.position = Terrain.Instance.TerrainCenter.ToWorldPosition();
                magnet.OccupiedTile = new(transform.position.x, transform.position.z, getClosestPoint: false);
                GameController.Instance.OnTerrainModified += magnet.ReactToTerrainChange;
                GameController.Instance.OnFlood += magnet.ReactToTerrainChange;
            }
        }

        /// <summary>
        /// Gets the position of the team symbol of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the symbol whose position should be returned belongs to.</param>
        /// <returns>A <c>Vector3</c> of the position of the symbol.</returns>
        public Vector3 GetMagnetPosition(Team team) => m_UnitMagnets[(int)team].transform.position;

        /// <summary>
        /// Sets the position of the symbol of the given team to the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the symbol whose position should be changed belongs to.</param>
        /// <param name="position">The position that the symbol should be set to.</param>
        public void SetMagnetPosition(Team team, Vector3 position) 
        {
            UnitMagnet symbol = m_UnitMagnets[(int)team];
            if (position == symbol.transform.position) return;

            symbol.OccupiedTile = new(position.x, position.z, getClosestPoint: false);

            symbol.SetMagnetPosition_ClientRpc/*Rpc*/(symbol.OccupiedTile.ToWorldPosition()); 
        }

        #endregion


        #region Settlements

        /// <summary>
        /// Creates a settlement of the given team on the given tile.
        /// </summary>
        /// <param name="tile">The tile that the settlement should be created on.</param>
        /// <param name="team">The team the settlement should belong to.</param>
        public void CreateSettlement(TerrainPoint tile, Team team) 
        {
            if (tile.IsLastPoint) return;
            SpawnStructure(m_SettlementPrefab, (tile.GridX, tile.GridZ), tile.TileCorners, team); 
        }

        /// <summary>
        /// Switches the team the given settlement belongs to.
        /// </summary>
        /// <param name="settlement">The <c>SETTLEMENT</c> whose team should be switched.</param>
        /// <param name="team">The new <c>Team</c> the settlement should belong to.</param>
        public void SwitchTeam(Settlement settlement, Team team)
        {
            RemoveSettlementPosition(settlement.transform.position, settlement.Team);
            //GameController.Instance.RemoveVisibleObject/*ClientRpc*/(settlement.GetInstanceID()//, new ClientRpcParams
            ////{
            ////    Send = new ClientRpcSendParams
            ////    {
            ////        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(settlement.Team) }
            ////    }
            ////}
            //);

            settlement.ChangeTeam(team);
            //SetupSettlementClientRpc(settlement.GetComponent<NetworkObject>().NetworkObjectId, $"{team} SETTLEMENT", LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]));
            settlement.gameObject.name = $"{team} Settlement";
            settlement.gameObject.layer = LayerData.TeamLayers[(int)team];

            AddSettlementPosition(settlement.transform.position, settlement.Team);
        }

        /// <summary>
        /// Burns the given settlement down.
        /// </summary>
        /// <param name="settlement">The <c>SETTLEMENT</c> that should be burned down.</param>
        public void BurnSettlement(Settlement settlement)
        {
            settlement.BurnSettlementDown();
            SwitchTeam(settlement, Team.NONE);
        }

        /// <summary>
        /// Gets settlement location at the given index in the list of settlement locations of the given team.
        /// </summary>
        /// <param name="index">The index in the settlement location list of the settlement whose location should be returned.</param>
        /// <param name="team">The <c>Team</c> the settlement that should be returned belongs to.</param>
        /// <returns>The position at the given index in the settlement position list, null if the index is out of bounds.</returns>
        public Vector3? GetSettlementLocation(int index, Team team) 
            => index >= m_SettlementLocations[(int)team].Count ? null : m_SettlementLocations[(int)team][index];

        /// <summary>
        /// Gets the number of settlements of the given team currently on the terrain.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose number of settlements should be returned.</param>
        /// <returns>The number of active settlements of the given team.</returns>
        public int GetSettlementsNumber(Team team) => m_SettlementLocations[(int)team].Count;

        /// <summary>
        /// Adds the position of a settlement the given team to the settlement locations list.
        /// </summary>
        /// <param name="position">The position of the settlement.</param>
        /// <param name="team">The <c>Team</c> whose settlement should be added.</param>
        public void AddSettlementPosition(Vector2 position, Team team) => m_SettlementLocations[(int)team].Add(position);

        /// <summary>
        /// Removes the position of a settlement the given team to the settlement locations list.
        /// </summary>
        /// <param name="position">The position of the settlement.</param>
        /// <param name="team">The <c>Team</c> whose settlement should be removed.</param>
        public void RemoveSettlementPosition(Vector2 position, Team team) => m_SettlementLocations[(int)team].Remove(position);

        #endregion


        #region Fields

        /// <summary>
        /// Creates a field belonging to the given team on the given tile.
        /// </summary>
        /// <param name="tile">The tile the field should be created on.</param>
        /// <param name="team">The <c>Team</c> the field should belong to.</param>
        /// <returns>The <c>Field</c> that was created.</returns>
        public Field SpawnField((int x, int z) tile, Team team)
        {
            //if (!IsServer) return null;
            Field field = SpawnStructure(m_FieldPrefab, tile, Terrain.Instance.GetTileCorners(tile)).GetComponent<Field>();
            field.Team = team;
            return field;
        }

        #endregion


        #region Swamp

        /// <summary>
        /// Creates a swamp on the given tile.
        /// </summary>
        /// <param name="tile">The tile the swamp should be created on.</param>
        public void SpawnSwamp((int x, int z) tile) => SpawnStructure(m_SwampPrefab, tile, Terrain.Instance.GetTileCorners(tile));

        #endregion


        /// <summary>
        /// Checks whether the given team has a leader that is in a settlement.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team as a leader that is in a settlement, false otherwise.</returns>
        public bool HasSettlementLeader(Team team) => m_LeaderSettlements[(int)team];

        /// <summary>
        /// Gets the <c>Settlement</c> the team leader is part of, if such a settlement exists.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> of the team's leader, null if the leader is not part of a settlement..</returns>
        public Settlement GetLeaderSettlement(Team team) => m_LeaderSettlements[(int)team];

        public void SetLeaderSettlement(Team team, Settlement settlement)
        {
            UnsetLeaderSettlement(team);

            m_LeaderSettlements[(int)team] = settlement;
            settlement.SetLeader(true);
            UnitManager.Instance.OnNewLeaderGained?.Invoke();
        }

        public void UnsetLeaderSettlement(Team team)
        {
            if (!HasSettlementLeader(team)) return;

            m_LeaderSettlements[(int)team].SetLeader(false);
            m_LeaderSettlements[(int)team] = null;
        }



    }
}