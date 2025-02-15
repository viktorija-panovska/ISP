using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainChunk</c> class handles the creation and modification of one chunk of the terrain.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TerrainChunk : MonoBehaviour
    {
        #region Class Fields

        /// <summary>
        /// A container for the data of this chunk's mesh i.e. the vertices and the triangles that make it up.
        /// </summary>
        private MeshData m_MeshData;

        private (int x, int z) m_ChunkIndex;
        /// <summary>
        /// Gets the index of the chunk in the terrain chunk grid.
        /// </summary>
        public (int X, int Z) ChunkIndex { get => m_ChunkIndex; }

        /// <summary>
        /// Gets the <c>Vector3</c> position of the chunk in the world.
        /// </summary>
        public Vector3 ChunkPosition { get => transform.position; }

        /// <summary>
        /// The directions of the neighbors to check when computing the height of a vertex.
        /// </summary>
        private static readonly (int, int)[] m_HeightCheckDirections = new (int, int)[] 
        { 
            (-1, -1), (-1, 0), (-1, 1), (0, -1), (1, -1) 
        };

        #endregion


        #region Setup Chunk

        /// <summary>
        /// Sets up the properties of the chunk and generates its mesh.
        /// </summary>
        /// <param name="gridX">The X coordinate of the chunk on the terrain chunk grid.</param>
        /// <param name="gridZ">The Z coordinate of the chunk on the terrain chunk grid.</param>
        public void Setup(int gridX, int gridZ)
        {
            m_ChunkIndex = (gridX, gridZ);
            name = "Chunk " + ChunkIndex.X + " " + ChunkIndex.Z;

            SetVisibility(true);
            m_MeshData = ConstructGrid();
            SetVertexHeights();
            SetMesh();
        }

        /// <summary>
        /// Triggers the application of the current <c>MeshData</c> to the terrain chunk object.
        /// </summary>
        public void SetMesh() => m_MeshData.SetMesh(gameObject, Terrain.Instance.TerrainMaterial);

        #endregion


        #region Generate Terrain Chunk

        /// <summary>
        /// Places the vertices and triangles to create a new mesh for the terrain chunk.
        /// </summary>
        /// <returns>An instance of the <c>MeshData</c> struct containing the data of the generated mesh.</returns>
        private MeshData ConstructGrid()
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
                            0,
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
                    SetPointHeight((0, z), Terrain.Instance.GetPointHeight(
                        (ChunkIndex.X - 1, ChunkIndex.Z),
                        (ChunkIndex.X * Terrain.Instance.TilesPerChunkSide, z + ChunkIndex.Z * Terrain.Instance.TilesPerChunkSide)
                    ));
            }

            // bottom row should have the same heights as the top row in the chunk below
            if (ChunkIndex.Z > 0)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                    SetPointHeight((x, 0), Terrain.Instance.GetPointHeight(
                        (ChunkIndex.X, ChunkIndex.Z - 1),
                        (x + ChunkIndex.X * Terrain.Instance.TilesPerChunkSide, ChunkIndex.Z * Terrain.Instance.TilesPerChunkSide)
                    ));
            }

            for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    if ((x == 0 && ChunkIndex.X > 0) || (z == 0 && ChunkIndex.Z > 0)) continue;

                    int height = CalculateVertexHeight((x, z));
                    SetPointHeight((x, z), height);
                }
            }

            RecomputeAllCenters();
        }

        /// <summary>
        /// Generates and computes the height of the terrain at the given point.
        /// </summary>
        /// <param name="pointInChunk">The coordinates relative to the chunk of the point whose height should be computed.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        private int CalculateVertexHeight((int x, int z) pointInChunk)
        {
            int vertexIndex = GetPointIndex(pointInChunk);

            // the noise generator essentially returns a percentage of the maximum height the height of the current point will be
            float noiseHeight = Terrain.Instance.NoiseGenerator.GetNoiseAtPosition(ChunkPosition + m_MeshData.GetVertexPosition(vertexIndex)) * Terrain.Instance.MaxHeight;
            // make sure the height of the is a multiple of the step height.
            int height = Mathf.FloorToInt(noiseHeight / Terrain.Instance.StepHeight) * Terrain.Instance.StepHeight;

            // Vertex (0, 0) in chunk (0, 0) is the very first vertex, so it has nothing to coordinate with
            if (ChunkIndex == (0, 0) && pointInChunk == (0, 0))
                return height;

            List<int> neighborHeights = new();

            foreach ((int dx, int dz) in m_HeightCheckDirections)
            {
                (int x, int z) neighbor = (pointInChunk.x + dx, pointInChunk.z + dz);

                // if we are looking at the point above the current point and we are in the first chunk
                // (that doesn't copy the heights of the first column from the previous chunk) or we're in any
                // other column than the first one, then the point above won't have been assigned a height yet.
                if (!IsPointInChunk(neighbor) || (dz == 1 && (ChunkIndex.X == 0 || neighbor.x > 0)))
                    continue;

                neighborHeights.Add(GetMeshHeightAtPoint(neighbor));
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
        /// Sets the visibility of the chunk.
        /// </summary>
        /// <param name="isVisible">True if the chunk should be set as visible, false otherwise.</param>
        public void SetVisibility(bool isVisible) => GetComponent<MeshRenderer>().enabled = isVisible;

        /// <summary>
        /// Changes the height at the given <c>TerrainPoint</c> and propagates the changes if needed 
        /// so that the one step height difference between two points is maintained.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be changed.</param>
        /// <param name="lower">True if the height of the pointInChunk should be lowered by one step, false if it should be elevated by one step.</param>
        /// <param name="steps"></param>
        public void ChangeHeight(TerrainPoint point, bool lower)
        {
            (int x, int z) pointInChunk = GetPointInChunk((point.GridX, point.GridZ));

            SetPointHeight(pointInChunk, Mathf.Clamp(
                GetPointHeight((point.GridX, point.GridZ)) + (lower ? -1 : 1) * Terrain.Instance.StepHeight,
                0,
                Terrain.Instance.MaxHeight
            ));

            // Update neighboring vertices
            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    TerrainPoint neighbor = new(point.GridX + xOffset, point.GridZ + zOffset);

                    if ((xOffset, zOffset) == (0, 0) || !Terrain.Instance.IsPointInBounds(new(point.GridX + xOffset, point.GridZ + zOffset)))
                        continue;

                    if (Mathf.Abs(point.Y - neighbor.Y) > Terrain.Instance.StepHeight && IsPointInChunk((pointInChunk.x + xOffset, pointInChunk.z + zOffset)))
                        Terrain.Instance.ChangePointHeight(neighbor, lower);
                }
            }

            RecomputeCenters(pointInChunk);
        }

        /// <summary>
        /// Sets the height at a given <c>TerrainPoint</c>.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be changed.</param>
        /// <param name="height">The value the height at the <c>TerrainPoint</c> should be set to.</param>
        public void SetVertexHeight(TerrainPoint point, int height) => SetPointHeight(GetPointInChunk((point.GridX, point.GridZ)), height);

        /// <summary>
        /// Recalculates the heights of the centers of all the tiles in the terrain chunk.
        /// </summary>
        public void RecomputeAllCenters()
        {
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                    SetCenterHeight((x, z), CalculateTileCenterHeight((x, z)));
        }

        /// <summary>
        /// Recalculates the height of the centers of the tiles surrounding a given pointInChunk in the chunk.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates relative to one chunk of the pointInChunk whose surrounding centers should be recomputed.</param>
        private void RecomputeCenters((int x, int z) pointInChunk)
        {
            for (int z = -1; z <= 0; ++z)
            {
                for (int x = -1; x <= 0; ++x)
                {
                    (int x, int z) tile = (pointInChunk.x + x, pointInChunk.z + z);

                    if (tile.x >= 0 && tile.z >= 0 && tile.x < Terrain.Instance.TilesPerChunkSide && tile.z < Terrain.Instance.TilesPerChunkSide)
                    {
                        int prevHeight = GetCenterHeight(tile);
                        int newHeight = CalculateTileCenterHeight(tile);

                        if (prevHeight == newHeight)
                            continue;

                        SetCenterHeight(tile, newHeight);
                    }
                }
            }
        }

        #endregion


        #region Point Info

        /// <summary>
        /// Checks whether the point is inside the bounds of the chunk.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates of the point relative to the chunk.</param>
        /// <returns>True if the point is in the bounds of the chunk, false otherwise.</returns>
        public bool IsPointInChunk((int x, int z) pointInChunk)
            => pointInChunk.x >= 0 && pointInChunk.z >= 0 && pointInChunk.x <= Terrain.Instance.TilesPerChunkSide && pointInChunk.z <= Terrain.Instance.TilesPerChunkSide;

        /// <summary>
        /// Translates the coordinates of the given pointInChunk from coordinates relative to the whole terrain to coordinates relative to one chunk.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the pointInChunk relative to the whole terrain.</param>
        /// <returns>The (x, z) coordinates of the pointInChunk relative to one chunk.</returns>
        public (int x, int z) GetPointInChunk((int x, int z) point)
            => (point.x - m_ChunkIndex.x * Terrain.Instance.TilesPerChunkSide, point.z - m_ChunkIndex.z * Terrain.Instance.TilesPerChunkSide);

        /// <summary>
        /// Gets the height of the terrain at the given pointInChunk.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the pointInChunk whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given pointInChunk.</returns>
        public int GetPointHeight((int x, int z) point) => GetMeshHeightAtPoint(GetPointInChunk(point));

        /// <summary>
        /// Gets the height of the mesh at the given point.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates relative to one chunk of the pointInChunk whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given pointInChunk.</returns>
        private int GetMeshHeightAtPoint((int x, int z) pointInChunk) => (int)m_MeshData.GetVertexPosition(GetPointIndex(pointInChunk)).y;

        /// <summary>
        /// Gets an index of the vertex in the mesh which corresponds to the given pointInChunk.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates in relation to the chunk of a pointInChunk.</param>
        /// <returns>The index of a vertex at the given pointInChunk.</returns>
        private int GetPointIndex((int x, int z) pointInChunk)
        {
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && pointInChunk.z != pointInChunk.x)
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshProperties.TriangleIndices_Terrain.Length + 2;
            else if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && pointInChunk.x != pointInChunk.z)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshProperties.TriangleIndices_Terrain.Length + 8;
            else if (pointInChunk.x == pointInChunk.z && pointInChunk.z == Terrain.Instance.TilesPerChunkSide)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshProperties.TriangleIndices_Terrain.Length + 5;
            else
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshProperties.TriangleIndices_Terrain.Length;
        }

        /// <summary>
        /// Sets the height of all the vertices at the given point to the given height.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates in relation to the chunk of the point whose height should be set.</param>
        /// <param name="height">The value the height of the point should be set to.</param>
        private void SetPointHeight((int x, int z) pointInChunk, int height)
        {
            int vertexIndex = 0;
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

        #endregion


        #region Tile Center Info

        /// <summary>
        /// Gets the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose tile center height should be returned.</param>
        /// <returns>An <c>int</c> representing the height of the center of the given tile.</returns>
        public int GetTileCenterHeight((int x, int z) tile) => GetCenterHeight(GetPointInChunk(tile));

        /// <summary>
        /// Gets the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The (x, z) coordinates in relation to the chunk of the tile whose tile center height should be returned.</param>
        /// <returns>An <c>int</c> representing the height of the center of the given tile.</returns>
        private int GetCenterHeight((int x, int z) tileInChunk) => (int)m_MeshData.GetVertexPosition(GetCenterIndex(tileInChunk)).y;

        /// <summary>
        /// Gets an index of the vertex in the mesh which is at the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The (x, z) coordinates in relation to the chunk of the tile whose tile center height should be returned.</param>
        /// <returns>The index of a vertex at the center of the tile.</returns>
        private int GetCenterIndex((int x, int z) tileInChunk) => GetPointIndex(tileInChunk) + 1;

        /// <summary>
        /// Sets the height of all the vertices at the center of the given tile to the given height.
        /// </summary>
        /// <param name="tileInChunk">The (x, z) coordinates in relation to the chunk of the tile whose tile center height should be set.</param>
        /// <param name="height">The value the height of the center of the tile should be set to.</param>
        private void SetCenterHeight((int x, int z) tileInChunk, int height)
        {
            int tileIndex = GetPointIndex(tileInChunk);

            foreach (var index in MeshProperties.SharedVertexOffsets[^1])
                m_MeshData.SetVertexHeight(tileIndex + index, height);
        }

        /// <summary>
        /// Computes the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tileInChunk">The (x, z) coordinates in relation to the chunk of the tile whose tile center height should be computed.</param>
        /// <returns>An <c>int</c> representing the height of the center of the given tile.</returns>
        private int CalculateTileCenterHeight((int x, int z) tileInChunk)
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

        #endregion


        #region Tile Info

        /// <summary>
        /// Checks whether all the points at the corners of the given tile are at the same height and whether they are above the water level.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is flat and not underwater, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) tile)
        {
            (int x, int z) inChunk = GetPointInChunk(tile);

            int height = GetMeshHeightAtPoint(inChunk);

            for (int z = 0; z <= 1; ++z)
                for (int x = 0; x <= 1; ++x)
                    if (height != GetMeshHeightAtPoint((inChunk.x + x, inChunk.z + z)))
                        return false;

            return height != Terrain.Instance.WaterLevel;
        }

        #endregion
    }
}