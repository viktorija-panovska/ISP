using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainChunk</c> class represents one chunk of the terrain and handles its creation and modification.
    /// </summary>
    public class TerrainChunk
    {
        /// <summary>
        /// A container for the data of this chunk's mesh i.e. the vertices and the triangles that make it up.
        /// </summary>
        private readonly MeshData m_MeshData;

        /// <summary>
        /// The GameObject representing this terrain chunk.
        /// </summary>
        private readonly GameObject m_ChunkObject;

        private (int x, int z) m_ChunkIndex;
        /// <summary>
        /// Gets the index of the chunk in the terrain chunk grid.
        /// </summary>
        public (int X, int Z) ChunkIndex { get => m_ChunkIndex; }

        /// <summary>
        /// Gets the <c>Vector3</c> position of the chunk in the world.
        /// </summary>
        public Vector3 ChunkPosition { get => m_ChunkObject.transform.position; }

        /// <summary>
        /// The directions of the neighbors to check when computing the height of a vertex.
        /// </summary>
        private static readonly (int, int)[] m_HeightCheckDirections = new (int, int)[] 
        { 
            (-1, -1), (-1, 0), (-1, 1), (0, -1), (1, -1) 
        };


        #region Setup Chunk

        /// <summary>
        /// The constructor of the <c>TerrainChunk</c> class, which creates a terrain chunk GameObject.
        /// </summary>
        /// <param name="gridX">The X coordinate of the chunk on the terrain chunk grid.</param>
        /// <param name="gridZ">The Z coordinate of the chunk on the terrain chunk grid.</param>
        /// <param name="parent">The transform the created terrain chunk GameObject should be parented to.</param>
        public TerrainChunk(int gridX, int gridZ, Transform parent)
        {
            m_ChunkObject = new();
            m_ChunkObject.AddComponent<MeshFilter>();
            m_ChunkObject.AddComponent<MeshRenderer>();
            m_ChunkObject.AddComponent<MeshCollider>();
            m_ChunkObject.AddComponent<Rigidbody>();

            m_ChunkObject.name = $"Chunk ({gridX}, {gridZ})";
            m_ChunkObject.layer = LayerData.TerrainLayer;
            m_ChunkObject.GetComponent<MeshRenderer>().enabled = false;

            Transform transform = m_ChunkObject.transform;
            transform.position = new Vector3(
                gridX * Terrain.Instance.UnitsPerChunkSide, 
                0, 
                gridZ * Terrain.Instance.UnitsPerChunkSide
            );
            transform.parent = parent;

            Rigidbody rigidbody = m_ChunkObject.GetComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;

            m_ChunkIndex = (gridX, gridZ);
            m_MeshData = ConstructTerrainGrid();
            //SetVertexHeights();
            SetMesh();
        }

        /// <summary>
        /// Triggers the application of the current <c>MeshData</c> to the terrain chunk object.
        /// </summary>
        public void SetMesh() => m_MeshData.SetMesh(m_ChunkObject, Terrain.Instance.TerrainMaterial);

        #endregion


        #region Generate Terrain Chunk

        /// <summary>
        /// Places the vertices and triangles to create a new mesh for the terrain chunk.
        /// </summary>
        /// <returns>An instance of the <c>MeshData</c> struct containing the data of the generated mesh.</returns>
        private MeshData ConstructTerrainGrid()
        {
            MeshData meshData = new(Terrain.Instance.TilesPerChunkSide, Terrain.Instance.TilesPerChunkSide, isTerrain: true);
            int vertexIndex = 0;
            int triangleIndex = 0;

            // Generating the mesh tile by tile
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    for (int i = 0; i < MeshProperties.TriangleIndices_Terrain.Length; ++i)
                    {
                        int index = MeshProperties.TriangleIndices_Terrain[i];

                        Vector3 vertex = new(
                            (x + MeshProperties.VertexOffsets[index].x) * Terrain.Instance.UnitsPerTileSide,
                            60,
                            (z + MeshProperties.VertexOffsets[index].z) * Terrain.Instance.UnitsPerTileSide
                        );
                        meshData.AddVertex(vertexIndex + i, vertex);

                        // After every third vertex, add a triangle
                        if ((i + 1) % 3 == 0)
                        {
                            meshData.AddTriangle(triangleIndex, vertexIndex + i - 2, vertexIndex + i - 1, vertexIndex + i);
                            triangleIndex += 3;
                        }
                    }
                    vertexIndex += MeshProperties.TriangleIndices_Terrain.Length;
                }
            }

            return meshData;
        }

        /// <summary>
        /// Sets the heights of each point in the terrain chunk.
        /// </summary>
        private void SetVertexHeights()
        {
            // first column should have the same heights as the last column in the chunk to the left
            if (ChunkIndex.X > 0)
            {
                for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
                {
                    SetMeshHeightAtPoint(
                        pointInChunk: (0, z),
                        height: Terrain.Instance.GetChunkByIndex((ChunkIndex.X - 1, ChunkIndex.Z)).GetPointHeight((Terrain.Instance.TilesPerChunkSide, z))
                    );
                }
            }

            // bottom row should have the same heights as the top row in the chunk below
            if (ChunkIndex.Z > 0)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    SetMeshHeightAtPoint(
                        pointInChunk: (x, 0),
                        height: Terrain.Instance.GetChunkByIndex((ChunkIndex.X, ChunkIndex.Z - 1)).GetPointHeight((x, Terrain.Instance.TilesPerChunkSide))
                    );
                }
            }

            for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    if ((x == 0 && ChunkIndex.X > 0) || (z == 0 && ChunkIndex.Z > 0)) continue;

                    int height = CalculateVertexHeight((x, z));
                    SetMeshHeightAtPoint((x, z), height);
                }
            }

            RecalculateAllTileCenterHeights();
        }

        /// <summary>
        /// Generates and computes the height of the terrain at the given point.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose height should be computed.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        private int CalculateVertexHeight((int x, int z) pointInChunk)
        {
            int vertexIndex = GetPointVertexIndex(pointInChunk);

            // the noise generator essentially returns a percentage of the maximum height the height of the current point will be
            float noiseHeight = Terrain.Instance.NoiseGenerator.GetNoiseAtPosition(
                ChunkPosition + m_MeshData.GetVertexPosition(vertexIndex)
            ) * Terrain.Instance.MaxHeight;
            // make sure the height of the is a multiple of the step height.
            int height = Mathf.FloorToInt(noiseHeight / Terrain.Instance.StepHeight) * Terrain.Instance.StepHeight;

            // Vertex (0, 0) in chunk (0, 0) is the very first vertex, so it has nothing to coordinate with
            if (ChunkIndex == (0, 0) && pointInChunk == (0, 0))
                return height;

            List<int> neighborHeights = new();

            foreach ((int dx, int dz) in m_HeightCheckDirections)
            {
                (int x, int z) neighborInChunk = (pointInChunk.x + dx, pointInChunk.z + dz);

                // if we are looking at the point above the current point and we are in the first chunk
                // (that doesn't copy the heights of the first column from the previous chunk) or we're in any
                // other column than the first one, then the point above won't have been assigned a height yet.
                if (!IsPointInChunk(neighborInChunk) || (dz == 1 && (ChunkIndex.X == 0 || neighborInChunk.x > 0)))
                    continue;

                neighborHeights.Add(GetMeshHeightAtPoint(neighborInChunk));
            }

            // if all the neighbors are at the same height, we can go one step up or one step down in height.
            bool EqualToFirst(int x) => x == neighborHeights[0];
            if (neighborHeights.TrueForAll(EqualToFirst))
            {
                height = Mathf.Clamp(height,
                    Mathf.Max(0, neighborHeights[0] - Terrain.Instance.StepHeight),
                    Mathf.Min(neighborHeights[0] + Terrain.Instance.StepHeight, Terrain.Instance.MaxHeight)
                );

                return height;
            }

            int minHeight = neighborHeights.Min();
            int maxHeight = neighborHeights.Max();

            // minimum height and maximum height can have at most 2 step heights between them
            if (Mathf.Abs(minHeight - maxHeight) > Terrain.Instance.StepHeight)
                height = minHeight + Terrain.Instance.StepHeight;
            else
                height = Mathf.Clamp(height, minHeight, maxHeight);

            return height;
        }

        #endregion


        #region Modify Terrain Chunk

        /// <summary>
        /// Increases or decreases the height of the given terrain point and triggers 
        /// the propagation of the height modification to its neighbors, if needed.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be modified.</param>
        /// <param name="lower">True if the height should be decreased, false if the height should be increased.</param>
        public void ChangeHeight(TerrainPoint point, bool lower)
        {
            (int x, int z) pointInChunk = GetChunkCoordinates(point.X, point.Z);

            int prevHeight = GetPointHeight(point);
            int newHeight = prevHeight + (lower ? -1 : 1) * Terrain.Instance.StepHeight;

            if (newHeight < 0 || newHeight > Terrain.Instance.MaxHeight)
                return;

            SetMeshHeightAtPoint(pointInChunk, newHeight);

            // Update neighboring vertices
            foreach (TerrainPoint neighbor in point.GetAllNeighbors())
            {
                (int x, int z) neighborInChunk = GetChunkCoordinates(neighbor.X, neighbor.Z);

                if (!IsPointInChunk(neighborInChunk) || Mathf.Abs(newHeight - GetPointHeight(neighborInChunk)) <= Terrain.Instance.StepHeight)
                    continue;

                Terrain.Instance.ChangePointHeight(neighbor, lower);
            }

            RecalculateSurroundingTileCenterHeights(pointInChunk);
        }

        /// <summary>
        /// Recalculates the heights of the centers of the tiles surrounding the given point in the chunk.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose surrounding centers should be recomputed.</param>
        private void RecalculateSurroundingTileCenterHeights((int x, int z) pointInChunk)
        {
            for (int z = -1; z <= 0; ++z)
            {
                for (int x = -1; x <= 0; ++x)
                {
                    (int x, int z) tile = (pointInChunk.x + x, pointInChunk.z + z);

                    if (tile.x < 0 || tile.x >= Terrain.Instance.TilesPerChunkSide ||
                        tile.z < 0 || tile.z >= Terrain.Instance.TilesPerChunkSide)
                        continue;

                    float prevHeight = GetTileCenterHeight(tile);
                    float newHeight = CalculateTileCenterHeight(tile);

                    if (prevHeight == newHeight) continue;

                    SetTileCenterHeight(tile, newHeight);
                }
            }
        }

        /// <summary>
        /// Sets the height of the given <c>TerrainPoint</c>.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be set.</param>
        /// <param name="height">The value the height the <c>TerrainPoint</c> should be set to.</param>
        public void SetVertexHeight(TerrainPoint point, int height) 
            => SetMeshHeightAtPoint(GetChunkCoordinates(point.X, point.Z), height);

        /// <summary>
        /// Updates the heights of all the points that are under the water level to be at the water level.
        /// </summary>
        public void UpdatePointsUnderwater()
        {
            for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    if (GetPointHeight((x, z)) < Terrain.Instance.WaterLevel)
                        SetMeshHeightAtPoint((x, z), Terrain.Instance.WaterLevel);

                    if (x == Terrain.Instance.TilesPerChunkSide || z == Terrain.Instance.TilesPerChunkSide)
                        continue;

                    if (GetTileCenterHeight((x, z)) < Terrain.Instance.WaterLevel)
                        SetTileCenterHeight((x, z), Terrain.Instance.WaterLevel);
                }
            }
        }

        #endregion


        #region Terrain Point

        /// <summary>
        /// Checks whether the point is inside the bounds of the chunk.
        /// </summary>
        /// <param name="pointInChunk">The coordinates of the point relative to the chunk.</param>
        /// <returns>True if the point is in the bounds of the chunk, false otherwise.</returns>
        private bool IsPointInChunk((int x, int z) pointInChunk)
            => pointInChunk.x >= 0 && pointInChunk.x <= Terrain.Instance.TilesPerChunkSide &&
               pointInChunk.z >= 0 && pointInChunk.z <= Terrain.Instance.TilesPerChunkSide;

        /// <summary>
        /// Gets the height of the given point.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be returned.</param>
        /// <returns>The height of the given point.</returns>
        public int GetPointHeight(TerrainPoint point) => GetPointHeight(GetChunkCoordinates(point.X, point.Z));
        /// <summary>
        /// Gets the height of the given point.
        /// </summary>
        /// <param name="point">The coordinates of the point relative to the chunk whose height should be returned.</param>
        /// <returns>The height at the given point.</returns>
        public int GetPointHeight((int x, int z) pointInChunk) => GetMeshHeightAtPoint(pointInChunk);

        /// <summary>
        /// Gets the height of the mesh at the given point.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose height should be returned.</param>
        /// <returns>The height at the given point.</returns>
        private int GetMeshHeightAtPoint((int x, int z) pointInChunk) 
            => (int)m_MeshData.GetVertexPosition(GetPointVertexIndex(pointInChunk)).y;
        
        /// <summary>
        /// Sets the height of all the vertices at the given point to the given height.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose height should be returned.</param>
        /// <param name="height">The height the point should be set to.</param>
        private void SetMeshHeightAtPoint((int x, int z) pointInChunk, int height)
        {
            int vertexIndex = 0;

            // goes over all the tiles that contain the point to find the vertices that share the point
            // the current tile is (0,0) and the vertex index is 0, which means the bottom-left vertex
            // the tile to the left is (0, -1) and the vertex index is 1, which means the bottom-right vertex
            // the tile below is (-1, 0) and the vertex index is 2, which means the top-left vertex
            // and the last tile is (-1, -1) and the vertex index is 3, which means the top-right vertex
            for (int z = 0; z >= -1; --z)
            {
                for (int x = 0; x >= -1; --x)
                {
                    if (pointInChunk.z + z < 0 || pointInChunk.z + z >= Terrain.Instance.TilesPerChunkSide ||
                        pointInChunk.x + x < 0 || pointInChunk.x + x >= Terrain.Instance.TilesPerChunkSide)
                    {
                        vertexIndex++;
                        continue;
                    }

                    int tileIndex = (pointInChunk.z + z) * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x + x);

                    for (int i = 0; i < MeshProperties.SharedVertexOffsets[vertexIndex].Length; ++i)
                    {
                        int index = tileIndex * MeshProperties.TriangleIndices_Terrain.Length + MeshProperties.SharedVertexOffsets[vertexIndex][i];
                        if (index < 0) continue;
                        m_MeshData.SetVertexHeight(index, height);
                    }

                    vertexIndex++;
                }
            }
        }

        /// <summary>
        /// Gets a vertex in the mesh that corresponds to the given point.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose height should be returned.</param>
        /// <returns>The index of the vertex in the mesh.</returns>
        private int GetPointVertexIndex((int x, int z) pointInChunk)
        {
            // point at the right edge of the mesh, but not the topmost point
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && pointInChunk.z != Terrain.Instance.TilesPerChunkSide)
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshProperties.TriangleIndices_Terrain.Length 
                        + MeshProperties.SharedVertexOffsets[1][0];

            // point at the top edge of the mesh, but not the rightmost point
            else if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && pointInChunk.x != Terrain.Instance.TilesPerChunkSide)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshProperties.TriangleIndices_Terrain.Length 
                        + MeshProperties.SharedVertexOffsets[2][0];

            // topmost rightmost point
            else if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && pointInChunk.x == Terrain.Instance.TilesPerChunkSide)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshProperties.TriangleIndices_Terrain.Length 
                        + MeshProperties.SharedVertexOffsets[3][0];

            // any other point
            else
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshProperties.TriangleIndices_Terrain.Length 
                        + MeshProperties.SharedVertexOffsets[0][0];
        }

        #endregion


        #region Tile Center

        /// <summary>
        /// Gets the height of the terrain at the center of the tile represented by the given point.
        /// </summary>
        /// <param name="tilePoint">The coordinates of the point at the bottom-left of the tile whose center height should be returned.</param>
        /// <returns>The height at the center of the given tile.</returns>
        public float GetTileCenterHeight(TerrainTile tilePoint) => GetTileCenterHeight(GetChunkCoordinates(tilePoint.X, tilePoint.Z));

        /// <summary>
        /// Gets the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The coordinates relative to the chunk of the tile whose center height should be returned.</param>
        /// <returns>The height at the center of the given tile.</returns>
        private float GetTileCenterHeight((int x, int z) tileInChunk) 
            => (int)m_MeshData.GetVertexPosition(GetTileCenterVertexIndex(tileInChunk)).y;

        /// <summary>
        /// Sets the height of all the vertices at the center of the given tile to the given height.
        /// </summary>
        /// <param name="tileInChunk">The coordinates relative to the chunk of the tile whose center height should be returned.</param>
        /// <param name="height">The height the tile center should be set to.</param>
        private void SetTileCenterHeight((int x, int z) tileInChunk, float height)
        {
            int tileIndex = GetPointVertexIndex(tileInChunk);

            foreach (var index in MeshProperties.SharedVertexOffsets[^1])
                m_MeshData.SetVertexHeight(tileIndex + index, height);
        }

        /// <summary>
        /// Computes the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The coordinates relative to the chunk of the tile whose center height should be returned.</param>
        /// <returns>The height at the center of the given tile.</returns>
        private float CalculateTileCenterHeight((int x, int z) tileInChunk)
        {
            int[] cornerHeights = new int[4];
            int i = 0;
            for (int zOffset = 0; zOffset <= 1; ++zOffset)
                for (int xOffset = 0; xOffset <= 1; ++xOffset)
                    cornerHeights[i++] = GetMeshHeightAtPoint((tileInChunk.x + xOffset, tileInChunk.z + zOffset));

            if (cornerHeights[0] == cornerHeights[3] && cornerHeights[1] == cornerHeights[2])
                return Mathf.Max(cornerHeights);
            else
                return Mathf.Min(cornerHeights) + (Terrain.Instance.StepHeight / 2);
        }

        /// <summary>
        /// Recalculates the heights of the centers of all the tiles in the terrain chunk.
        /// </summary>
        public void RecalculateAllTileCenterHeights()
        {
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                    SetTileCenterHeight((x, z), CalculateTileCenterHeight((x, z)));
        }

        /// <summary>
        /// Gets a vertex in the mesh that corresponds to the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The coordinates relative to the chunk of the tile whose center height should be returned.</param>
        /// <returns>The index of the vertex in the mesh.</returns>
        private int GetTileCenterVertexIndex((int x, int z) tileInChunk) => GetPointVertexIndex(tileInChunk) + 1;

        #endregion


        /// <summary>
        /// Translates the given coordinates from coordinates relative to the whole terrain to coordinates relative to the chunk.
        /// </summary>
        /// <param name="x">The x coordinate relative to the whole terrain.</param>
        /// <param name="z">The z coordinate relative to the whole terrain.</param>
        /// <returns>The coordinates relative to the chunk.</returns>
        private (int, int) GetChunkCoordinates(int x, int z)
            => (x - m_ChunkIndex.x * Terrain.Instance.TilesPerChunkSide, z - m_ChunkIndex.z * Terrain.Instance.TilesPerChunkSide);
    }
}