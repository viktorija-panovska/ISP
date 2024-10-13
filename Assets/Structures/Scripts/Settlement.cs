using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    public class Settlement : Structure, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Settlements")]
        [SerializeField] private GameObject[] m_SettlementObjects;
        [SerializeField] private SettlementData[] m_SettlementData;
        [SerializeField] private int m_SettlementObjectOffset = 5;

        [Header("Flag")]
        [SerializeField] private GameObject m_Flag;
        [SerializeField] private Color[] m_FlagColors;
        [SerializeField] private GameObject[] m_LeaderSigns;

        public new Team Team 
        { 
            get => m_Team; 
            set { 
                m_Team = value;
                m_Flag.GetComponent<MeshRenderer>().materials[1].color = m_FlagColors[(int)m_Team];
            }
        }

        private int m_CurrentSettlementIndex;
        private SettlementData m_CurrentSettlementData;

        private int m_Health;
        private int m_UnitsInHouse;
        private bool m_ContainsLeader;

        private Action<Settlement> OnStructureDestroyed;


        public void Start()
        {
            Vector3 startingScale = m_SettlementObjects[0].transform.localScale;

            for (int i = 0; i < m_SettlementData.Length; ++i)
                GameUtils.ResizeGameObject(
                    m_SettlementObjects[i],
                    m_SettlementData[i].Type == SettlementType.CITY ? 3 * Terrain.Instance.UnitsPerTileSide : Terrain.Instance.UnitsPerTileSide, 
                    scaleY: true
                );

            m_Flag.transform.position = new Vector3(0, m_SettlementObjects[0].transform.position.y, 0);
            m_Flag.transform.localScale = new Vector3(
                m_Flag.transform.localScale.x * (m_SettlementObjects[0].transform.localScale.x / startingScale.x),
                m_Flag.transform.localScale.y * (m_SettlementObjects[0].transform.localScale.y / startingScale.y),
                m_Flag.transform.localScale.z * (m_SettlementObjects[0].transform.localScale.z / startingScale.z)
            );

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

        public override void ReactToTerrainChange()
        {
            if (ShouldDestroyStructure())
            {
                OnStructureDestroyed?.Invoke(this);
                StructureManager.Instance.DespawnStructure(gameObject);
            }
            else
            {
                UpdateType();
            }
        }

        public override void Cleanup()
        {
            OnStructureDestroyed = null;
        }



        #region Settlement Type

        public void UpdateType()
        {
            if (!GameController.Instance.IsPlayerHosting) return;

            int fields = CreateFields();
            int settlementIndex = Mathf.Clamp(Mathf.CeilToInt((fields + 1) / 2f), 0, m_SettlementData.Length);
            SettlementData newSettlement = m_SettlementData[settlementIndex];

            if (m_CurrentSettlementData != null && m_CurrentSettlementData.Type == newSettlement.Type)
                return;

            if (m_CurrentSettlementData != null)
            {
                m_SettlementObjects[m_CurrentSettlementIndex].GetComponent<ObjectActivator>().SetActiveClientRpc(false);

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
                            Structure structure = Terrain.Instance.GetStructureOccupyingTile(neighborTile);

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

            m_SettlementObjects[m_CurrentSettlementIndex].GetComponent<ObjectActivator>().SetActiveClientRpc(true);


            UpdateColliderClientRpc(
                m_CurrentSettlementData.Type, 
                new Vector3(
                    Terrain.Instance.UnitsPerTileSide - m_SettlementObjectOffset, 
                    1, 
                    Terrain.Instance.UnitsPerTileSide - m_SettlementObjectOffset
                )
            );
            
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
                        Structure structure = Terrain.Instance.GetStructureOccupyingTile(neighborTile);
                        Field field = null;
                        if (structure && structure.GetType() == typeof(Field))
                            field = (Field)structure;

                        if (field == null)
                            field = StructureManager.Instance.SpawnField(neighborTile, Team);

                        if (!field.IsServingSettlement(this))
                        {
                            field.AddSettlementServed(this);
                            OnStructureDestroyed += field.OnSettlementRemoved;
                            field.OnFieldDestroyed += UpdateType;
                        }
                    }
                }
            }
        }

        [ClientRpc]
        private void UpdateColliderClientRpc(SettlementType settlement, Vector3 size, ClientRpcParams clientRpcParams = default)
            => GetComponent<BoxCollider>().size = settlement != SettlementType.CITY ? size : new Vector3(3 * size.x, size.y, 3 * size.z);

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

                    Structure structure = Terrain.Instance.GetStructureOccupyingTile(neighborTile);
                    Field field = null;

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Swamp) || structureType == typeof(Settlement) ||
                            (structureType == typeof(Field) && ((Field)structure).Team != Team))
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
                        field = StructureManager.Instance.SpawnField(neighborTile, Team);

                    if (!field.IsServingSettlement(this))
                    {
                        field.AddSettlementServed(this);
                        OnStructureDestroyed += field.OnSettlementRemoved;
                        field.OnFieldDestroyed += UpdateType;
                    }
                }
            }

            return fields < 0 ? 0 : fields;
        }

        public void RemoveField(Field field)
        {
            OnStructureDestroyed -= field.OnSettlementRemoved;
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
            GameUI.Instance.ToggleHealthBar(
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
            GameUI.Instance.UpdateHealthBar(maxHealth, currentHealth);
        }

        #endregion


        #region Leader

        public void MakeLeader()
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