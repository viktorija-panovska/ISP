using UnityEngine;

namespace Populous
{
    public static class GameUtils
    {
        public static void ResizeGameObject(GameObject gameObject, float newSize, bool scaleY = false)
        {
            Vector3 size = gameObject.GetComponent<Renderer>().bounds.size;
            Vector3 scale = gameObject.transform.localScale;
            float newX = newSize * scale.x / size.x;
            float newZ = newSize * scale.z / size.z;
            float min = Mathf.Min(newX, newZ);

            if (scaleY) scale.y = (min / Mathf.Min(scale.x, scale.z)) * scale.y;

            scale.x = min;
            scale.z = min;
            gameObject.transform.localScale = scale;
        }
    }
}