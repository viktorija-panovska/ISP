using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    /// <summary>
    /// The <c>Settlement</c> class is a <c>Structure</c> that represents a settlement built by one of the teams on the terrain.
    /// </summary>
    public class Settlement : Structure, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BoxCollider m_SettlementCollision;
        [SerializeField] private BoxCollider m_SettlementTrigger;

        [Header("Settlements")]
        [SerializeField] private GameObject[] m_SettlementObjects;
        [SerializeField] private SettlementData[] m_SettlementData;
        [SerializeField] private GameObject m_RuinedSettlement;

        [Header("Flag")]
        [SerializeField] private GameObject[] m_Flags;
        [SerializeField] private GameObject[] m_TeamSymbols;

        /// <summary>
        /// Gets and sets the team that the settlement belongs to.
        /// </summary>
        public override Team Team 
        { 
            get => m_Team;
            set
            {
                m_Flags[(int)m_Team].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);

                m_Team = value;

                if (m_Team != Team.NONE)
                    m_Flags[(int)m_Team].SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
            }
        }

        /// <summary>
        /// The index of the type of settlement currently active.
        /// </summary>
        private int m_CurrentSettlementIndex;
        /// <summary>
        /// The data of the type of settlement currently active.
        /// </summary>
        private SettlementData m_CurrentSettlementData;

        private int m_FollowersInSettlement;
        /// <summary>
        /// Gets the number of followers currently inside the settlement.
        /// </summary>
        public int FollowersInSettlement { get => m_FollowersInSettlement; }

        /// <summary>
        /// True if the settlement contains the leader of the team, false otherwise.
        /// </summary>
        private bool m_ContainsLeader;

        /// <summary>
        /// The strength of a unit released from this settlement.
        /// </summary>
        public int UnitStrength { get => m_CurrentSettlementData.UnitStrength; }

        /// <summary>
        /// True if the settlement cannot contain any more followers, false otherwise.
        /// </summary>
        public bool IsSettlementFull { get => m_FollowersInSettlement == m_CurrentSettlementData.Capacity; }

        private bool m_IsAttacked;
        /// <summary>
        /// True if the settlement is under attack by a unit from the enemy team, false otherwise.
        /// </summary>
        public bool IsAttacked { get => m_IsAttacked; set => m_IsAttacked = value; }

        private bool m_IsBurnedDown;
        /// <summary>
        /// True if the settlement has been burned down, false otherwise.
        /// </summary>
        public bool IsBurnedDown { get => m_IsBurnedDown; }

        private bool m_ShouldTryUpdateType = true;
        /// <summary>
        /// True if the settlement should check if there are any new m_Fields or missing ones, false otherwise.
        /// </summary>
        public bool ShouldTryUpdateType { set => m_ShouldTryUpdateType = value; }

        private bool m_IsDestroyed = false;
        private int m_Fields;

        /// <summary>
        /// Action to be called when the settlement is destroyed.
        /// </summary>
        private Action<Settlement> OnSettlementDestroyed;
        /// <summary>
        /// Action to be called when the settlement is burned.
        /// </summary>
        private Action OnSettlementBurned;
        /// <summary>
        /// Action to be called when the settlement's team has been changed.
        /// </summary>
        private Action<Team> OnSettlementTeamChanged;


        private void Start()
        {
            m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;

            Vector3 startingScale = m_SettlementObjects[0].transform.localScale;

            for (int i = 0; i < m_SettlementData.Length; ++i)
                GameUtils.ResizeGameObject(
                    m_SettlementObjects[i],
                    m_SettlementData[i].Type == SettlementType.CITY ? 3 * Terrain.Instance.UnitsPerTileSide : Terrain.Instance.UnitsPerTileSide, 
                    scaleY: true
                );

            GameUtils.ResizeGameObject(m_RuinedSettlement, Terrain.Instance.UnitsPerTileSide, scaleY: true);

            foreach (GameObject flag in m_Flags)
            {
                flag.transform.localScale = new Vector3(
                    flag.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    flag.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    flag.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
                flag.transform.localPosition = new Vector3(-Terrain.Instance.UnitsPerTileSide / 2, 0, -Terrain.Instance.UnitsPerTileSide / 2);
            }

            foreach (GameObject sign in m_TeamSymbols)
            {
                sign.transform.localScale = new Vector3(
                    sign.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    sign.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    sign.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
                sign.transform.localPosition = new Vector3(-Terrain.Instance.UnitsPerTileSide / 2, 0, Terrain.Instance.UnitsPerTileSide / 2);
            }

            //StartCoroutine(FillSettlement());
        }


        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();
            if (!unit) return;

            if (unit.Team == m_Team && GameController.Instance.GetLeaderSettlement(unit.Team) == this)
                GameController.Instance.SetLeader(unit.gameObject, m_Team);
            else if (unit.Team == m_Team && !IsSettlementFull && unit.CanEnterSettlement && unit.Behavior != UnitBehavior.GO_TO_SYMBOL)
                TakeFollowersFromUnit(unit);

            if (unit.Team != m_Team && !IsAttacked && unit.Behavior != UnitBehavior.GO_TO_SYMBOL)
                UnitManager.Instance.AttackSettlement(unit, this);
        }


        #region Structure

        /// <inheritdoc />
        public override void ReactToTerrainChange()
        {
            if (ShouldDestroyStructure())
            {
                m_IsDestroyed = true;
                OnSettlementDestroyed?.Invoke(this);
                ReleaseUnit(m_CurrentSettlementData.UnitStrength);
                StructureManager.Instance.DespawnStructure(gameObject);
                return;
            }

            if (m_DestroyMethod == DestroyMethod.DROWN)
            {
                SetHeightClientRpc(
                    GetType() == typeof(TeamSymbol)
                    ? Terrain.Instance.GetPointHeight(m_OccupiedTile)
                    : Terrain.Instance.GetTileCenterHeight(m_OccupiedTile)
                );
            }
        }

        /// <inheritdoc />
        public override void Cleanup() => OnSettlementDestroyed = null;

        #endregion


        #region Settlement Type

        /// <summary>
        /// Sets the type of settlement currently active based on the number of m_Fields available around it.
        /// </summary>
        public void SetType()
        {
            if (/*!IsHost || */!m_ShouldTryUpdateType) return;
            m_ShouldTryUpdateType = false;

            m_Fields = CreateFields();
            // formula for getting the index of the settlement from the number of m_Fields
            int settlementIndex = Mathf.Clamp(Mathf.CeilToInt((m_Fields + 1) / 2f), 0, m_SettlementData.Length);
            SettlementData newSettlement = m_SettlementData[settlementIndex];

            if (m_CurrentSettlementData && m_CurrentSettlementData.Type == newSettlement.Type)
                return;

            // remove stuff from old settlement
            if (m_CurrentSettlementData)
            {
                m_SettlementObjects[m_CurrentSettlementIndex].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
                if (m_CurrentSettlementData.Type == SettlementType.CITY)
                    RemoveCityFields();
            }

            m_CurrentSettlementIndex = settlementIndex;
            m_CurrentSettlementData = newSettlement;

            Vector3 colliderSize = new(Terrain.Instance.UnitsPerTileSide, 1, Terrain.Instance.UnitsPerTileSide);
            if (m_CurrentSettlementData.Type == SettlementType.CITY)
            {
                AddCityFields();
                colliderSize = new(3 * Terrain.Instance.UnitsPerTileSide, 1, 3 * Terrain.Instance.UnitsPerTileSide);
            }

            m_SettlementObjects[m_CurrentSettlementIndex].SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(true);

            if (m_FollowersInSettlement > m_CurrentSettlementData.Capacity)
                ReleaseUnit(m_FollowersInSettlement - m_CurrentSettlementData.Capacity);

            // change the size of the collider
            SetCollider/*ClientRpc*/(colliderSize);
        }

        /// <summary>
        /// Sets the size of the collider of the settlement.
        /// </summary>
        /// <param name="size">A <c>Vector3</c> of the new size of the collider.</param>
        /// <param name="clientRpcParams">RPC data for the client RPC.</param>
        //[ClientRpc]
        private void SetCollider/*ClientRpc*/(Vector3 size, ClientRpcParams clientRpcParams = default) 
        {
            m_SettlementCollision.size = size;
            m_SettlementTrigger.size = size;
        }

        /// <summary>
        /// Sets this settlement to the burned down state.
        /// </summary>
        public void BurnSettlementDown()
        {
            m_IsBurnedDown = true;
            Team = Team.NONE;
            m_DestroyMethod = DestroyMethod.DROWN;

            if (m_CurrentSettlementData)
            {
                m_SettlementObjects[m_CurrentSettlementIndex].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
                m_CurrentSettlementData = null;
                m_CurrentSettlementIndex = -1;
            }

            m_RuinedSettlement.SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(true);

            //SetColliderClientRpc(Vector3.zero);
            GetComponent<BoxCollider>().size = Vector3.zero;

            OnSettlementBurned?.Invoke();
        }

        #endregion


        #region Change Team

        /// <summary>
        /// Handles the 
        /// </summary>
        /// <param name="newTeam">The new <c>Team</c> the settlement belongs to.</param>
        public void ChangeTeam(Team newTeam) 
        {
            if (newTeam == m_Team) return;

            Team = newTeam;
            RemoveFollowers(m_FollowersInSettlement - 1);
            OnSettlementTeamChanged?.Invoke(m_Team);

            UpdateSharedSettlements();
        }

        /// <summary>
        /// Updates the types of the settlements that are close enough to 
        /// the current settlement that they could be sharing fields with it.
        /// </summary>
        private void UpdateSharedSettlements()
        {
            for (int z = -4; z <= 4; ++z)
            {
                for (int x = -4; x <= 4; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    (int x, int z) tile = (OccupiedTile.x + x, OccupiedTile.z + z);

                    if (tile.x < 0 || tile.x >= Terrain.Instance.TilesPerSide || tile.z < 0 || tile.z >= Terrain.Instance.TilesPerSide)
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOnTile(tile);
                    if (!structure || structure.GetType() != typeof(Settlement)) continue;

                    Settlement settlement = (Settlement)structure;
                    settlement.ShouldTryUpdateType = true;
                    settlement.SetType();
                }
            }
        }

        #endregion


        #region Fields

        /// <summary>
        /// Creates m_Fields in the flat spaces in a 5x5 square around the settlement.
        /// </summary>
        /// <returns>The number of m_Fields created around the settlement.</returns>
        private int CreateFields()
        {
            int fields = 0;

            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    (int x, int z) neighborTile = (m_OccupiedTile.x + x, m_OccupiedTile.z + z);

                    if ((x, z) == (0, 0) || (x != 0 && z != 0 && Mathf.Abs(x) != Mathf.Abs(z)) ||
                        neighborTile.x < 0 || neighborTile.x >= Terrain.Instance.TilesPerSide ||
                        neighborTile.z < 0 || neighborTile.z >= Terrain.Instance.TilesPerSide ||
                        !Terrain.Instance.IsTileFlat(neighborTile))
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);
                    Field field = null;

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;

                        if (structureType == typeof(Field) && ((Field)structure).Team != m_Team)
                        {
                            Field enemyField = (Field)structure;
                            if (enemyField.IsServingSettlement(this))
                            {
                                enemyField.RemoveSettlementServed(this);
                                RemoveField(enemyField);
                            }
                            
                            continue;
                        }

                        if (structureType == typeof(Rock))
                        {
                            fields--;
                            continue;
                        }

                        if (structureType == typeof(Tree))
                            StructureManager.Instance.DespawnStructure(structure.gameObject);

                        if (structureType == typeof(Field))
                            field = (Field)structure;
                    }

                    fields++;

                    SetupField(field, neighborTile);
                }
            }

            return fields;
        }

        /// <summary>
        /// Removes the associations with the given field from the settlement.
        /// </summary>
        /// <param name="field">The <c>Field</c> that should be removed.</param>
        public void RemoveField(Field field) 
        {
            OnSettlementDestroyed -= field.RemoveSettlementServed;
            OnSettlementBurned -= field.BurnField;
            OnSettlementTeamChanged -= field.OnTeamChanged;
        }

        /// <summary>
        /// Spawns field (if one doesn't exist already) and sets up the field's properties to serve this settlement.
        /// </summary>
        /// <param name="field">The field whose properties need to be modified, null if a new field should be spawned.</param>
        /// <param name="tile">The (x, z) coordinates of the tile on the terrain grid where the field should be created.</param>
        private void SetupField(Field field, (int x, int z) tile)
        {
            if (!field)
                field = StructureManager.Instance.SpawnField(tile, m_Team);

            if (!field.IsServingSettlement(this))
            {
                field.AddSettlementServed(this);
                OnSettlementDestroyed += field.RemoveSettlementServed;
                OnSettlementBurned += field.BurnField;
                OnSettlementTeamChanged += field.OnTeamChanged;
            }
        }

        /// <summary>
        /// Adds extra m_Fields to fill in the 5x5 square around the city.
        /// </summary>
        private void AddCityFields()
        {
            // fill in the blank spaces between the parallels and diagonals with m_Fields
            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    if (x == 0 || z == 0 || Mathf.Abs(x) == Mathf.Abs(z)) continue;

                    (int x, int z) neighborTile = (m_OccupiedTile.x + x, m_OccupiedTile.z + z);
                    Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);
                    Field field = null;
                    if (structure && structure.GetType() == typeof(Field))
                        field = (Field)structure;
                    SetupField(field, neighborTile);
                }
            }
        }

        /// <summary>
        /// Removes the extra m_Fields added to fill in the 5x5 square around the city.
        /// </summary>
        private void RemoveCityFields()
        {
            // remove m_Fields on the diagonals and parallels
            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    if (x == 0 || z == 0 || Mathf.Abs(x) == Mathf.Abs(z))
                        continue;

                    (int x, int z) neighborTile = (m_OccupiedTile.x + x, m_OccupiedTile.z + z);
                    Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);

                    if (!structure || structure.GetType() != typeof(Field))
                        continue;

                    ((Field)structure).RemoveSettlementServed(this);
                }
            }
        }

        #endregion


        #region Followers

        /// <summary>
        /// Starts the coroutine that periodically adds followers to the settlement.
        /// </summary>
        public void StartFillingSettlement() => StartCoroutine(FillSettlement());

        /// <summary>
        /// Adds the given amount of followers to the settlement.
        /// </summary>
        /// <param name="amount">The amount of followers to be added.</param>
        private void AddFollowers(int amount) => SetFollowerCount(m_FollowersInSettlement + amount);

        /// <summary>
        /// Removes the given amount of followers from the settlement.
        /// </summary>
        /// <param name="amount">The amount of followers to be removed.</param>
        private void RemoveFollowers(int amount) => SetFollowerCount(m_FollowersInSettlement - amount);

        /// <summary>
        /// Sets the amount of followers in the settlement to the given amount.
        /// </summary>
        /// <param name="amount">The amount of followers in the settlement.</param>
        private void SetFollowerCount(int amount)
        {
            m_FollowersInSettlement = Mathf.Clamp(amount, 0, m_CurrentSettlementData.Capacity);
            UpdateUnitUIClient(m_CurrentSettlementIndex, m_CurrentSettlementData.Capacity, m_FollowersInSettlement);

            if (m_FollowersInSettlement == 0)
                StructureManager.Instance.DespawnStructure(gameObject);

            if (m_FollowersInSettlement >= m_CurrentSettlementData.Capacity)
                ReleaseUnit(m_CurrentSettlementData.UnitStrength);
        }

        /// <summary>
        /// Creates a new unit with the given strength beside the settlement.
        /// </summary>
        /// <param name="strength">The amount of strength the new unit should start with.</param>
        private void ReleaseUnit(int strength)
        {
            RemoveFollowers(strength);

            if (UnitManager.Instance.IsTeamFull(m_Team)) return;

            // if the settlement contains a leader, the first unit to leave from it will be the leader.
            if (m_ContainsLeader)
            {
                GameController.Instance.RemoveLeader(m_Team);
                UnitManager.Instance.SpawnUnit(new MapPoint(m_OccupiedTile.x, m_OccupiedTile.z), m_Team, strength, true);
            }
            else
            {
                UnitManager.Instance.SpawnUnit(new MapPoint(m_OccupiedTile.x, m_OccupiedTile.z), m_Team, strength, false);
            }

        }

        /// <summary>
        /// Takes a number of followers from a unit and transfers them to the settlement.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> whose followers should be taken.</param>
        private void TakeFollowersFromUnit(Unit unit)
        {
            int amount = Mathf.Clamp(m_CurrentSettlementData.Capacity - m_FollowersInSettlement, 0, unit.Strength);

            if (unit.Class == UnitClass.LEADER)
                GameController.Instance.SetLeader(gameObject, m_Team);

            unit.LoseStrength(amount, isDamaged: false);
            AddFollowers(amount);
        }

        /// <summary>
        /// Gradually fills the settlement with followers over time.
        /// </summary>
        /// <returns>An <c>IEnumerator</c> which waits for a number of seconds before adding another follower.</returns>
        private IEnumerator FillSettlement()
        {
            while (true)
            {
                yield return new WaitForSeconds(m_CurrentSettlementData.FillRate);

                if (m_FollowersInSettlement == 0 || m_IsBurnedDown || m_IsDestroyed)
                    break;

                if (UnitManager.Instance.IsTeamFull(m_Team) || IsAttacked) 
                    continue;

                AddFollowers(1);
            }
        }

        /// <summary>
        /// Sets the state of the settlement to either contain the team leader or not.
        /// </summary>
        /// <param name="isLeader">True if the settlement should contain the team leader, false otherwise.</param>
        public void SetLeader(bool isLeader)
        {
            m_ContainsLeader = isLeader;
            m_TeamSymbols[(int)m_Team].SetActive(isLeader);//.GetComponent<ObjectActivator>().SetActiveClientRpc(isLeader);
        }

        #endregion


        #region UI

        /// <summary>
        /// Called when the mouse cursor hovers over the settlement.
        /// </summary>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData) => ToggleSettlementUIServer/*Rpc*/(true);

        /// <summary>
        /// Called when the mouse cursor stops hovering over the settlement.
        /// </summary>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerExit(PointerEventData eventData) => ToggleSettlementUIServer/*Rpc*/(false);


        /// <summary>
        /// Shows or hides the info for the settlement on the UI.
        /// </summary>
        /// <param name="show">True if the UI should be active, false otherwise.</param>
        /// <param name="parameters">RPC data for the server RPC.</param>
        //[ServerRpc(RequireOwnership = false)]
        private void ToggleSettlementUIServer/*Rpc*/(bool show, ServerRpcParams parameters = default)
        {
            if (m_IsBurnedDown) return;

            ToggleSettlementUIClient/*Rpc*/(show, m_CurrentSettlementIndex, m_CurrentSettlementData.Capacity, 
                m_FollowersInSettlement, GameController.Instance.TeamColors[(int)m_Team],
                new ClientRpcParams()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { parameters.Receive.SenderClientId }
                    }
                }
            );
        }

        /// <summary>
        /// Shows or hides the info for the settlement on the UI.
        /// </summary>
        /// <param name="show">True if the UI should be active, false otherwise.</param>
        /// <param name="settlementIndex">The index of the settlement.</param>
        /// <param name="maxCapacity">The maximum value of the capacity of the settlement.</param>
        /// <param name="currentCapacity">The current value of the capacity of the settlement.</param>
        /// <param name="teamColor">The color of the capacity bar.</param>
        /// <param name="parameters">RPC data for the client RPC.</param>
        //[ClientRpc]
        private void ToggleSettlementUIClient/*Rpc*/(bool show, int settlementIndex, int maxCapacity, int currentCapacity, Color teamColor, ClientRpcParams parameters = default)
        {
            GameUI.Instance.ToggleSettlementUI(
                show,
                settlementIndex,
                maxCapacity,
                currentCapacity,
                teamColor
            );
        }

        /// <summary>
        /// Updates the settlement info on the UI.
        /// </summary>
        /// <param name="settlementIndex">The index of the settlement.</param>
        /// <param name="maxCapacity">The maximum value of the capacity of the settlement.</param>
        /// <param name="currentCapacity">The current value of the capacity of the settlement.</param>
        /// <param name="parameters">RPC data for the client RPC.</param>
        //[ClientRpc]
        private void UpdateUnitUIClient/*Rpc*/(int settlementIndex, int maxCapacity, int currentCapacity, ClientRpcParams parameters = default)
            => GameUI.Instance.UpdateSettlementUI(settlementIndex, maxCapacity, currentCapacity);

        #endregion
    }
}