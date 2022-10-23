using UnityEngine;


[RequireComponent(typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer))]
[RequireComponent(typeof(MapGenerator))]
public class TerrainDeformer : MonoBehaviour
{
    private const float radius = 20f;
    private const float deformationStrength = 500f;
    private const float smoothingFactor = 2f;

    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] modifiedVertices;

    // Start is called before the first frame update
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        modifiedVertices = mesh.vertices; 
    }

    // Update is called once per frame
    void Update()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            for (int i = 0; i < modifiedVertices.Length; ++i)
            {
                Vector3 distance = modifiedVertices[i] - hitInfo.point;
                float force = deformationStrength / (1f + hitInfo.point.sqrMagnitude);

                if (distance.sqrMagnitude < radius)
                {
                    if (Input.GetMouseButton(0))
                        modifiedVertices[i] = modifiedVertices[i] + (Vector3.up * force) / smoothingFactor;
                    else if (Input.GetMouseButton(1))
                    {
                        if (modifiedVertices[i].y > 0)
                            modifiedVertices[i] = modifiedVertices[i] + (Vector3.down * force) / smoothingFactor;
                    }
                        
                }
            }
        }

        RecalculateMesh();
    }

    private void RecalculateMesh()
    {
        GetComponent<MeshFilter>().mesh.vertices = modifiedVertices;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        mesh.RecalculateNormals();

        //Texture2D texture = GetComponent<MapGenerator>().GenerateTexture(mesh);
        //GetComponent<MeshRenderer>().sharedMaterial.mainTexture = texture;
    }
}
