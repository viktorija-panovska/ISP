using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Swamp</c> class is a <c>Structure</c> that represents one tile of swamp on the terrain.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Swamp : Structure
    {
        private void Start()
        {
            m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;
            GameUtils.ResizeGameObject(gameObject, Terrain.Instance.UnitsPerTileSide);
            GetComponent<Collider>().enabled = IsHost;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<Unit>()) return;

            UnitManager.Instance.DespawnUnit(other.gameObject, hasDied: true);
        }
    }
}