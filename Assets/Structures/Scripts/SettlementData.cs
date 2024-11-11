using UnityEngine;

namespace Populous
{
    public enum SettlementType
    {
        TENT,
        MUD_HUT,
        STRAW_HUT,
        ROCK_HUT,
        WOOD_HOUSE,
        STONE_HOUSE,
        NICE_HOUSE,
        ROUND_TOWER,
        SQUARE_TOWER,
        CITY
    }


    [CreateAssetMenu(fileName = "Settlement", menuName = "Settlements")]
    public class SettlementData : ScriptableObject
    {
        [SerializeField] private SettlementType m_Type;
        public SettlementType Type { get => m_Type; }

        [SerializeField] private int m_UnitStrength;
        public int UnitStrength { get => m_UnitStrength; }

        [SerializeField] private int m_FollowerCapacity;
        public int FollowerCapacity { get => m_FollowerCapacity; }

        [SerializeField] private int m_MaxHealth;
        public int MaxHealth { get => m_MaxHealth; }

        [SerializeField] private int m_UnitReleaseWait;
        public int UnitReleaseWait { get => m_UnitReleaseWait; }
    }
}