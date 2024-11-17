using System;
using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Field</c> class is a <c>Structure</c> that represents one tile of fields that exists on the terrain.
    /// </summary>
    public class Field : Structure
    {
        /// <summary>
        /// The <c>GameObject</c> that corresponds to each team's field.
        /// </summary>
        [SerializeField] private GameObject[] m_TeamFields;

        /// <summary>
        /// Gets and sets the team that the field belongs to.
        /// </summary>
        public override Team Team
        {
            get => m_Team;
            set
            {
                m_TeamFields[(int)m_Team].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
                m_Team = value;
                m_TeamFields[(int)m_Team].SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
            }
        }

        /// <summary>
        /// A list of the settlements that this field belongs to.
        /// </summary>
        private readonly HashSet<Settlement> m_SettlementsServed = new();


        private void Start() => m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;

        /// <inheritdoc />
        public override void Cleanup()
        {
            foreach (Settlement settlement in m_SettlementsServed)
                settlement.RemoveField(this);
        }

        /// <summary>
        /// Adds the given settlement to the list of settlements this field belongs to.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be added.</param>
        public void AddSettlementServed(Settlement settlement) => m_SettlementsServed.Add(settlement);

        /// <summary>
        /// Removes the given settlement from the list of settlements this field belongs 
        /// to and removes the field if there are no more settlements that own it.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be removed.</param>
        public void RemoveSettlementServed(Settlement settlement)
        {
            m_SettlementsServed.Remove(settlement);

            if (m_SettlementsServed.Count == 0)
                StructureManager.Instance.DespawnStructure(gameObject);
        }

        /// <summary>
        /// Checks whether the given settlement owns this field.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be checked.</param>
        /// <returns>True if this field serves the given settlement, false otherwise.</returns>
        public bool IsServingSettlement(Settlement settlement) => m_SettlementsServed.Contains(settlement);

        /// <summary>
        /// Makes this field unusable by any settlement.
        /// </summary>
        public void BurnField() => OnTeamChanged(Team.NONE);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="team"></param>
        public void OnTeamChanged(Team team)
        {
            if (team == m_Team) return;
            Team = team;
        }
    }
}