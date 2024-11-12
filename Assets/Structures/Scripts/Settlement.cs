using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    /// <summary>
    /// 
    /// </summary>
    public class Settlement : Structure, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Settlements")]
        [SerializeField] private GameObject[] m_SettlementObjects;
        [SerializeField] private SettlementData[] m_SettlementData;
        [SerializeField] private int m_SettlementObjectOffset = 5;
        [SerializeField] private GameObject m_RuinedSettlement;

        [Header("Flag")]
        [SerializeField] private GameObject[] m_Flags;
        [SerializeField] private Color[] m_FlagColors;
        [SerializeField] private GameObject[] m_LeaderSigns;

        public new Team Team 
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

        private int m_CurrentSettlementIndex;
        private SettlementData m_CurrentSettlementData;

        public SettlementType Type { get => m_CurrentSettlementData.Type; }

        private int m_Health;
        private int m_UnitsInHouse;
        private bool m_ContainsLeader;

        public bool HasSpace { get => m_UnitsInHouse < m_CurrentSettlementData.FollowerCapacity; }

        private Action<Settlement> OnSettlementDestroyed;
        private Action OnSettlementBurned;

        private bool m_IsAttacked;
        public bool IsAttacked { get => m_IsAttacked; set => m_IsAttacked = value; }

        private bool m_IsRuined;
        public bool IsRuined { get => m_IsRuined; }

        public int UnitStrength = 1;


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
                flag.transform.position = new Vector3(0, m_SettlementObjects[0].transform.position.y, 0);
                flag.transform.localScale = new Vector3(
                    flag.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    flag.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    flag.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
            }

            foreach (GameObject sign in m_LeaderSigns)
            {
                sign.transform.position = new Vector3(0, m_SettlementObjects[0].transform.position.y, Terrain.Instance.UnitsPerTileSide);
                sign.transform.localScale = new Vector3(
                    sign.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                    sign.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                    sign.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
                );
            }

            UpdateType();
        }

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();
            if (!unit) return;

            if (unit.Team == m_Team)
            {
                // share strength?
            }

            if (unit.Team != m_Team && !IsAttacked)
                UnitManager.Instance.AttackSettlement(unit, this);
        }




        /// <inheritdoc />
        public override void ReactToTerrainChange()
        {
            if (ShouldDestroyStructure())
            {
                OnSettlementDestroyed?.Invoke(this);
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

            if (!m_IsRuined)
            {
                UpdateType();
            }
        }

        /// <inheritdoc />
        public override void Cleanup()
        {
            OnSettlementDestroyed = null;
        }




        #region Settlement Type

        public void UpdateType()
        {
            //if (!GameController.Instance.IsPlayerHosting) return;

            int fields = CreateFields();
            int settlementIndex = Mathf.Clamp(Mathf.CeilToInt((fields + 1) / 2f), 0, m_SettlementData.Length);
            SettlementData newSettlement = m_SettlementData[settlementIndex];

            if (m_CurrentSettlementData != null && m_CurrentSettlementData.Type == newSettlement.Type)
                return;

            if (m_CurrentSettlementData != null)
            {
                m_SettlementObjects[m_CurrentSettlementIndex].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);

                if (m_CurrentSettlementData.Type == SettlementType.CITY)
                {
                    // destroy fields that are not on the diagonals or on the parallels.
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

                            ((Field)structure).OnSettlementRemoved(this);
                        }
                    }
                }
            }

            m_CurrentSettlementIndex = settlementIndex;
            m_CurrentSettlementData = newSettlement;
            m_Health = newSettlement.MaxHealth;

            m_SettlementObjects[m_CurrentSettlementIndex].SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(true);

            Vector3 size = new (Terrain.Instance.UnitsPerTileSide, 1, Terrain.Instance.UnitsPerTileSide);
            GetComponent<BoxCollider>().size = m_CurrentSettlementData.Type != SettlementType.CITY ? size : new Vector3(3 * size.x, size.y, 3 * size.z);

            //UpdateColliderClientRpc(m_CurrentSettlementData.Type != SettlementType.CITY ? size : new Vector3(3 * size.x, size.y, 3 * size.z));

            if (m_CurrentSettlementData.Type == SettlementType.CITY)
            {
                // fill in the blank spaces between the parallels and diagonals
                for (int z = -2; z <= 2; ++z)
                {
                    for (int x = -2; x <= 2; ++x)
                    {
                        if (x == 0 || z == 0 || Mathf.Abs(x) == Mathf.Abs(z))
                            continue;

                        (int x, int z) neighborTile = (m_OccupiedTile.x + x, m_OccupiedTile.z + z);
                        Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);
                        Field field = null;
                        if (structure && structure.GetType() == typeof(Field))
                            field = (Field)structure;

                        if (field == null)
                            field = StructureManager.Instance.SpawnField(neighborTile, m_Team);

                        if (!field.IsServingSettlement(this))
                        {
                            field.AddSettlementServed(this);
                            OnSettlementDestroyed += field.OnSettlementRemoved;
                            field.OnFieldDestroyed += UpdateType;
                        }
                    }
                }
            }
        }

        [ClientRpc]
        private void UpdateColliderClientRpc(Vector3 size, ClientRpcParams clientRpcParams = default)
            => GetComponent<BoxCollider>().size = size;

        // Count surrounding flat spaces (and set them to fields) in 5x5 space around the settlement
        private int CreateFields()
        {
            int fields = 0;

            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    (int x, int z) neighborTile = (m_OccupiedTile.x + x, m_OccupiedTile.z + z);

                    if ((x, z) == (0, 0) || (x != 0 && z != 0 && Mathf.Abs(x) != Mathf.Abs(z)) ||
                        neighborTile.x < 0 || neighborTile.z < 0 ||
                        neighborTile.x >= Terrain.Instance.TilesPerSide || neighborTile.z >= Terrain.Instance.TilesPerSide ||
                        !Terrain.Instance.IsTileFlat(neighborTile))
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);
                    Field field = null;

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Swamp) || structureType == typeof(Settlement) ||
                            (structureType == typeof(Field) && ((Field)structure).Team != m_Team))
                            continue;

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

                    if (field == null)
                        field = StructureManager.Instance.SpawnField(neighborTile, m_Team);

                    if (!field.IsServingSettlement(this))
                    {
                        field.AddSettlementServed(this);
                        OnSettlementDestroyed += field.OnSettlementRemoved;
                        OnSettlementBurned += field.BurnField;
                        field.OnFieldDestroyed += UpdateType;
                    }
                }
            }

            return fields < 0 ? 0 : fields;
        }

        public void RemoveField(Field field)
        {
            OnSettlementDestroyed -= field.OnSettlementRemoved;
        }

        public void BurnSettlement()
        {
            m_IsRuined = true;
            Team = Team.NONE;

            if (m_CurrentSettlementData != null)
            {
                m_SettlementObjects[m_CurrentSettlementIndex].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
                m_CurrentSettlementData = null;
            }

            m_RuinedSettlement.SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(true);

            //UpdateColliderClientRpc(Vector3.zero);
            GetComponent<BoxCollider>().size = Vector3.zero;

            OnSettlementBurned?.Invoke();
        }


        #endregion


        #region Units

        public void AddUnit(bool isLeader)
        {
            Debug.Log("Unit");
            m_UnitsInHouse++;
            if (isLeader) AddLeader();
        }


        #endregion


        #region HealthBar

        public void OnPointerEnter(PointerEventData eventData)
            => ToggleHealthBarServerRpc(show: true);

        public void OnPointerExit(PointerEventData eventData)
            => ToggleHealthBarServerRpc(show: false);

        [ServerRpc(RequireOwnership = false)]
        public void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
        {
            ToggleHealthBarClientRpc(show, m_CurrentSettlementData.MaxHealth, m_Health, m_FlagColors[(int)m_Team], new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { parameters.Receive.SenderClientId }
                }
            });
        }

        [ClientRpc]
        private void ToggleHealthBarClientRpc(bool show, int maxHealth, int currentHealth, Color teamColor, ClientRpcParams parameters = default)
        {
            GameUI.Instance.ToggleUnitUI(
                show,
                maxHealth,
                currentHealth,
                teamColor,
                transform.position + Vector3.up * m_SettlementObjects[m_CurrentSettlementIndex].GetComponent<Renderer>().bounds.size.y
            );
        }

        [ClientRpc]
        private void UpdateHealthBarClientRpc(int maxHealth, int currentHealth, Team team, ClientRpcParams parameters = default)
        {
            GameUI.Instance.UpdateStrengthBar(maxHealth, currentHealth);
        }

        #endregion


        #region Leader

        public void AddLeader()
        {
            m_ContainsLeader = true;
            m_LeaderSigns[(int)m_Team].GetComponent<ObjectActivator>().SetActiveClientRpc(true);
        }

        private void RemoveLeader()
        {
            m_ContainsLeader = false;
            m_LeaderSigns[(int)m_Team].GetComponent<ObjectActivator>().SetActiveClientRpc(false);
        }

        public void ReleaseLeader()
        {
            //ReleaseUnit(newUnit: false);
            RemoveLeader();
        }

        #endregion
    }
}