using UnityEngine;

public class Swamp : MonoBehaviour
{

    public void Start()
    {
        Debug.Log(LayerMask.NameToLayer("Unit"));
    }


    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            Unit unit = other.gameObject.GetComponent<Unit>();
            unit.KillUnit();
        }
    }
}
