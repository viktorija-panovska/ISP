using Unity.Netcode;

namespace Populous
{
    /// <summary>
    /// The <c>ObjectActivator</c> class is a <c>NetworkBehavior</c> which is applied to <c>GameObject</c>s for 
    /// easy activation/deactivation of said <c>GameObject</c> across the network.
    /// </summary>
    public class ObjectActivator : NetworkBehaviour
    {
        /// <summary>
        /// Activates/deactivates the <c>GameObject</c>.
        /// </summary>
        /// <param name="active">True if the <c>GameObject</c> should be active, false otherwise.</param>
        [ClientRpc]
        public void SetActiveClientRpc(bool active) => gameObject.SetActive(active);
    }
}