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
        [SerializeField] private UnitCloseRangeDetector m_CloseRangeDetector;
        [SerializeField] private UnitMidRangeDetector m_MidRangeDetector;
        [SerializeField] private UnitWideRangeDetector m_WideRangeDetector;

        [SerializeField] private int m_CloseRangeRadius = 15;
        [SerializeField] private int m_MidRangeTilesPerSide = 1;
        [SerializeField] private int m_WideRangeTilesPerSide = 20;

        private Team m_Team;
        public Team Team { get => m_Team; }

        private int m_MaxHealth;
        public int MaxHealth { get => m_MaxHealth; }

        private int m_CurrentHealth;
        public int CurrentHealth { get => m_CurrentHealth; }

        private int m_Strength;
        public int Strength { get => m_Strength; }

        private int m_Speed;
        public int Speed { get => m_Speed; }

        private int m_ManaGain;
        public int ManaGain { get => m_ManaGain; }

        private bool m_IsLeader;
        public bool IsLeader
        {
            get => m_IsLeader;
            set { m_IsLeader = value; m_LeaderSigns[(int)m_Team].GetComponent<ObjectActivator>().SetActiveClientRpc(m_IsLeader); }
        }

        private bool m_IsKnight;
        public bool IsKnight { get => m_IsKnight; set => m_IsKnight = value; }

        private bool m_IsBattling;
        public bool IsBattling { get => m_IsBattling; set => m_IsBattling = value; }

        public MapPoint ClosestMapPoint { get => new(gameObject.transform.position.x, gameObject.transform.position.z); }

        private UnitState m_LastState;
        public UnitState LastState { get => m_LastState; }

        private UnitState m_CurrentState;
        public UnitState CurrentState { get => m_CurrentState; }

        private UnitMovementHandler m_MovementHandler;




        public void Setup(Team team, UnitData unitData)
        {
            m_Team = team;
            m_MaxHealth = unitData.MaxHealth;
            m_Strength = unitData.Strength;
            m_Speed = unitData.Speed;
            m_ManaGain = unitData.ManaGain;
            m_CurrentHealth = m_MaxHealth;

            m_MovementHandler = GetComponent<UnitMovementHandler>();
            m_MovementHandler.InitializeMovement();

            m_CloseRangeDetector.Setup(this);
            m_WideRangeDetector.Setup(team, m_WideRangeTilesPerSide);
            m_MidRangeDetector.Setup(this, m_MidRangeTilesPerSide);
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

            // TODO: ones on the edges shouldn't disappear if they haven't been sunk
            if (height <= Terrain.Instance.WaterLevel)
                UnitManager.Instance.DespawnUnit(gameObject);
            else
                SetHeightClientRpc(height);
        }

        [ClientRpc]
        private void SetHeightClientRpc(float height) => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        //[ClientRpc]
        public void Rotate/*ClientRpc*/(Vector3 lookPosition) => transform.rotation = Quaternion.LookRotation(lookPosition);


        public void AbsorbUnit(Unit unit)
        {

        }


        public void StartBattle()
        {
            m_IsBattling = true;
            m_MovementHandler.Pause(true);
        }

        public void EndBattle()
        {
            m_IsBattling = false;
            m_MovementHandler.Pause(false);
        }

        public void TakeDamage(int damage) => m_CurrentHealth -= damage;

        public void PauseMovement(bool pause) => m_MovementHandler.Pause(pause);


        public Vector3 GetRoamingDirection() => m_WideRangeDetector.GetAverageDirection();

        public void NewTargetAcquired(GameObject target)
        {
            if (target.GetComponent<Unit>() != null)
                m_MovementHandler.FollowUnit(target.GetComponent<Unit>());
        }

        public void TargetLost(GameObject target)
        {
            if (target.GetComponent<Unit>() != null)
                m_MovementHandler.StopFollowingUnit(target.GetComponent<Unit>());
        }

        public void GoToFlag()
        {
            if (m_CurrentState != UnitState.GO_TO_FLAG) return;

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
            if (m_CurrentState != UnitState.GO_TO_FLAG) return;
            m_MovementHandler.FlagReached = true;
        }


        public void RemoveRefrencesToUnit(Unit unit)
        {
            m_WideRangeDetector.RemoveUnit(unit);
            m_MovementHandler.StopFollowingUnit(unit);
            m_MidRangeDetector.RemoveTarget(unit.gameObject);
        }

        public void RemoveRefrencesToSettlement(Settlement settlement)
        {
            m_MidRangeDetector.RemoveTarget(settlement.gameObject);
        }


        #region State

        public void SwitchState(UnitState state)
        {
            if (state == m_CurrentState) return;

            m_LastState = m_CurrentState;
            m_CurrentState = state;

            m_MidRangeDetector.StateChange(state);
            m_WideRangeDetector.StateChange(state);

            m_MovementHandler.StopFollowingUnit();

            switch (m_CurrentState)
            {
                case UnitState.GO_TO_FLAG:
                    GoToFlag();
                    break;

                case UnitState.SETTLE:
                    m_MovementHandler.RoamToSettle();
                    break;

                case UnitState.BATTLE:
                case UnitState.GATHER:
                    m_MovementHandler.RoamToBattleOrGather();
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
            ToggleHealthBarClientRpc(show, m_MaxHealth, m_CurrentHealth, UnitManager.Instance.UnitColors[(int)m_Team], new ClientRpcParams
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