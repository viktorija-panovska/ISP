using System;
using System.Collections.Generic;
using UnityEngine;


namespace Populous
{
    public class Field : Structure
    {
        [SerializeField] private GameObject[] m_FieldTypes;

        private readonly HashSet<Settlement> m_SettlementsServed = new();
        public Action OnFieldDestroyed;


        public new Team Team
        {
            get => m_Team;
            set
            {
                m_FieldTypes[(int)m_Team].SetActive(false);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);

                m_Team = value;
                m_FieldTypes[(int)m_Team].SetActive(true);//.GetComponent<ObjectActivator>().SetActiveClientRpc(false);
            }
        }


        public override void Cleanup()
        {
            foreach (Settlement settlement in m_SettlementsServed)
                settlement.RemoveField(this);

            OnFieldDestroyed = null;
        }


        public void AddSettlementServed(Settlement settlement) => m_SettlementsServed.Add(settlement);

        public void OnSettlementRemoved(Settlement settlement)
        {
            m_SettlementsServed.Remove(settlement);

            if (m_SettlementsServed.Count == 0)
                StructureManager.Instance.DespawnStructure(gameObject);
        }

        public bool IsServingSettlement(Settlement settlement) => m_SettlementsServed.Contains(settlement);

        public void RuinField()
        {
            Team = Team.NONE;
        }
    }
}