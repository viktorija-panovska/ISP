using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>TeamSymbol</c> class is a <c>Structure</c> that represents the faction symbol of one of the teams.
    /// </summary>
    public class TeamSymbol : Structure
    {
        private void Start() => m_DestroyMethod = DestroyMethod.NONE;

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (!unit || unit.Team != m_Team || unit.Class == UnitClass.KNIGHT)
                return;

            unit.TeamSymbolReached();

            // if we don't have a leader, set the first unit that reached the symbol to be the leader.
            if (GameController.Instance.HasLeader(m_Team)) return;
            GameController.Instance.SetLeader(unit.gameObject, unit.Team);
        }

        /// <summary>
        /// Sets the position of the team symbol to the given position.
        /// </summary>
        /// <param name="position">The new position of the team symbol.</param>
        //[ClientRpc]
        public void SetSymbolPositionClient/*Rpc*/(Vector3 position) => transform.position = position;
    }
}