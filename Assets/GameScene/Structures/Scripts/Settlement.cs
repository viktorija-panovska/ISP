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
    public class Settlement : Structure, IInspectableObject, ILeader
    {
        #region Inspector Fields

        [Tooltip("The collider that detects the cursor's interactions with the settlement.")]
        [SerializeField] private BoxCollider m_SettlementCollision;
        [Tooltip("The collider that detects units near the settlement.")]
        [SerializeField] private BoxCollider m_SettlementTrigger;
        [Tooltip("The GameObject of the highlight enabled when the unit is clicked in Query mode.")]
        [SerializeField] private GameObject m_Highlight;
        [Tooltip("The GameObject of the icon for the unit on the minimap.")]
        [SerializeField] private GameObject m_MinimapIcon;

        [Header("Settlements")]
        [Tooltip("The GameObjects of each type of settlement, in order as they are defined in the SettlementType enum.")]
        [SerializeField] private GameObject[] m_SettlementObjects;
        [Tooltip("The SerializableObjects with the data for each type of settlement, in order as they are defined in the SettlementType enum.")]
        [SerializeField] private SettlementData[] m_SettlementData;

        [Header("Flag")]
        [Tooltip("The GameObjects of the flags denoting the faction that the settlement belongs to, with index 0 being the red faction and index 1 the blue.")]
        [SerializeField] private GameObject[] m_Flags;
        [Tooltip("The GameObjects of the symbols denoting that the faction leader is in the settlement, with index 0 being the red faction and index 1 the blue.")]
        [SerializeField] private GameObject[] m_LeaderSymbols;

        #endregion


        #region Class Fields

        /// <summary>
        /// The GameObject associated with this settlement.
        /// </summary>
        public GameObject GameObject { get => gameObject; }

        /// <summary>
        /// The data of the type of settlement currently active.
        /// </summary>
        private SettlementData m_CurrentSettlementData;

        /// <summary>
        /// Gets the type of the settlement.
        /// </summary>
        public SettlementType Type { get => m_CurrentSettlementData.Type; }
        /// <summary>
        /// Gets the maximum amount of followers that can be in the settlement.
        /// </summary>
        public int Capacity { get => m_CurrentSettlementData.Capacity; }
        /// <summary>
        /// The strength of a unit released from this settlement.
        /// </summary>
        public int ReleasedUnitStrength { get => m_CurrentSettlementData.ReleasedUnitStrength; }

        private int m_FollowersInSettlement;
        /// <summary>
        /// Gets the number of followers currently inside the settlement.
        /// </summary>
        public int FollowersInSettlement { get => m_FollowersInSettlement; }

        /// <summary>
        /// True if the settlement cannot contain any more followers, false otherwise.
        /// </summary>
        public bool IsSettlementFull { get => m_FollowersInSettlement == m_CurrentSettlementData.Capacity; }

        /// <summary>
        /// True if the settlement contains the faction leader, false otherwise.
        /// </summary>
        private bool m_ContainsLeader;

        private bool m_IsAttacked;
        /// <summary>
        /// True if the settlement is currently under attack by a unit from the enemy faction, false otherwise.
        /// </summary>
        public bool IsAttacked { get => m_IsAttacked; set => m_IsAttacked = value; }

        /// <summary>
        /// True if the settlement has been destroyed, false otherwise.
        /// </summary>
        private bool m_IsDestroyed = false;

        private readonly NetworkVariable<bool> m_IsInspected = new();
        /// <summary>
        /// True if the settlement is being inspected by any player, false otherwise.
        /// </summary>
        public bool IsInspected { get => m_IsInspected.Value; set => m_IsInspected.Value = value; }

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the settlement is destroyed.
        /// </summary>
        public Action<Settlement> OnSettlementDestroyed;
        /// <summary>
        /// Action to be called when the settlement's faction has been changed.
        /// </summary>
        public Action<Faction> OnSettlementFactionChanged;

        #endregion


        #region Event Functions

        private void Start() => m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();
            if (!unit) return;

            //if (unit.Faction == m_Faction && m_ContainsLeader)
            //    GameController.Instance.SetLeader(unit.Faction, unit);
            //else if (unit.Faction == m_Faction && !IsSettlementFull && unit.CanEnterSettlement && unit.Behavior != UnitBehavior.GO_TO_MAGNET)
            //    TakeFollowersFromUnit(unit);

            //if (unit.Faction != m_Faction && !IsAttacked && unit.Behavior != UnitBehavior.GO_TO_MAGNET)
            //    UnitManager.Instance.AttackSettlement(unit, this);
        }

        #endregion


        #region Setup

        /// <inheritdoc/>
        public override void Setup(Faction faction, TerrainTile occupiedTile)
        {
            base.Setup(faction, occupiedTile);
            GameController.Instance.OnArmageddon += ReactToArmageddon;

            SetupMinimapIcon();
            ScaleObjects();

            SetObjectInfo_ClientRpc(
                gameObject.GetComponent<NetworkObject>().NetworkObjectId,
                $"{m_Faction} Settlement",
                LayerData.FactionLayers[(int)m_Faction]
            );

            SetType();
            StartCoroutine(FillSettlement());
        }

        /// <summary>
        /// Sets up some <c>GameObject</c> properties for the given settlement.
        /// </summary>
        /// <param name="settlementNetworkId">The <c>NetworkObjectId</c> of the settlement.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the settlement.</param>
        /// <param name="layer">An <c>int</c> representing the layer the settlement should be on.</param>
        [ClientRpc]
        private void SetObjectInfo_ClientRpc(ulong settlementNetworkId, string name, int layer)
        {
            GameObject settlementObject = GetNetworkObject(settlementNetworkId).gameObject;
            settlementObject.name = name;
            settlementObject.layer = layer;
        }

        /// <summary>
        /// Sets up the icon that represents the settlement on the minimap.
        /// </summary>
        private void SetupMinimapIcon()
        {
            if (m_Faction == Faction.NONE)
            {
                m_MinimapIcon.SetActive(false);
                return;
            }

            float scale = Terrain.Instance.UnitsPerSide / StructureManager.Instance.MinimapIconScale;
            m_MinimapIcon.transform.localScale = new(scale, m_MinimapIcon.transform.localScale.y, scale);
            m_MinimapIcon.GetComponent<MeshRenderer>().material.color = StructureManager.Instance.MinimapSettlementColors[(int)m_Faction];
        }

        /// <summary>
        /// Sets the scale of all the GameObjects that make up the settlement to match the size of the terrain tile.
        /// </summary>
        private void ScaleObjects()
        {
            // resize all settlement objects to match the size of the terrain tiles.
            Vector3 startingScale = m_SettlementObjects[0].transform.localScale;

            for (int i = 0; i < m_SettlementData.Length; ++i)
                GameUtils.ResizeGameObject(
                    m_SettlementObjects[i],
                    m_SettlementData[i].Type == SettlementType.CITY ? 3 * Terrain.Instance.UnitsPerTileSide : Terrain.Instance.UnitsPerTileSide,
                    scaleY: true
                );

            GameUtils.ResizeGameObject(m_Highlight, Terrain.Instance.UnitsPerTileSide * 1.5f);

            foreach (GameObject flag in m_Flags)
            {
                flag.transform.localScale = new Vector3(
                    flag.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    flag.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    flag.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
                flag.transform.localPosition = new Vector3(-Terrain.Instance.UnitsPerTileSide / 2, 0, -Terrain.Instance.UnitsPerTileSide / 2);
            }

            foreach (GameObject sign in m_LeaderSymbols)
            {
                sign.transform.localScale = new Vector3(
                    sign.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    sign.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    sign.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
                sign.transform.localPosition = new Vector3(-Terrain.Instance.UnitsPerTileSide / 2, 0, Terrain.Instance.UnitsPerTileSide / 2);
            }
        }

        #endregion


        #region Structure Overrides

        /// <inheritdoc />
        public override void ReactToTerrainChange()
        {
            if (ShouldDestroyStructure())
            {
                Destroy(updateNearbySettlements: false);
                return;
            }

            // settlement is burned down
            if (m_DestroyMethod == DestroyMethod.DROWN)
            {
                SetHeight_ClientRpc((int)m_OccupiedTile.GetCenterHeight());
                return;
            }

            if (m_IsDestroyed) return;

            // if the settlement wasn't destroyed but it was in the range of the
            // terrain modification check if it has gained or lost any fields
            SetType();
        }

        /// <inheritdoc />
        public override void Cleanup()
        {
            base.Cleanup();

            OnSettlementDestroyed = null;
            StructureManager.Instance.OnRemoveReferencesToSettlement?.Invoke(this);
            GameController.Instance.OnArmageddon -= ReactToArmageddon;
            GameController.Instance.RemoveInspectedObject(this);
        }

        #endregion


        #region Settlement Type

        /// <summary>
        /// Sets the type of this settlement based on the number of fields that are created around it.
        /// </summary>
        public void SetType()
        {
            if (!IsHost) return;

            int fields = StructureManager.Instance.CreateSettlementFields(this);

            // formula for getting the index of the settlement from the number of fields
            int settlementIndex = Mathf.Clamp(Mathf.CeilToInt((fields + 1) / 2f), 0, m_SettlementData.Length);

            SettlementData newSettlement = m_SettlementData[settlementIndex];

            if (m_CurrentSettlementData && m_CurrentSettlementData.Type == newSettlement.Type)
                return;

            // remove stuff from old settlement
            if (m_CurrentSettlementData)
            {
                ToggleSettlement_ClientRpc(m_CurrentSettlementData.Type, false);
                if (m_CurrentSettlementData.Type == SettlementType.CITY)
                    StructureManager.Instance.RemoveCityFields(this);
            }

            m_CurrentSettlementData = newSettlement;

            Vector3 colliderSize = new(Terrain.Instance.UnitsPerTileSide, 1, Terrain.Instance.UnitsPerTileSide);
            if (m_CurrentSettlementData.Type == SettlementType.CITY)
            {
                StructureManager.Instance.AddCityFields(this);
                colliderSize = new(3 * Terrain.Instance.UnitsPerTileSide, 1, 3 * Terrain.Instance.UnitsPerTileSide);
            }

            ToggleSettlement_ClientRpc(m_CurrentSettlementData.Type, true);

            if (m_FollowersInSettlement > m_CurrentSettlementData.Capacity)
                ReleaseUnit(m_FollowersInSettlement - m_CurrentSettlementData.Capacity);

            // change the size of the collider
            if (m_SettlementCollision.size != colliderSize)
                SetColliderSize_ClientRpc(colliderSize);

            if (IsInspected)
                GameController.Instance.UpdateInspectedSettlement(this, updateType: true);
        }

        /// <summary>
        /// Sets the size of the collider of the settlement.
        /// </summary>
        /// <param name="size">A <c>Vector3</c> of the new size of the collider.</param>
        /// <param name="clientRpcParams">RPC data for the client RPC.</param>
        [ClientRpc]
        private void SetColliderSize_ClientRpc(Vector3 size, ClientRpcParams clientRpcParams = default) 
        {
            m_SettlementCollision.size = size;
            m_SettlementTrigger.size = size;
        }

        /// <summary>
        /// Activates or deactivates settlement of the given type.
        /// </summary>
        /// <param name="type">The type of the settlement that should be activated.</param>
        /// <param name="isOn">True if the sign should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleSettlement_ClientRpc(SettlementType type, bool isOn)
        {
            //m_SettlementObjects[(int)type].SetActive(isOn);

            GameObject settlement = GameUtils.GetChildWithTag(gameObject, TagData.SettlementTags[(int)type]);
            if (!settlement) return;
            settlement.SetActive(isOn);
        }

        #endregion


        #region Followers

        /// <summary>
        /// Adds the given amount of followers to the settlement.
        /// </summary>
        /// <param name="amount">The amount of followers to be added.</param>
        private void AddFollowers(int amount)
        {
            SetFollowers(m_FollowersInSettlement + amount);
            UnitManager.Instance.AddFollowers(m_Faction, amount);
        }

        /// <summary>
        /// Removes the given amount of followers from the settlement.
        /// </summary>
        /// <param name="amount">The amount of followers to be removed.</param>
        private void RemoveFollowers(int amount, bool updatePopulation)
        {
            SetFollowers(m_FollowersInSettlement - amount);

            if (updatePopulation)
                UnitManager.Instance.RemovePopulation(m_Faction, amount);
        }

        /// <summary>
        /// Sets the amount of followers in the settlement to the given number.
        /// </summary>
        /// <param name="amount">The amount of followers in the settlement.</param>
        private void SetFollowers(int amount)
        {
            m_FollowersInSettlement = Mathf.Clamp(amount, 0, m_CurrentSettlementData.Capacity);

            if (m_FollowersInSettlement == 0)
                StructureManager.Instance.DespawnStructure(gameObject);

            if (m_FollowersInSettlement >= m_CurrentSettlementData.Capacity)
                ReleaseUnit(m_CurrentSettlementData.ReleasedUnitStrength);

            if (IsInspected)
                GameController.Instance.UpdateInspectedSettlement(this, updateFollowers: true);
        }

        /// <summary>
        /// Creates a new unit with the given strength beside the settlement.
        /// </summary>
        /// <param name="strength">The amount of strength the new unit should start with.</param>
        private void ReleaseUnit(int strength)
        {
            if (strength > m_FollowersInSettlement - 1)
                strength = m_FollowersInSettlement - 1;

            RemoveFollowers(strength, updatePopulation: false);

            UnitManager.Instance.SpawnUnit(
                location: new TerrainPoint(m_OccupiedTile.X, m_OccupiedTile.Z),
                faction: m_Faction,
                type: m_ContainsLeader ? UnitType.LEADER : UnitType.WALKER,
                strength: strength,
                origin: this
            );
        }

        /// <summary>
        /// Takes a number of followers from a unit and transfers them to the settlement.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> whose followers should be taken.</param>
        private void TakeFollowersFromUnit(Unit unit)
        {
            if (unit.Faction != m_Faction) return;

            // TODO: decide on the amount of followers taken
            int amount = Mathf.Clamp(m_CurrentSettlementData.Capacity - m_FollowersInSettlement, 0, unit.Strength);

            if (unit.Type == UnitType.LEADER)
                GameController.Instance.SetLeader(m_Faction, this);

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

                if (m_FollowersInSettlement == 0 || m_IsDestroyed)
                    break;

                if (UnitManager.Instance.IsFactionFull(m_Faction) || IsAttacked)
                    continue;

                AddFollowers(1);
            }
        }

        /// <summary>
        /// Sets the state of the settlement to either contain the faction leader or not.
        /// </summary>
        /// <param name="isLeader">True if the settlement should contain the daction leader, false otherwise.</param>
        public void SetLeader(bool isLeader)
        {
            m_ContainsLeader = isLeader;
            ToggleLeaderSign_ClientRpc(m_Faction, m_ContainsLeader);
        }

        /// <summary>
        /// Activates or deactivates the sign in front of the settlement if a leader is inside.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader sign should be activated.</param>
        /// <param name="isOn">True if the sign should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleLeaderSign_ClientRpc(Faction faction, bool isOn)
        {
            //m_LeaderSigns[(int)faction].SetActive(isOn);

            GameObject flag = GameUtils.GetChildWithTag(gameObject, TagData.LeaderTags[(int)faction]);
            if (!flag) return;
            flag.SetActive(isOn);
        }


        #endregion


        #region Settlement Changes

        /// <summary>
        /// Changes the faction the settlement belongs to to the given faction.
        /// </summary>
        /// <param name="newFaction">The new <c>Faction</c> the settlement should belong to.</param>
        public void ChangeFaction(Faction newFaction)
        {
            if (newFaction == m_Faction) return;

            ToggleFlag_ClientRpc(m_Faction, false);

            m_Faction = newFaction;

            SetObjectInfo_ClientRpc(
                GetComponent<NetworkObject>().NetworkObjectId,
                $"{m_Faction} Settlement",
                LayerData.FactionLayers[(int)m_Faction]
            );

            OnSettlementFactionChanged?.Invoke(m_Faction);  // changes the faction of the fields
            StructureManager.Instance.UpdateNearbySettlements(m_OccupiedTile);
            SetupMinimapIcon();

            if (m_Faction == Faction.NONE)
            {
                GameController.Instance.RemoveInspectedObject(this);
                return;
            }

            ToggleFlag_ClientRpc(m_Faction, true);

            if (IsInspected)
                GameController.Instance.UpdateInspectedSettlement(this, updateTeam: true);
        }

        /// <summary>
        /// Destroys the settlement as a result of an attack by a Knight.
        /// </summary>
        public void BurnDown()
        {
            if (m_IsDestroyed) return;

            m_IsDestroyed = true;
            OnSettlementFactionChanged?.Invoke(Faction.NONE);  // burns down the fields

            UnitManager.Instance.RemovePopulation(m_Faction, m_FollowersInSettlement);
            GameController.Instance.RemoveInspectedObject(this);

            StructureManager.Instance.CreateRuin(this);
        }

        /// <summary>
        /// Destroys this settlement.
        /// </summary>
        /// <param name="updateNearby">True if the nearby settlements should be updates, false if not.</param>
        public void Destroy(bool updateNearbySettlements)
        {
            if (m_IsDestroyed) return;

            m_IsDestroyed = true;
            OnSettlementDestroyed?.Invoke(this);    // also destroys fields

            if (m_OccupiedTile.IsUnderwater())
                UnitManager.Instance.RemovePopulation(m_Faction, m_FollowersInSettlement);
            else
                ReleaseUnit(m_CurrentSettlementData.ReleasedUnitStrength);

            StructureManager.Instance.DespawnStructure(gameObject, updateNearbySettlements);
        }

        /// <summary>
        /// Called when the Armageddon Divine Intervention is activated.
        /// </summary>
        public void ReactToArmageddon() => Destroy(updateNearbySettlements: false);

        /// <summary>
        /// Activates or deactivates the flag with the faction's color.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose flag should be activated.</param>
        /// <param name="isOn">True if the flag should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleFlag_ClientRpc(Faction faction, bool isOn)
        {
            //m_Flags[(int)faction].SetActive(isOn);

            GameObject flag = GameUtils.GetChildWithTag(gameObject, TagData.FactionTags[(int)faction]);
            if (!flag) return;
            flag.SetActive(isOn);
        }

        #endregion


        #region Inspecting

        /// <summary>
        /// Called when the mouse cursor hovers over the settlement.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!PlayerController.Instance.IsQueryModeActive) return;
            SetHighlight(true);
        }

        /// <summary>
        /// Called when the mouse cursor stops hovering over the settlement.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (m_IsInspected.Value) return;
            SetHighlight(false);
        }

        /// <summary>
        /// Called when the mouse cursor clicks on the settlement.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!PlayerController.Instance.IsQueryModeActive) return;
            GameController.Instance.SetInspectedObject_ServerRpc(PlayerController.Instance.Faction, GetComponent<NetworkObject>());
        }

        /// <summary>
        /// Activates or deactivates the highlight of the settlement.
        /// </summary>
        /// <param name="shouldActivate">True if the highlight should be activated, false otherwise.</param>
        public void SetHighlight(bool shouldActivate) => m_Highlight.SetActive(shouldActivate);

        #endregion
    }
}