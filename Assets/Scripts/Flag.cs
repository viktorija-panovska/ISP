using UnityEngine;

public class Flag : MonoBehaviour
{
    public Teams Team;
    public NotifyRemoveFlag RemoveFlag;


    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("HI");
        //if (other.gameObject.layer == LayerMask.NameToLayer("unit") && other.GetComponent<Unit>().Team == Team)
        //    RemoveFlag?.Invoke();
    }
}
