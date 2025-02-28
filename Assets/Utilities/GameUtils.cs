using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>GameUtils</c> class contains utility methods which are useful for gameplay or for miscellaneous coding purposes.
    /// </summary>
    public static class GameUtils
    {
        /// <summary>
        /// Changes the scale of the given <c>GameObject</c> to match the given size.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be scaled.</param>
        /// <param name="newSize">The final size of the X and Z axis of the object.</param>
        /// <param name="scaleY">True if the object should also be scaled on the Y axis, false otherwise.</param>
        public static void ResizeGameObject(GameObject gameObject, float newSize, bool scaleY = false)
        {
            Vector3 size = gameObject.GetComponent<Renderer>().bounds.size;
            Vector3 scale = gameObject.transform.localScale;
            float newX = newSize * scale.x / size.x;
            float newZ = newSize * scale.z / size.z;
            float min = Mathf.Min(newX, newZ);

            if (scaleY) 
                scale.y = (min / Mathf.Min(scale.x, scale.z)) * scale.y;

            scale.x = min;
            scale.z = min;
            gameObject.transform.localScale = scale;
        }

        /// <summary>
        /// Gets the next array index, looping around when the end is reached.
        /// </summary>
        /// <param name="start">The start index.</param>
        /// <param name="increment">The amount to increment by.</param>
        /// <param name="arrayLength">The length of the array.</param>
        /// <returns></returns>
        public static int GetNextArrayIndex(int start, int increment, int arrayLength)
            => (start + increment + arrayLength) % arrayLength;


        public static ClientRpcParams GetClientParams(ulong clientId)
            => new()
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
    }
}