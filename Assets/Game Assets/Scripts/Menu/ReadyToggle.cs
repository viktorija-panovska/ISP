using UnityEngine;
using UnityEngine.EventSystems;


public class ReadyToggle : MonoBehaviour, IPointerDownHandler
{
    public EventTrigger.TriggerEvent onPointerDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        onPointerDown.Invoke(eventData);
    }
}
