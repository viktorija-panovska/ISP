using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    public enum UnitState
    {
        SETTLE,
        BATTLE
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


        #region Health Bar

        public void OnPointerEnter(PointerEventData eventData)
        {
            ToggleHealthBarServerRpc(show: true);
        }

        public void OnPointerExit(PointerEventData eventData)
            => ToggleHealthBarServerRpc(show: false);

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