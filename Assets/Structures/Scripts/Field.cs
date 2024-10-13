using System;
using System.Collections.Generic;


namespace Populous
{
    public class Field : Structure
    {
        private readonly HashSet<Settlement> m_SettlementsServed = new();
        public Action OnFieldDestroyed;


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
    }
}