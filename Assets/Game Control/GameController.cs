using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
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
        [SerializeField] private int m_VolcanoRadius = 3;
        [SerializeField, Range(0, 1)] private float m_VolcanoRockDensity = 0.4f;

        private static GameController m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static GameController Instance { get => m_Instance; }

        public bool IsPlayerHosting { get => IsHost; }

        public string[] TeamLayers = new string[] { "Red Team", "Blue Team", "None Team" };

        public int MaxManna { get => m_MaxManna; }
        public int[] PowerActivationThreshold { get => m_PowerActivationThreshold; }
        public int[] PowerMannaCost { get => m_PowerMannaCost; }
        public int EarthquakeRadius { get => m_EarthquakeRadius; }
        public int SwampRadius { get => m_SwampRadius; }
        public int VolcanoRadius { get => m_VolcanoRadius; }

        public Action OnTerrainMoved;
        public Action OnFlood;
        public Action OnRedFlagMoved;
        public Action OnBlueFlagMoved;

        public Team Winner;  // TODO: Remove - for testing only

        private int m_BattlesIndex;
        private int[] m_KnightsIndex = new int[2];
        private int[] m_SettlementsIndex = new int[2];



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            Terrain.Instance.CreateTerrain();
            //StructureManager.Instance.PlaceTreesAndRocks();
            UnitManager.Instance.SpawnStarterUnits();
            StructureManager.Instance.SpawnFlags();
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
            Terrain.Instance.ModifyTerrain(point, lower);

            //MoldTerrainServerRpc(point, lower);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoldTerrainServerRpc(MapPoint point, bool lower)
            => MoldTerrainClientRpc(point, lower);

        [ClientRpc]
        private void MoldTerrainClientRpc(MapPoint point, bool lower)
            => Terrain.Instance.ModifyTerrain(point, lower);

        #endregion


        #region Guide Followers

        //[ServerRpc(RequireOwnership = false)]
        public void MoveFlag/*ServerRpc*/(MapPoint point, Team team)
        {
            if (UnitManager.Instance.GetLeader(team) == null)
                return;

            StructureManager.Instance.SetFlagPosition/*ClientRpc*/(team, new Vector3(
                point.TileX * Terrain.Instance.UnitsPerTileSide,
                point.Y,
                point.TileZ * Terrain.Instance.UnitsPerTileSide
            ));

            if (team == Team.RED)
                OnRedFlagMoved?.Invoke();
            else if (team == Team.BLUE)
                OnBlueFlagMoved?.Invoke();
        }


        #endregion


        #region Earthquake

        /// <summary>
        /// Executes the Earthquake power on server.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void EarthquakeServerRpc(MapPoint point)
            => EarthquakeClientRpc(point, new Random().Next());

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
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    (int x, int z) neighborTile = (tile.TileX + x, tile.TileZ + z);
                    if (tile.TileX + x < 0 || tile.TileX + x >= Terrain.Instance.TilesPerSide ||
                        tile.TileZ + z < 0 || tile.TileZ + z >= Terrain.Instance.TilesPerSide ||
                        !Terrain.Instance.IsTileFlat(neighborTile))
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOccupyingTile(neighborTile);

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Rock) || structureType == typeof(Tree) ||
                            structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;

                        if (structureType == typeof(Field))
                        {
                            ((Field)structure).OnFieldDestroyed?.Invoke();
                            StructureManager.Instance.DespawnStructure(structure.gameObject);
                        }
                    }

                    flatTiles.Add(neighborTile);
                }
            }

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
                    StructureManager.Instance.SpawnSwamp(flatTile, Terrain.Instance.GetTilePoints(flatTile));
            }
        }

        #endregion


        #region Knight

        public void CreateKnight(Team team)
        {
            if (UnitManager.Instance.GetLeader(team) == null)
                return;

            UnitManager.Instance.CreateKnight(team);
            StructureManager.Instance.SetFlagPosition(team, UnitManager.Instance.GetNewestKnight(team).ClosestMapPoint.ToWorldPosition());
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
            StructureManager.Instance.PlaceTreesAndRocks(0, m_VolcanoRockDensity, 0);
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
                OnFlood?.Invoke();
        }

        #endregion

        #endregion



        #region Zoom

        [ServerRpc(RequireOwnership = false)]
        public void ShowLeaderServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            Unit leader = UnitManager.Instance.GetLeader(team);

            if (leader == null)
            {
                ShowFlagServerRpc(team);
                return;
            }

            Vector3 leaderPosition = leader.transform.position;
            CameraController.Instance.LookAtClientRpc(
                new Vector3(leaderPosition.x, 0, leaderPosition.z),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        [ServerRpc(RequireOwnership = false)]
        public void ShowFlagServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            Vector3 flagPosition = StructureManager.Instance.GetFlagPosition(team);

            CameraController.Instance.LookAtClientRpc(
                new Vector3(flagPosition.x, 0, flagPosition.z),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        [ServerRpc(RequireOwnership = false)]
        public void ShowKnightsServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Unit knight = UnitManager.Instance.GetKnight(m_KnightsIndex[teamIndex], team);
            if (knight == null) return;

            Vector3 knightPosition = knight.transform.position;

            CameraController.Instance.LookAtClientRpc(
                new Vector3(knightPosition.x, 0, knightPosition.z),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_KnightsIndex[teamIndex] = (m_KnightsIndex[teamIndex] + 1) % UnitManager.Instance.GetKnightsNumber(team);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ShowSettlementsServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            (int x, int z) tile = StructureManager.Instance.GetSettlementTile(m_SettlementsIndex[teamIndex], team);

            CameraController.Instance.LookAtClientRpc(
                new Vector3((tile.x + 0.5f) * Terrain.Instance.UnitsPerTileSide, 0, (tile.z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_SettlementsIndex[teamIndex] = (m_SettlementsIndex[teamIndex] + 1) % StructureManager.Instance.GetSettlementsNumber(team);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ShowBattlesServerRpc(ServerRpcParams serverRpcParams = default)
        {
            Vector2 battleLocation = UnitManager.Instance.GetBattlePosition(m_BattlesIndex);

            CameraController.Instance.LookAtClientRpc(
                new Vector3(battleLocation.x, 0, battleLocation.y),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_BattlesIndex = (m_BattlesIndex + 1) % UnitManager.Instance.GetBattlesNumber();
        }

        #endregion
    }
}