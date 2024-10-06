using Unity.Netcode;

namespace Populous
{
    public class ObjectActivator : NetworkBehaviour
    {
        [ClientRpc]
        public void SetActiveClientRpc(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}