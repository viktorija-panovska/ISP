using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The type of a settlement.
    /// </summary>
    public enum SettlementType
    {
        /// <summary>
        /// A tent, a settlement with no tiles of fields and with rocks in the vicinity.
        /// </summary>
        TENT,
        /// <summary>
        /// A mud hut, a settlement with 0-1 tiles of fields.
        /// </summary>
        MUD_HUT,
        /// <summary>
        /// A straw hut, a settlement with 2-3 tiles of fields.
        /// </summary>
        STRAW_HUT,
        /// <summary>
        /// A rock hut, a settlement with 4-5 tiles of fields.
        /// </summary>
        ROCK_HUT,
        /// <summary>
        /// A wood house, a settlement with 6-7 tiles of fields.
        /// </summary>
        WOOD_HOUSE,
        /// <summary>
        /// A stone house, a settlement with 8-9 tiles of fields.
        /// </summary>
        STONE_HOUSE,
        /// <summary>
        /// A nice house, a settlement with 10-11 tiles of fields.
        /// </summary>
        NICE_HOUSE,
        /// <summary>
        /// A round tower, a settlement with 12-13 tiles of fields.
        /// </summary>
        ROUND_TOWER,
        /// <summary>
        /// A square tower, a settlement with 14-15 tiles of fields.
        /// </summary>
        SQUARE_TOWER,
        /// <summary>
        /// A city, a settlement with 16-25 tiles of fields.
        /// </summary>
        CITY
    }


    /// <summary>
    /// The <c>SettlementData</c> class is a <c>ScriptableObject</c> which defines the required data for one type of settlement.
    /// </summary>
    [CreateAssetMenu(fileName = "Settlement", menuName = "Settlements")]
    public class SettlementData : ScriptableObject
    {
        [SerializeField] private SettlementType m_Type;
        /// <summary>
        /// The type of the settlement this data belongs to.
        /// </summary>
        public SettlementType Type { get => m_Type; }

        [SerializeField] private int m_Capacity;
        /// <summary>
        /// The number of followers the settlement can hold.
        /// </summary>
        /// <remarks>More advanced settlements have a greater capacity.</remarks>
        public int Capacity { get => m_Capacity; }

        [SerializeField] private int m_FillRate;
        /// <summary>
        /// The number of seconds after which the settlement gains a new follower.
        /// </summary>
        /// <remarks>More advanced settlements have a faster fill rate.</remarks>
        public int FillRate { get => m_FillRate; }

        [SerializeField] private int m_UnitStrength;
        /// <summary>
        /// The strength of a unit produced by this settlement.
        /// </summary>
        /// <remarks>More advanced settlements produce stronger units.</remarks>
        public int ReleasedUnitStrength { get => m_UnitStrength; }
    }
}