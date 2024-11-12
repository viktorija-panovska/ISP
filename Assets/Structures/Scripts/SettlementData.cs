using UnityEngine;

namespace Populous
{
    public enum SettlementType
    {
        /// <summary>
        /// A tent, a settlement with 0 fields.
        /// </summary>
        TENT,
        /// <summary>
        /// A mud hut, a settlement with - fields.
        /// </summary>
        MUD_HUT,
        /// <summary>
        /// A straw hut, a settlement with - fields.
        /// </summary>
        STRAW_HUT,
        /// <summary>
        /// A rock hut, a settlement with - fields.
        /// </summary>
        ROCK_HUT,
        /// <summary>
        /// A wood house, a settlement with - fields.
        /// </summary>
        WOOD_HOUSE,
        /// <summary>
        /// A stone house, a settlement with - fields.
        /// </summary>
        STONE_HOUSE,
        /// <summary>
        /// A nice house, a settlement with - fields.
        /// </summary>
        NICE_HOUSE,
        /// <summary>
        /// A round tower, a settlement with - fields.
        /// </summary>
        ROUND_TOWER,
        /// <summary>
        /// A square tower, a settlement with - fields.
        /// </summary>
        SQUARE_TOWER,
        /// <summary>
        /// A city, a settlement with - fields.
        /// </summary>
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