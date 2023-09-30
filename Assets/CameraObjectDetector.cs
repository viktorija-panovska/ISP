using UnityEngine;


[RequireComponent(typeof(Collider))]
public class CameraObjectDetector : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        if (IsTeamMember(other.gameObject))
            PlayerController.Instance.AddObjectInView();
    }


    public void OnTriggerExit(Collider other)
    {
        if (IsTeamMember(other.gameObject))
            PlayerController.Instance.RemoveObjectFromView();
    }


    public bool IsTeamMember(GameObject other)
        => (other.layer == LayerMask.NameToLayer("Unit") && other.GetComponent<Unit>().Team == PlayerController.Instance.Team) ||
           (other.layer == LayerMask.NameToLayer("House") && other.GetComponent<House>().Team == PlayerController.Instance.Team);
}
