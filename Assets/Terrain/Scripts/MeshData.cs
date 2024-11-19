using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>MeshData</c> struct contains all the required data for constructing a mesh.
    /// </summary>
    public readonly struct MeshData
    {
        private readonly Vector3[] m_Vertices;
        /// <summary>
        /// Contains the positions of the vertices of the mesh.
        /// </summary>
        public readonly Vector3[] Vertices { get => m_Vertices; }

        private readonly int[] m_Triangles;
        /// <summary>
        /// Contains the indices in the vertices array of the vertices of each triangle.
        /// </summary>
        /// <remarks>Each triple of indices represents one triangle. The vertices are entered in order left-center-right.</remarks>
        public readonly int[] Triangles { get => m_Triangles; }


        #region Mesh Construction Properties

        /// <summary>
        /// The number of vertices in a single tile of a normal mesh, where a tile consists of two 
        /// triangles with shared vertices.
        /// </summary>
        public const int VERTICES_PER_TILE_STANDARD = 4;
        /// <summary>
        /// The number of vertices in a single tile of a normal mesh, where a tile consists of four 
        /// triangles with duplicate vertices for each.
        /// </summary>
        public const int VERTICES_PER_TILE_TERRAIN = 12;

        /// <summary>
        /// An array of the unit offsets of the vertices in a tile.
        /// </summary>
        public static readonly (float x, float z)[] VertexOffsets = new (float x, float z)[]
        {
            (0, 0), (1, 0), (1, 1), (0, 1), (0.5f, 0.5f)
        };

        /// <summary>
        /// An array of indices from the <c>VertexOffsets</c> array representing points in a tile, in the order 
        /// in which they need to be inserted in the <c>Triangles</c> array to form a tile of a standard mesh, 
        /// consisting of two triangles with shared vertices.
        /// </summary>
        public static readonly int[] TriangleIndices_Standard = new int[]
        {
            0, 2, 1,
            0, 3, 2
        };

        /// <summary>
        /// An array of indices from the <c>VertexOffsets</c> array representing points in a tile, in the order 
        /// in which they need to be inserted in the <c>Triangles</c> array to form a tile of the terrain, consisting
        /// of four triangles with duplicate vertices for each.
        /// </summary>
        public static readonly int[] TriangleIndices_Terrain = new int[]
        {
            0, 4, 1,    // bottom left, center, bottom right
            1, 4, 2,    // bottom right, center, top right
            2, 4, 3,    // top right, center, top left
            3, 4, 0     // top left, center, bottom left
        };

        /// <summary>
        /// A list of arrays each representing a point in the tile which contain the index offset
        /// for the mesh vertex array for each vertex which occupies that point.
        /// </summary>
        public static readonly List<int>[] SharedVertexOffsets = new List<int>[5]
        {
            new() { 0, 11 },        // bottom left
            new() { 2, 3  },        // bottom right
            new() { 8, 9 },         // top left
            new() { 5, 6 },         // top right
            new() { 1, 4, 7, 10 }   // center
        };

        #endregion


        /// <summary>
        /// The constructor for the <c>MeshData</c> struct.
        /// </summary>
        /// <param name="width">How many vertices wide should the mesh be.</param>
        /// <param name="height">How many vertices high should the mesh be.</param>
        public MeshData(int width, int height, bool isTerrain)
        {
            if (isTerrain)
            {
                m_Vertices = new Vector3[width * height * VERTICES_PER_TILE_TERRAIN];
                m_Triangles = new int[width * height * TriangleIndices_Terrain.Length];
            }
            else
            {
                m_Vertices = new Vector3[width * height * VERTICES_PER_TILE_STANDARD];
                m_Triangles = new int[width * height * TriangleIndices_Standard.Length];
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
                vertices = Vertices,
                triangles = Triangles,
            };

            mesh.RecalculateNormals();
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;

            MeshCollider collider = gameObject.GetComponent<MeshCollider>();
            if (collider) collider.sharedMesh = mesh;
        }
    }
}