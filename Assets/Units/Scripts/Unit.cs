using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Populous
{
    public enum UnitState
    {
        SETTLE,
        BATTLE,
        GO_TO_FLAG
    }


    public class Unit : NetworkBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UnitData[] m_UnitData;
        [SerializeField] private Color[] m_UnitColors;

        private Team m_Team;
        public Team Team { get => m_Team; set => m_Team = value; }

        private int m_CurrentUnitIndex;
        private UnitData m_CurrentUnitData;

        private int m_Health;

        private void Start()
        {
            m_CurrentUnitData = m_UnitData[m_CurrentUnitIndex];
        }


        public void RecalculateHeight()
        {
            Debug.Log("Recalculate");

            //float height;

            //int startHeight = WorldMap.Instance.GetHeight(MovementHandler.StartLocation);
            //int endHeight = WorldMap.Instance.GetHeight(MovementHandler.EndLocation);

            //if (startHeight == endHeight)
            //    height = startHeight;
            //else
            //{
            //    float heightDifference = Mathf.Abs(endHeight - startHeight);
            //    float totalDistance = new Vector2(MovementHandler.EndLocation.X - MovementHandler.StartLocation.X, MovementHandler.EndLocation.Z - MovementHandler.StartLocation.Z).magnitude;

            //    float distance = startHeight < endHeight
            //        ? new Vector2(Position.x - MovementHandler.StartLocation.X, Position.z - MovementHandler.StartLocation.Z).magnitude
            //        : new Vector2(MovementHandler.EndLocation.X - Position.x, MovementHandler.EndLocation.Z - Position.z).magnitude;

            //    height = heightDifference * distance / totalDistance;
            //    height = startHeight < endHeight ? startHeight + height : endHeight + height;
            //}

            //if (height <= OldGameController.Instance.WaterLevel.Value)
            //    KillUnit();
            //else
            //    Position = new Vector3(Position.x, height, Position.z);
        }

        public void SwitchState(UnitState state)
        {
            
        }


        #region Health Bar

        public void OnPointerEnter(PointerEventData eventData)
        {
            GameUI.Instance.ToggleHealthBar(
                true,
                m_CurrentUnitData.MaxHealth,
                m_Health,
                m_UnitColors[(int)m_Team],
                transform.position + Vector3.up * GetComponent<Renderer>().bounds.size.y
            );
            //ToggleHealthBarServerRpc(show: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            GameUI.Instance.ToggleHealthBar(
                false,
                m_CurrentUnitData.MaxHealth,
                m_Health,
                m_UnitColors[(int)m_Team],
                transform.position + Vector3.up * GetComponent<Renderer>().bounds.size.y
            );
        }
            //=> ToggleHealthBarServerRpc(show: false);

        [ServerRpc(RequireOwnership = false)]
        public void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
        {
            ToggleHealthBarClientRpc(show, m_CurrentUnitData.MaxHealth, m_Health, m_UnitColors[(int)m_Team], new ClientRpcParams
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