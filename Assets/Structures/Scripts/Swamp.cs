using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Swamp</c> class is a <c>Structure</c> that represents one tile of swamp placed on the terrain.
    /// </summary>
    public class Swamp : Structure
    {
        private void Start() => m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerData.RedTeamLayer || other.gameObject.layer == LayerData.BlueTeamLayer)
                Debug.Log("Unit Entered Swamp");
        }
    }
}