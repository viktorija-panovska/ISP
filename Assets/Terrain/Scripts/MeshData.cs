using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>MeshData</c> struct contains all the required data for the construction of a mesh and the data of one such mesh.
    /// </summary>
    public readonly struct MeshData
    {
        /// <summary>
        /// Contains the positions of the vertices of the mesh.
        /// </summary>
        private readonly Vector3[] m_Vertices;
        /// <summary>
        /// Contains the indices in the vertices array of the vertices of each triangle.
        /// </summary>
        private readonly int[] m_Triangles;



        /// <summary>
        /// The constructor for the <c>MeshData</c> struct.
        /// </summary>
        /// <param name="width">How many vertices wide should the mesh be.</param>
        /// <param name="height">How many vertices high should the mesh be.</param>
        /// <param name="isTerrain">True if the mesh is intended for a terrain and should be made up of 
        /// four triangles per tile with duplicate vertices, false otherwise.</param>
        public MeshData(int width, int height, bool isTerrain)
        {
            if (isTerrain)
            {
                m_Vertices = new Vector3[width * height * MeshProperties.VERTICES_PER_TILE_TERRAIN];
                m_Triangles = new int[width * height * MeshProperties.TriangleIndices_Terrain.Length];
            }
            else
            {
                m_Vertices = new Vector3[width * height * MeshProperties.VERTICES_PER_TILE_STANDARD];
                m_Triangles = new int[width * height * MeshProperties.TriangleIndices_Standard.Length];
            }
        }

        /// <summary>
        /// Adds a new vertex at the given index in the vertices array.
        /// </summary>
        /// <param name="index">The index in the vertices array at which the new vertex should be added.</param>
        /// <param name="vertex">A <c>Vector3</c> representing the vertex.</param>
        public readonly void AddVertex(int index, Vector3 vertex) => m_Vertices[index] = vertex;

        /// <summary>
        /// Adds a new triangle starting at the given index in the triangles array.
        /// </summary>
        /// <param name="index">The index in the triangles array at which the first vertex index should be added.</param>
        /// <param name="a">The index of the first vertex in the triangle.</param>
        /// <param name="b">The index of the second vertex in the triangle.</param>
        /// <param name="c">The index of the third vertex in the triangle.</param>
        public readonly void AddTriangle(int index, int a, int b, int c)
        {
            m_Triangles[index] = a;
            m_Triangles[index + 1] = b;
            m_Triangles[index + 2] = c;
        }

        /// <summary>
        /// Creates a new mesh from the current <c>MeshData</c> and applies it to the given object.
        /// </summary>
        /// <param name="gameObject">The object the mesh should be applied to.</param>
        /// <param name="material">The material to be applied to the mesh.</param>
        public void SetMesh(GameObject gameObject, Material material)
        {
            Mesh mesh = new()
            {
                vertices = m_Vertices,
                triangles = m_Triangles,
            };

            mesh.RecalculateNormals();
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;

            MeshCollider collider = gameObject.GetComponent<MeshCollider>();
            if (collider) collider.sharedMesh = mesh;
        }



        /// <summary>
        /// Sets the height of the vertex at the given index to the given height.
        /// </summary>
        /// <param name="index">The index of the vertex in the mesh whose height should be set.</param>
        /// <param name="height">The height that should be set.</param>
        public readonly void SetVertexHeight(int index, float height) => m_Vertices[index].y = height;

        /// <summary>
        /// Sets the vertex at the given index to the given position.
        /// </summary>
        /// <param name="index">The index of the vertex in the mesh whose position should be set..</param>
        /// <param name="position">The new position of the vertex.</param>
        public readonly void SetVertexPosition(int index, Vector3 position) => m_Vertices[index] = position;

        /// <summary>
        /// Gets the position of the vertex at the given index.
        /// </summary>
        /// <param name="index">The index of the vertex in the mesh whose position should be returned..</param>
        /// <returns>The <c>Vector3</c> representing the position of the vertex in the world.</returns>
        public readonly Vector3 GetVertexPosition(int index) => m_Vertices[index];
    }
}