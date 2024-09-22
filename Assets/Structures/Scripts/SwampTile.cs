using UnityEngine;


public class SwampTile : Structure
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("RedUnit") || other.gameObject.layer == LayerMask.NameToLayer("BlueUnit"))
            Debug.Log("Unit Entered Swamp");
    }
}
