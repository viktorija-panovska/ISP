using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Rock</c> class is a <c>Structure</c> representing one of the rocks on the terrain.
    /// </summary>
    public class Rock : Structure
    {
        /// <summary>
        /// The type of rock.
        /// </summary>
        private enum RockType
        {
            /// <summary>
            /// A white rock, which is destroyed by lowering the terrain under it to the water level.
            /// </summary>
            WHITE,
            /// <summary>
            /// A black rock, which is destroyed by any modification of the terrain under it.
            /// </summary>
            BLACK
        }

        [SerializeField] private RockType m_Type;

        private void Start() => m_DestroyMethod = m_Type == RockType.WHITE ? DestroyMethod.DROWN : DestroyMethod.TERRAIN_CHANGE;
    }
}