using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    public enum UnitState
    {
        SETTLE,
        GO_TO_FLAG,
        GATHER,
        BATTLE
    }

    public class Unit : NetworkBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject[] m_LeaderSigns;
        [SerializeField] private GameObject m_Sword;

        private Team m_Team;
        public Team Team { get => m_Team; }

        private UnitData m_UnitData;
        public UnitData UnitData { get => m_UnitData; }

        private bool m_IsLeader;
        public bool IsLeader 
        {
            get => m_IsLeader; 
            set { m_IsLeader = value; m_LeaderSigns[(int)m_Team].GetComponent<ObjectActivator>().SetActiveClientRpc(m_IsLeader); }
        }

        private bool m_IsKnight;
        public bool IsKnight { get => m_IsKnight; set => m_IsKnight = value; }

        public MapPoint ClosestMapPoint { get => new(gameObject.transform.position.x, gameObject.transform.position.z); }

        private UnitState m_LastState;
        private UnitState m_CurrentState;
        public UnitState CurrentState { get => m_CurrentState; }

        private UnitMovementHandler m_MovementHandler;

        private int m_CurrentHealth;


        public void Setup(Team team, UnitData unitData)
        {
            m_Team = team;
            m_UnitData = unitData;
            m_CurrentHealth = m_UnitData.MaxHealth;

            m_MovementHandler = GetComponent<UnitMovementHandler>();
            m_MovementHandler.InitializeMovement();
        }


        public void RecalculateHeight()
        {
            float height;

            int startHeight = m_MovementHandler.StartLocation.Y;
            int endHeight = m_MovementHandler.EndLocation.Y;

            if (startHeight == endHeight)
                height = startHeight;
            else
            {
                float heightDifference = Mathf.Abs(endHeight - startHeight);
                float totalDistance = new Vector2(
                    m_MovementHandler.EndLocation.X - m_MovementHandler.StartLocation.X, 
                    m_MovementHandler.EndLocation.Z - m_MovementHandler.StartLocation.Z
                ).magnitude;

                float distance = startHeight < endHeight
                    ? new Vector2(transform.position.x - m_MovementHandler.StartLocation.X, transform.position.z - m_MovementHandler.StartLocation.Z).magnitude
                    : new Vector2(m_MovementHandler.EndLocation.X - transform.position.x, m_MovementHandler.EndLocation.Z - transform.position.z).magnitude;

                height = heightDifference * distance / totalDistance;
                height = startHeight < endHeight ? startHeight + height : endHeight + height;
            }

            if (height <= Terrain.Instance.WaterLevel)
                UnitManager.Instance.DespawnUnit(gameObject);
            else
                SetHeightClientRpc(height);
        }

        [ClientRpc]
        private void SetHeightClientRpc(float height) => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        [ClientRpc]
        public void RotateClientRpc(Vector3 lookPosition) => transform.rotation = Quaternion.LookRotation(lookPosition);


        public void GoToFlag()
        {
            if (m_CurrentState != UnitState.GO_TO_FLAG)
                return;

            m_MovementHandler.FlagReached = false;

            if (!m_IsLeader)
                m_MovementHandler.FollowLeader();
            else
            {
                Vector3 target = StructureManager.Instance.GetFlagPosition(m_Team);
                m_MovementHandler.SetPath(target);
            }
        }

        public void FlagReached()
        {
            if (m_CurrentState != UnitState.GO_TO_FLAG)
                return;

            m_MovementHandler.FlagReached = true;
        }


        #region State

        public void SwitchState(UnitState state)
        {
            m_LastState = m_CurrentState;
            m_CurrentState = state;

            if (m_LastState == UnitState.BATTLE)
                ShowSword(false);

            switch (m_CurrentState)
            {
                case UnitState.GO_TO_FLAG:
                    GoToFlag();
                    break;

                case UnitState.SETTLE:
                    break;

                case UnitState.GATHER:
                    break;

                case UnitState.BATTLE:
                    ShowSword(true);
                    break;
            }
        }

        private void ShowSword(bool show) => m_Sword.GetComponent<ObjectActivator>().SetActiveClientRpc(show);

        #endregion


        #region Health Bar

        public void OnPointerEnter(PointerEventData eventData)
            => ToggleHealthBarServerRpc(show: true);

        public void OnPointerExit(PointerEventData eventData)
            => ToggleHealthBarServerRpc(show: false);

        [ServerRpc(RequireOwnership = false)]
        public void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
        {
            ToggleHealthBarClientRpc(show, m_UnitData.MaxHealth, m_CurrentHealth, UnitManager.Instance.UnitColors[(int)m_Team], new ClientRpcParams
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
                transform.position + Vector3.up * GetComponent<Renderer>().bounds.size.y
            );
        }

        [ClientRpc]
        private void UpdateHealthBarClientRpc(int maxHealth, int currentHealth, Team team, ClientRpcParams parameters = default)
        {
            GameUI.Instance.UpdateHealthBar(maxHealth, currentHealth);
        }

        #endregion

    }
}