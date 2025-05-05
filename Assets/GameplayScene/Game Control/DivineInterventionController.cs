using System.Collections.Generic;
using System.Linq;
using System;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The Divine Interventions available in the game.
    /// </summary>
    public enum DivineIntervention
    {
        /// <summary>
        /// The "Mold Terrain" Divine Intervention.
        /// </summary>
        MOLD_TERRAIN,
        /// <summary>
        /// The "Place Unit Magnet" Divine Intervention.
        /// </summary>
        PLACE_MAGNET,
        /// <summary>
        /// The "Earthquake" Divine Intervention.
        /// </summary>
        EARTHQUAKE,
        /// <summary>
        /// The "Swamp" Divine Intervention.
        /// </summary>
        SWAMP,
        /// <summary>
        /// The "Knight" Divine Intervention.
        /// </summary>
        KNIGHT,
        /// <summary>
        /// The "Volcano" Divine Intervention.
        /// </summary>
        VOLCANO,
        /// <summary>
        /// The "Flood" Divine Intervention.
        /// </summary>
        FLOOD,
        /// <summary>
        /// The "Armageddon" Divine Intervention.
        /// </summary>
        ARMAGEDDON
    }


    /// <summary>
    /// The <c>DivineInterventionController</c> manages the execution of the Divine Interventions.
    /// </summary>
    public class DivineInterventionController : NetworkBehaviour
    {
        #region Inspector Fields

        [Header("Manna")]
        [Tooltip("The maximum amount of manna a player can have.")]
        [SerializeField] private int m_MaxManna = 64;
        [Tooltip("The amount of manna that is required to be full in order to have access to a Divine Intervention." +
            "The value for a Divine Intervention is at the index equal to the value of the Divine Intervention in the DivineIntervention enum.")]
        [SerializeField] private int[] m_ActivationThreshold = new int[Enum.GetNames(typeof(DivineIntervention)).Length];
        [Tooltip("The amount of manna spent for using a Divine Intervention." +
            "The value for a Divine Intervention is at the index equal to the value of the Divine Intervention in the DivineIntervention enum.")]
        [SerializeField] private int[] m_MannaCost = new int[Enum.GetNames(typeof(DivineIntervention)).Length];

        [Header("Divine Interventions")]
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Earthquake.")]
        [SerializeField] private int m_EarthquakeRadius = 8;
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Swamp.")]
        [SerializeField] private int m_SwampRadius = 8;
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Volcano.")]
        [SerializeField] private int m_VolcanoRadius = 8;

        #endregion


        #region Class Fields

        private static DivineInterventionController m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static DivineInterventionController Instance { get => m_Instance; }

        /// <summary>
        /// Gets the maximum amount of manna a faction can have.
        /// </summary>
        public int MaxManna { get => m_MaxManna; }
        /// <summary>
        /// An array of the amount of manna each faction has.
        /// </summary>
        /// <remarks>The manna at each index is the manna of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly float[] m_Manna = new float[2];


        private bool m_IsArmageddon;
        /// <summary>
        /// True if the Armageddon Divine Intervention has been activated, false otherwise.
        /// </summary>
        public bool IsArmageddon { get => m_IsArmageddon; }


        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Earthquake.
        /// </summary>
        public int EarthquakeRadius { get => m_EarthquakeRadius; }
        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Swamp.
        /// </summary>
        public int SwampRadius { get => m_SwampRadius; }
        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Volcano.
        /// </summary>
        public int VolcanoRadius { get => m_VolcanoRadius; }

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the heights of the terrain have been modified.
        /// </summary>
        public Action<TerrainPoint, TerrainPoint> OnTerrainModified;
        /// <summary>
        /// Action to be called when the Flood Divine Intervention is used.
        /// </summary>
        public Action OnFlood;
        /// <summary>
        /// Action to be called when the Armageddon Divine Intervention is used.
        /// </summary>
        public Action OnArmageddon;

        #endregion



        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start()
        {
            if (!IsHost) return;

            // set manna to include divine interventions up to an including knight
            SetManna(Faction.RED, m_ActivationThreshold[(int)DivineIntervention.KNIGHT]);
            SetManna(Faction.BLUE, m_ActivationThreshold[(int)DivineIntervention.KNIGHT]);
        }


        #region Manna

        /// <summary>
        /// Adds manna to the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> manna should be added to.</param>
        /// <param name="amount">The amount of manna to be added.</param>
        public void AddManna(Faction faction, float amount) => SetManna(faction, Mathf.Clamp(m_Manna[(int)faction] + amount, 0, m_MaxManna));

        /// <summary>
        /// Removes manna from the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> manna should be removed from.</param>
        /// <param name="amount">The amount of manna to be removed.</param>
        public void RemoveManna(Faction faction, float amount) => SetManna(faction, Mathf.Clamp(m_Manna[(int)faction] - amount, 0, m_MaxManna));

        /// <summary>
        /// Sets the manna of the given faction to the given amount.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose manna should be set.</param>
        /// <param name="amount">The amount of manna the given faction should have.</param>
        private void SetManna(Faction faction, float amount)
        {
            if (amount == m_Manna[(int)faction]) return;

            m_Manna[(int)faction] = amount;

            int activeInterventions = -1;
            foreach (float threshold in m_ActivationThreshold)
            {
                if (amount < threshold) break;
                activeInterventions++;
            }

            UpdateMannaUI_ClientRpc(
                amount,
                activeInterventions,
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            );
        }

        /// <summary>
        /// Triggers the update of a player's UI with manna and Divine Intervention information.
        /// </summary>
        /// <param name="manna">The amount of manna the player has.</param>
        /// <param name="activeInterventions">The number of active Divine Interventions the player has.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateMannaUI_ClientRpc(float manna, int activeInterventions, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateMannaBar(manna, activeInterventions);

        /// <summary>
        /// Checks whether the player from the given faction can use the given Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the player controls.</param>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> the player wants to activate.</param>
        [ServerRpc(RequireOwnership = false)]
        public void TryActivateDivineIntervention_ServerRpc(Faction faction, DivineIntervention divineIntervention, ServerRpcParams serverParams = default)
        {
            bool activated = true;

            if (m_Manna[(int)faction] < m_ActivationThreshold[(int)divineIntervention] ||
                (divineIntervention == DivineIntervention.KNIGHT && !GameController.Instance.HasLeader(faction)) ||
                (divineIntervention == DivineIntervention.FLOOD && Terrain.Instance.HasReachedMaxWaterLevel()))
                activated = false;

            if (activated)
            {
                if (divineIntervention == DivineIntervention.KNIGHT)
                    CreateKnight(faction);

                if (divineIntervention == DivineIntervention.FLOOD)
                    CauseFlood(faction);

                if (divineIntervention == DivineIntervention.ARMAGEDDON)
                    StartArmageddon(faction);
            }

            SendInterventionActivationInfo_ClientRpc(
                divineIntervention,
                activated,
                GameUtils.GetClientParams(serverParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Notifies the player whether the Divine Intervention they want to activate is activated or not.
        /// </summary>
        /// <param name="divineIntervention">The <c>DivineIntervention</c> the player wants to activate.</param>
        /// <param name="isActivated">True if the Divine Intervention is activated, false otherwise.</param>
        /// <param name="clientParams">RPC info for the client RPC.</param>
        [ClientRpc]
        private void SendInterventionActivationInfo_ClientRpc(DivineIntervention divineIntervention, bool isActivated, ClientRpcParams clientParams = default)
            => PlayerController.Instance.ReceiveInterventionActivationInfo(divineIntervention, isActivated);

        #endregion


        #region Mold Terrain

        /// <summary>
        /// Triggers the execution of the Mold Terrain Divine Intervention.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> that should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoldTerrain_ServerRpc(TerrainPoint point, bool lower) => MoldTerrain_ClientRpc(point, lower);

        /// <summary>
        /// Executes the Mold Terrain Divine Intervention on the client.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> that should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ClientRpc]
        private void MoldTerrain_ClientRpc(TerrainPoint point, bool lower) => RespondToTerrainChange(Terrain.Instance.ModifyTerrain(point, lower));

        #endregion


        #region Place Unit Magnet

        /// <summary>
        /// Executes the Place Unit Magnet Divine Intervention.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> on which the magnet should be set.</param>
        /// <param name="faction">The <c>Faction</c> whose magnet should be moved.</param>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceUnitMagnet_ServerRpc(Faction faction, TerrainPoint point)
        {
            // the magnet can only be moved if there is a leader
            if (!GameController.Instance.HasLeader(faction)) return;

            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.PLACE_MAGNET]);

            GameController.Instance.PlaceUnitMagnetAtPoint(faction, point);
        }

        #endregion


        #region Earthquake

        /// <summary>
        /// Triggers the execution of the Earthquake Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateEarthquake_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.EARTHQUAKE]);
            CreateEarthquake_ClientRpc(center, new Random().Next());
        }

        /// <summary>
        /// Executes the Earthquake Divine Intervention on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        /// <param name="randomizerSeed">The seed used for the randomizer that sets the heights in the earthquake area.</param>
        [ClientRpc]
        private void CreateEarthquake_ClientRpc(TerrainPoint center, int randomizerSeed)
            => RespondToTerrainChange(Terrain.Instance.CauseEarthquake(center, m_EarthquakeRadius, randomizerSeed));

        #endregion


        #region Swamp

        /// <summary>
        /// Executes the Swamp Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the swamp.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateSwamp_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.SWAMP]);

            // gets all the flat tiles in the swamp area
            List<TerrainTile> flatTiles = new();
            for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    TerrainTile neighborTile = new(center.X + x, center.Z + z);

                    if (!neighborTile.IsInBounds() || !neighborTile.IsFlat())
                        continue;

                    Structure structure = StructureManager.Instance.GetStructureOnTile(neighborTile);

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Rock) || structureType == typeof(Tree) ||
                            structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;
                    }

                    flatTiles.Add(neighborTile);
                }
            }

            // randomly places swamps in the area, using shuffle algorithm
            Random random = new();
            List<int> tiles = Enumerable.Range(0, flatTiles.Count).ToList();
            int swampTiles = random.Next(Mathf.RoundToInt(flatTiles.Count * 0.5f), flatTiles.Count);

            HashSet<Settlement> affectedSettlements = new();
            int count = tiles.Count;
            foreach (TerrainTile flatTile in flatTiles)
            {
                count--;
                int randomIndex = random.Next(count + 1);
                (tiles[count], tiles[randomIndex]) = (tiles[randomIndex], tiles[count]);

                if (tiles[count] <= swampTiles)
                {
                    Structure structure = StructureManager.Instance.GetStructureOnTile(flatTile);

                    if (structure && structure.GetType() == typeof(Field))
                    {
                        Field field = (Field)structure;
                        // gets settlements whose fields were destroyed
                        affectedSettlements.UnionWith(field.SettlementsServed);
                        StructureManager.Instance.DespawnStructure(field);
                    }
                    else if (structure) continue;

                    StructureManager.Instance.SpawnSwamp(flatTile);
                }
            }

            // updates settlements whose fields were destroyed
            foreach (Settlement settlement in affectedSettlements)
                settlement.UpdateType();
        }

        #endregion


        #region Knight

        /// <summary>
        /// Executes the Knight Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that should gain a knight.</param>
        public void CreateKnight(Faction faction)
        {
            // the faction has to have a leader to turn into a knight.
            if (!GameController.Instance.HasLeader(faction)) return;

            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.KNIGHT]);

            Unit knight = null;

            // if the leader is in a unit, just turn that unit into a knight
            if (GameController.Instance.HasLeaderUnit(faction))
            {
                knight = GameController.Instance.GetLeaderUnit(faction);
                GameController.Instance.SetLeader(faction, null);
                UnitManager.Instance.SetKnight(faction, knight);
            }

            // if the leader is in a origin, destroy that origin and spawnPoint a knight in its position
            if (GameController.Instance.HasLeaderSettlement(faction))
            {
                Settlement settlement = GameController.Instance.GetLeaderSettlement(faction);
                GameController.Instance.SetLeader(faction, null);

                knight = UnitManager.Instance.SpawnUnit(
                    location: new(settlement.OccupiedTile.X, settlement.OccupiedTile.Z),
                    faction,
                    type: UnitType.KNIGHT,
                    strength: settlement.FollowersInSettlement,
                    origin: settlement
                );

                StructureManager.Instance.DestroySettlement(settlement, updateNearbySettlements: true);
            }

            GameController.Instance.PlaceUnitMagnetAtPoint(faction, knight.ClosestTerrainPoint);

            // show the knight
            GameController.Instance.SetCameraLookPosition_ClientRpc(
                new(knight.transform.position.x, 0, knight.transform.position.z),
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            );
        }

        #endregion


        #region Volcano

        /// <summary>
        /// Triggers the execution of the Volcano Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateVolcano_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.VOLCANO]);

            CreateVolcano_ClientRpc(center);
            StructureManager.Instance.PlaceVolcanoRocks(center, m_VolcanoRadius);
        }

        /// <summary>
        /// Executes the Volcano Divine Intervention on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ClientRpc]
        private void CreateVolcano_ClientRpc(TerrainPoint center)
            => RespondToTerrainChange(Terrain.Instance.CauseVolcano(center, m_VolcanoRadius));

        #endregion


        #region Flood

        /// <summary>
        /// Triggers the execution of the Flood Divine Intervention on the clients.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        public void CauseFlood(Faction faction)
        {
            if (Terrain.Instance.HasReachedMaxWaterLevel()) return;

            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.FLOOD]);

            CauseFlood_ClientRpc();
            OnFlood?.Invoke();
        }

        /// <summary>
        /// Executes the Flood Divine Intervention on the client.
        /// </summary>
        [ClientRpc]
        private void CauseFlood_ClientRpc()
        {
            Terrain.Instance.RaiseWaterLevel();
            Water.Instance.Raise();
            BorderWalls.Instance.UpdateAllWalls();
            MinimapTextureGenerator.Instance.SetTexture();

            PlayerCamera.Instance.RaiseCameraToWaterLevel();
        }

        #endregion


        #region Armageddon

        /// <summary>
        /// Executes the Armageddon Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        public void StartArmageddon(Faction faction)
        {
            RemoveManna(faction, m_MannaCost[(int)DivineIntervention.ARMAGEDDON]);

            m_IsArmageddon = true;
            SetupArmageddon_ClientRpc();

            foreach (Faction factions in Enum.GetValues(typeof(Faction)))
            {
                if (factions == Faction.NONE) continue;

                GameController.Instance.PlaceUnitMagnetAtPoint(factions, Terrain.Instance.TerrainCenter);
                UnitManager.Instance.ChangeUnitBehavior_ServerRpc(factions, UnitBehavior.GO_TO_MAGNET);
            }

            // destroy all settlements
            OnArmageddon?.Invoke();
        }

        /// <summary>
        /// Sets the state of Armageddon to active on the client.
        /// </summary>
        [ClientRpc]
        private void SetupArmageddon_ClientRpc() => PlayerController.Instance.ActivateArmageddon();

        #endregion


        /// <summary>
        /// Updates the terrain accessories, the structures, the units, and the unit magnets after a terrain modification.
        /// </summary>
        /// <param name="modifiedAreaCorners">A tuple of the <c>TerrainPoint</c> at the bottom left and the <c>TerrainPoint</c> on the top right
        /// of a rectangular area containing all the points whose heights were changed in the terrain modification.</param>
        public void RespondToTerrainChange((TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaCorners)
        {
            BorderWalls.Instance.UpdateWallsInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            MinimapTextureGenerator.Instance.UpdateTextureInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);

            if (!IsHost) return;

            GameController.Instance.UpdateMagnetsInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            StructureManager.Instance.UpdateStructuresInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            OnTerrainModified?.Invoke(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight); // for units
        }
    }
}