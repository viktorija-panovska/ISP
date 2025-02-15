using UnityEngine.EventSystems;
using UnityEngine;

namespace Populous
{
    public interface IInspectableObject : IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public GameObject GameObject { get; }
        public bool IsInspected { get; set; }
        public void SetHighlight(bool isHighlightOn);
    }
}