using UnityEngine;

public class CameraController : MonoBehaviour
{
    private const float panSpeed = 20f;
    private readonly Vector2 panLimit = new Vector2(200, 200);

    private const float scrollSpeed = 100f;
    private const float minY = 100f;
    private const float maxX = 400f;
 

    void Update()
    {
        Vector3 position = transform.position;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            position.z -= panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            position.z += panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            position.x += panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            position.x -= panSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        position.y -= scroll * scrollSpeed * 100f * Time.deltaTime;

        position.x = Mathf.Clamp(position.x, -panLimit.x, panLimit.x);
        position.y = Mathf.Clamp(position.y, minY, maxX);
        position.z = Mathf.Clamp(position.z, -panLimit.y, panLimit.y);

        transform.position = position;
    }
}
