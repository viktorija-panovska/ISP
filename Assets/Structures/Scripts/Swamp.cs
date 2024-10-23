using UnityEngine;

namespace Populous
{
    public class Swamp : Structure
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Red Team") || other.gameObject.layer == LayerMask.NameToLayer("Blue Team"))
                Debug.Log("Unit Entered Swamp");
        }
    }
}