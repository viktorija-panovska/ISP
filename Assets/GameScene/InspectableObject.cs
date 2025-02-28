using UnityEngine.EventSystems;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>IInspectableObject</c> interface defines methods necessary for classes representing objects that can be inspected with the Query action.
    /// </summary>
    public interface IInspectableObject : IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>
        /// The GameObject associated with the class.
        /// </summary>
        public GameObject GameObject { get; }

        /// <summary>
        /// True if the object is currently being inspected by either player, false otherwise.
        /// </summary>
        public bool IsInspected { get; set; }

        /// <summary>
        /// Activates or deactivates the highlight of the object.
        /// </summary>
        /// <param name="shouldActivate">True if the highlight should be activated, false otherwise.</param>
        public void SetHighlight(bool shouldActivate);
    }
}