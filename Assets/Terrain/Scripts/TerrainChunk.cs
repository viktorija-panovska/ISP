using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainChunk</c> class is a class which contains properties
    /// and methods used to create and modify one chunk of the terrain.
    /// </summary>
    public class TerrainChunk
    {
        private readonly GameObject m_ChunkObject;
        private readonly MeshData m_MeshData;

        private readonly (int x, int z) m_ChunkIndex;
        /// <summary>
        /// Gets the (x, z) index of the chunk in the terrain chunk array.
        /// </summary>
        public (int X, int Z) ChunkIndex { get => m_ChunkIndex; }

        /// <summary>
        /// Gets the <c>Vector3</c> position of the chunk in the world.
        /// </summary>
        public Vector3 ChunkPosition { get => m_ChunkObject.transform.position; }

        /// <summary>
        /// Each cell the <c>Structure</c> occupying the tile with corresponding coordinates, or null if there isn't one.
        /// </summary>
        private readonly Structure[,] m_StructureOnTile = new Structure[Terrain.Instance.TilesPerChunkSide, Terrain.Instance.TilesPerChunkSide];


        /// <summary>
        /// The constructor for the <c>TerrainChunk</c> class.
        /// </summary>
        /// <param name="mapPositionX">The x index of the chunk in the terrain chunk array.</param>
        /// <param name="mapPositionZ">The z index of the chunk in the terrain chunk array.</param>
        /// <param name="parentTransform">The transform of the <c>Terrain</c> object.</param>
        public TerrainChunk(int mapPositionX, int mapPositionZ, Transform parentTransform)
        {
            m_ChunkObject = new GameObject();
            m_ChunkObject.AddComponent<MeshFilter>();
            m_ChunkObject.AddComponent<MeshRenderer>();
            m_ChunkObject.AddComponent<MeshCollider>();
            m_ChunkObject.AddComponent<Rigidbody>();

            m_ChunkIndex = (mapPositionX, mapPositionZ);

            m_ChunkObject.transform.position = new Vector3(
                ChunkIndex.X * Terrain.Instance.UnitsPerChunkSide,
                0,
                ChunkIndex.Z * Terrain.Instance.UnitsPerChunkSide
            );
            m_ChunkObject.transform.SetParent(parentTransform);
            m_ChunkObject.name = "Chunk " + ChunkIndex.X + " " + ChunkIndex.Z;

            m_ChunkObject.layer = LayerMask.NameToLayer("Terrain");
            m_ChunkObject.GetComponent<Rigidbody>().isKinematic = true;
            m_ChunkObject.GetComponent<Rigidbody>().useGravity = false;

            SetVisibility(false);
            m_MeshData = GenerateMeshData();
            SetVertexHeights();
            SetMesh();
        }


        #region Generate Terrain Chunk

        /// <summary>
        /// Applies the current <c>MeshData</c> to the terrain chunk.
        /// </summary>
        public void SetMesh() => m_MeshData.SetMesh(m_ChunkObject, Terrain.Instance.TerrainMaterial);

        /// <summary>
        /// Generates all the required data to create the terrain mesh.
        /// </summary>
        /// <returns>An instance of the <c>MeshData</c> struct containing the data of the generated mesh.</returns>
        private MeshData GenerateMeshData()
        {
            MeshData meshData = new(Terrain.Instance.TilesPerChunkSide, Terrain.Instance.TilesPerChunkSide, isTerrain: true);
            int vertexIndex = 0;
            int triangleIndex = 0;

            // Generating the mesh tile by tile
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    for (int i = 0; i < MeshData.TriangleIndices_Terrain.Length; ++i)
                    {
                        int index = MeshData.TriangleIndices_Terrain[i];

                        Vector3 vertex = new(
                            (x + MeshData.VertexOffsets[index].x) * Terrain.Instance.UnitsPerTileSide,
                            0,
                            (z + MeshData.VertexOffsets[index].z) * Terrain.Instance.UnitsPerTileSide
                        );
                        meshData.AddVertex(vertexIndex + i, vertex);

                        // After every third vertex, add a triangle
                        if ((i + 1) % 3 == 0)
                        {
                            meshData.AddTriangle(triangleIndex, vertexIndex + i - 2, vertexIndex + i - 1, vertexIndex + i);
                            triangleIndex += 3;
                        }
                    }
                    vertexIndex += MeshData.TriangleIndices_Terrain.Length;
                }
            }

            return meshData;
        }

        /// <summary>
        /// Sets the heights for each point in the terrain chunk.
        /// </summary>
        private void SetVertexHeights()
        {
            if (ChunkIndex.X > 0)
            {
                for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
                    SetPointHeight((0, z), Terrain.Instance.GetPointHeight(
                        (ChunkIndex.X - 1, ChunkIndex.Z),
                        (ChunkIndex.X * Terrain.Instance.TilesPerChunkSide, z + ChunkIndex.Z * Terrain.Instance.TilesPerChunkSide)
                    ));
            }

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
                    SetPointHeight((x, z), CalculateVertexHeight((x, z)));
                }
            }

            RecomputeAllCenters();
        }

        /// <summary>
        /// Generates and computes the height of the terrain at the given point.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates relative to one chunk of the point whose height should be computed.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        private int CalculateVertexHeight((int x, int z) pointInChunk)
        {
            int vertexIndex = GetPointIndex(pointInChunk);

            int height = Mathf.FloorToInt(
                Terrain.Instance.MapGenerator.GetHeightAtPosition(ChunkPosition + m_MeshData.Vertices[vertexIndex]) * Terrain.Instance.MaxHeightSteps
            ) * Terrain.Instance.StepHeight;

            // Vertex (0, 0) in chunk (0, 0)
            if (pointInChunk.x == 0 && pointInChunk.z == 0)
                return height;

            // first row in chunk (0, 0)
            if (pointInChunk.z == 0 && ChunkIndex.X == 0)
            {
                int lastHeight = GetMeshHeightAtPoint((pointInChunk.x - 1, pointInChunk.z));
                height = Mathf.Clamp(height, lastHeight - Terrain.Instance.StepHeight, lastHeight + Terrain.Instance.StepHeight);
            }
            else
            {
                List<(int, int)> directions = new(new (int, int)[] { (-1, -1), (-1, 0), (0, -1), (1, -1) });

                if (pointInChunk.x == 1 && ChunkIndex.X > 0)
                    directions.Add((-1, 1));

                List<int> neighborHeights = new();

                foreach ((int neighborX, int neighborZ) in directions)
                    if (pointInChunk.x + neighborX >= 0 && pointInChunk.x + neighborX <= Terrain.Instance.TilesPerChunkSide &&
                        pointInChunk.z + neighborZ >= 0 && pointInChunk.z + neighborZ <= Terrain.Instance.TilesPerChunkSide)
                        neighborHeights.Add(GetMeshHeightAtPoint((pointInChunk.x + neighborX, pointInChunk.z + neighborZ)));

                if (neighborHeights.TrueForAll(EqualToFirst))
                    height = Mathf.Clamp(
                        height,
                        Mathf.Max(0, neighborHeights[0] - Terrain.Instance.StepHeight),
                        Mathf.Min(neighborHeights[0] + Terrain.Instance.StepHeight, Terrain.Instance.MaxHeight));
                else
                {
                    int[] neighborHeightsArray = neighborHeights.ToArray();
                    int minHeight = Mathf.Min(neighborHeightsArray);
                    int maxHeight = Mathf.Max(neighborHeightsArray);

                    if (Mathf.Abs(minHeight - maxHeight) > Terrain.Instance.StepHeight)
                        height = minHeight + Terrain.Instance.StepHeight;
                    else
                        height = Mathf.Clamp(height, minHeight, maxHeight);
                }
                bool EqualToFirst(int x) => x == neighborHeights[0];
            }

            return height;
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


        #region Modify Terrain Chunk

        /// <summary>
        /// Sets the visibility of the chunk.
        /// </summary>
        /// <param name="isVisible">True if the chunk should be set as visible, false otherwise.</param>
        public void SetVisibility(bool isVisible) => m_ChunkObject.GetComponent<MeshRenderer>().enabled = isVisible;

        /// <summary>
        /// Changes the height at the given <c>MapPoint</c> and propagates the changes if needed 
        /// so that the one step height difference between two points is maintained.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> whose height should be changed.</param>
        /// <param name="lower">True if the height of the point should be lowered by one step, false if it should be elevated by one step.</param>
        /// <param name="steps"></param>
        public void ChangeHeights(MapPoint point, bool lower)
        {
            (int x, int z) pointInChunk = GetPointInChunk((point.GridX, point.GridZ));

            SetPointHeight(pointInChunk, Mathf.Clamp(
                GetPointHeight((point.GridX, point.GridZ)) + (lower ? -1 : 1) * Terrain.Instance.StepHeight,
                0,
                Terrain.Instance.MaxHeight
            ));

            for (int z = 0; z >= -1; --z)
            {
                for (int x = 0; x >= -1; --x)
                {
                    (int x, int z) tile = (pointInChunk.x + x, pointInChunk.z + z);

                    if (tile.x < 0 || tile.x >= Terrain.Instance.TilesPerChunkSide || tile.z < 0 || tile.z >= Terrain.Instance.TilesPerChunkSide)
                        continue;

                    Structure structure = GetStructureOnTile((point.GridX + x, point.GridZ + z));
                    if (structure != null)
                        structure.ReactToTerrainChange();
                }
            }

            // Update neighboring vertices
            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    MapPoint neighbor = new(point.GridX + xOffset, point.GridZ + zOffset);

                    if ((xOffset, zOffset) == (0, 0) || !Terrain.Instance.IsPointInBounds(new(point.GridX + xOffset, point.GridZ + zOffset)))
                        continue;

                    if (Mathf.Abs(point.Y - neighbor.Y) > Terrain.Instance.StepHeight && IsPointInChunk((pointInChunk.x + xOffset, pointInChunk.z + zOffset)))
                        Terrain.Instance.ChangePointHeight(neighbor, lower);
                }
            }

            RecomputeCenters(pointInChunk);
        }

        /// <summary>
        /// Sets the height at a given <c>MapPoint</c>.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> whose height should be changed.</param>
        /// <param name="height">The value the height at the <c>MapPoint</c> should be set to.</param>
        public void SetVertexHeight(MapPoint point, int height) => SetPointHeight(GetPointInChunk((point.GridX, point.GridZ)), height);

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
        /// Recalculates the height of the centers of the tiles surrounding a given point in the chunk.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates relative to one chunk of the point whose surrounding centers should be recomputed.</param>
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
        /// <param name="point">The (x, z) coordinates of the point.</param>
        /// <returns>True if the point is in the bounds of the chunk, false otherwise.</returns>
        public bool IsPointInChunk((int x, int z) point)
            => point.x >= 0 && point.z >= 0 && point.x <= Terrain.Instance.TilesPerChunkSide && point.z <= Terrain.Instance.TilesPerChunkSide;

        /// <summary>
        /// Translates the coordinates of the given point from coordinates relative to the whole terrain to coordinates relative to one chunk.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the point relative to the whole terrain.</param>
        /// <returns>The (x, z) coordinates of the point relative to one chunk.</returns>
        public (int x, int z) GetPointInChunk((int x, int z) point)
            => (point.x - m_ChunkIndex.x * Terrain.Instance.TilesPerChunkSide, point.z - m_ChunkIndex.z * Terrain.Instance.TilesPerChunkSide);

        /// <summary>
        /// Gets the height of the terrain at the given point.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the point whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        public int GetPointHeight((int x, int z) point) => GetMeshHeightAtPoint(GetPointInChunk(point));

        /// <summary>
        /// Gets the height of the mesh at the given point.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates relative to one chunk of the point whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        private int GetMeshHeightAtPoint((int x, int z) pointInChunk) => (int)m_MeshData.Vertices[GetPointIndex(pointInChunk)].y;

        /// <summary>
        /// Gets an index of the vertex in the mesh which corresponds to the given point.
        /// </summary>
        /// <param name="pointInChunk">The (x, z) coordinates in relation to the chunk of a point.</param>
        /// <returns>The index of a vertex at the given point.</returns>
        private int GetPointIndex((int x, int z) pointInChunk)
        {
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && pointInChunk.z != pointInChunk.x)
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshData.TriangleIndices_Terrain.Length + 2;
            else if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && pointInChunk.x != pointInChunk.z)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshData.TriangleIndices_Terrain.Length + 8;
            else if (pointInChunk.x == pointInChunk.z && pointInChunk.z == Terrain.Instance.TilesPerChunkSide)
                return ((pointInChunk.z - 1) * Terrain.Instance.TilesPerChunkSide + (pointInChunk.x - 1)) * MeshData.TriangleIndices_Terrain.Length + 5;
            else
                return (pointInChunk.z * Terrain.Instance.TilesPerChunkSide + pointInChunk.x) * MeshData.TriangleIndices_Terrain.Length;
        }

        /// <summary>
        /// Sets the height of all the vertices at the given point to the given height..
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

                    for (int i = 0; i < MeshData.SharedVertexOffsets[vertexIndex].Count; ++i)
                    {
                        int index = tileIndex * MeshData.TriangleIndices_Terrain.Length + MeshData.SharedVertexOffsets[vertexIndex][i];

                        if (index < 0) continue;

                        m_MeshData.Vertices[index].y = height;
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
        private int GetCenterHeight((int x, int z) tileInChunk) => (int)m_MeshData.Vertices[GetCenterIndex(tileInChunk)].y;

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

            foreach (var index in MeshData.SharedVertexOffsets[^1])
                m_MeshData.Vertices[tileIndex + index].y = height;
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

        /// <summary>
        /// Checks whether there is a structure on the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is occupied by a structure, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) tile)
        {
            (int x, int z) inChunk = GetPointInChunk(tile);
            return m_StructureOnTile[inChunk.z, inChunk.x];
        }

        /// <summary>
        /// Gets the <c>Structure</c> occupying the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <returns>The <c>Structure</c> occupying the given tile, or <c>null</c> if the tile is unoccupied.</returns>
        public Structure GetStructureOnTile((int x, int z) tile)
        {
            (int x, int z) inChunk = GetPointInChunk(tile);
            return m_StructureOnTile[inChunk.z, inChunk.x];
        }

        /// <summary>
        /// Sets the given structure to occupy the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <param name="structure">The <c>Structure</c> that should be placed on the tile.</param>
        public void SetOccupiedTile((int x, int z) tile, Structure structure)
        {
            (int x, int z) inChunk = GetPointInChunk(tile);
            m_StructureOnTile[inChunk.z, inChunk.x] = structure;
        }

        #endregion
    }
}