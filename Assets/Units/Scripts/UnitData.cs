using UnityEngine;


namespace Populous
{
    [CreateAssetMenu(fileName = "Unit", menuName = "Units")]
    public class UnitData : ScriptableObject
    {
        [SerializeField] private SettlementType m_Origin;
        public SettlementType Origin { get => m_Origin; }

        [SerializeField] private int m_MaxHealth;
        public int MaxHealth { get => m_MaxHealth; }

        [SerializeField] private int m_Strength;
        public int Strength { get => m_Strength; }

        [SerializeField] private int m_Speed;
        public int Speed { get => m_Speed; }

        [SerializeField] private int m_ManaGain;
        public int ManaGain { get => m_ManaGain; }
    }
}
