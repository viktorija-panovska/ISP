using UnityEngine;


[RequireComponent(typeof(Collider))]
public class CameraObjectDetector : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        if (IsTeamMember(other.gameObject))
            OldPlayerController.Instance.AddObjectInView();
    }


    public void OnTriggerExit(Collider other)
    {
        if (IsTeamMember(other.gameObject))
            OldPlayerController.Instance.RemoveObjectFromView();
    }


    public bool IsTeamMember(GameObject other)
        => (other.layer == LayerMask.NameToLayer("Unit") && other.GetComponent<OldUnit>().Team == OldPlayerController.Instance.Team) ||
           (other.layer == LayerMask.NameToLayer("House") && other.GetComponent<House>().Team == OldPlayerController.Instance.Team);
}
