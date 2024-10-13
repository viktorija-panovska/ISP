using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    public struct MapPoint : IEquatable<MapPoint>, INetworkSerializable
    {
        private int m_TileX;
        private int m_TileZ;

        /// <summary>
        /// Gets the X coordinate of this <c>MapPoint</c> on the grid.
        /// </summary>
        public readonly int TileX { get => m_TileX; }
        /// <summary>
        /// Gets the Z coordinate of this <c>MapPoint</c> on the grid.
        /// </summary>
        public readonly int TileZ { get => m_TileZ; }

        public readonly int X { get => m_TileX * Terrain.Instance.UnitsPerTileSide; }
        public readonly int Z { get => m_TileZ * Terrain.Instance.UnitsPerTileSide; }

        public readonly int Y
        {
            get => m_TouchingChunks == null
                ? Terrain.Instance.GetPointHeight((m_TileX, m_TileZ))
                : Terrain.Instance.GetPointHeight(m_TouchingChunks[0], (m_TileX, m_TileZ));
        }

        private List<(int x, int z)> m_TouchingChunks;
        /// <summary>
        /// Gets the list of chunks which this <c>MapPoint</c> is a part of if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<(int x, int z)> TouchingChunks
        {
            get
            {
                m_TouchingChunks ??= GetAllTouchingChunks();
                return m_TouchingChunks;
            }
        }

        private List<MapPoint> m_Neighbors;
        /// <summary>
        /// Gets the list of neighboring points of this <c>MapPoint</c> if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<MapPoint> Neighbors
        {
            get
            {
                m_Neighbors ??= GetAllPointNeighbors();
                return m_Neighbors;
            }
        }

        public readonly bool IsOnEdge { get => m_TileX == Terrain.Instance.TilesPerSide || m_TileZ == Terrain.Instance.TilesPerSide; }

        public readonly Vector3 TileCenter 
        { 
            get => new(
                (m_TileX + 0.5f) * Terrain.Instance.TilesPerSide, 
                Terrain.Instance.GetTileCenterHeight((m_TileX, m_TileZ)),
                (m_TileZ + 0.5f) * Terrain.Instance.TilesPerSide
            ); 
        }


        #region Constructors

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when the coordinates of the point on the grid need to be computed from a world position.
        /// </summary>
        /// <param name="x">The x coordinate of a world position.</param>
        /// <param name="z">The y coordinate of a world position.</param>
        public MapPoint(float x, float z)
            : this(Mathf.RoundToInt(x / Terrain.Instance.UnitsPerTileSide), Mathf.RoundToInt(z / Terrain.Instance.UnitsPerTileSide)) { }

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when the coordinates of the point on the grid are already computed.
        /// </summary>
        /// <param name="x">The x coordinate of the point on the grid.</param>
        /// <param name="z">The z coordinate of the point on the grid.</param>
        public MapPoint(int x, int z)
        {
            m_TileX = x;
            m_TileZ = z;

            m_TouchingChunks = null;
            m_Neighbors = null;
        }

        #endregion


        #region Conversion

        /// <summary>
        /// Gets the <c>MapPoint</c> coordinates as a world position.
        /// </summary>
        /// <returns>A <c>Vector3</c> of the world position.</returns>
        public readonly Vector3 ToWorldPosition() => new(X, Y, Z);

        /// <summary>
        /// Gets a string representation of the <c>MapPoint</c>.
        /// </summary>
        /// <returns>A <c>string</c> representation of the <c>MapPoint</c>.</returns>
        public override readonly string ToString() => $"MapPoint -> {ToWorldPosition()}";

        #endregion


        #region Equality

        /// <summary>
        /// Tests whether two <c>MapPoint</c>s are equal to each other.
        /// </summary>
        /// <param name="a">The first <c>MapPoint</c> operand.</param>
        /// <param name="b">The second <c>MapPoint</c> operand.</param>
        /// <returns>True if the two operands are equal to each other, false otherwise.</returns>
        public static bool operator ==(MapPoint a, MapPoint b) => a.Equals(b);

        /// <summary>
        /// Tests whether two <c>MapPoint</c>s are not equal to each other.
        /// </summary>
        /// <param name="a">The first <c>MapPoint</c> operand.</param>
        /// <param name="b">The second <c>MapPoint</c> operand.</param>
        /// <returns>True if the two operands are not equal to each other, false otherwise.</returns>
        public static bool operator !=(MapPoint a, MapPoint b) => !a.Equals(b);

        /// <summary>
        /// Tests whether an object is equal to this <c>MapPoint</c>.
        /// </summary>
        /// <param name="obj">The object which is being compared against this <c>MapPoint</c>.</param>
        /// <returns>True if the object is a <c>MapPoint</c> and is equal to this <c>MapPoint</c>, false otherwise.</returns>
        public override readonly bool Equals(object obj) => obj.GetType() == typeof(MapPoint) && Equals((MapPoint)obj);

        #endregion


        #region Interface members

        /// <summary>
        /// Tests whether a <c>MapPoint</c> is equal to this <c>MapPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <param name="other">The <c>MapPoint</c> instance which is being compared against this <c>MapPoint</c>.</param>
        /// <returns>True if the coordinates of the <c>MapPoint</c>s are equal, false otherwise.</returns>
        public readonly bool Equals(MapPoint other) => m_TileX == other.TileX && m_TileZ == other.TileZ;

        /// <summary>
        /// Gets the hash code for the current <c>MapPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <returns>The integer hash code of the current <c>MapPoint</c>.</returns>
        public override readonly int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Serializes the <c>MapPoint</c> coordinates.
        /// </summary>
        /// <remarks>Interface member of <c>INetworkSerializable</c>.</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer">The <c>BufferSerializer</c> to be used in the serialization.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_TileX);
            serializer.SerializeValue(ref m_TileZ);
        }

        #endregion


        #region Methods

        /// <summary>
        /// Gets all the chunks that the given point belongs to.
        /// </summary>
        /// <param name="point">The (x, z) index of a point in the terrain mesh.</param>
        /// <returns>A list of (x,z) indices of chunks that contain the given point.</returns>
        private readonly List<(int x, int z)> GetAllTouchingChunks()
        {
            (int x, int z) mainChunk = Terrain.Instance.GetChunkIndex((m_TileX, m_TileZ));

            List<(int x, int z)> chunks = new() { mainChunk };

            (int x, int z) pointInChunk = Terrain.Instance.GetChunkByIndex(mainChunk).GetPointInChunk((m_TileX, m_TileZ));

            // bottom left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z - 1));

            // bottom right
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && mainChunk.x < Terrain.Instance.ChunksPerSide - 1 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x + 1, mainChunk.z - 1));

            // top left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == Terrain.Instance.TilesPerChunkSide && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
                chunks.Add((mainChunk.x - 1, mainChunk.z + 1));

            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && mainChunk.x < Terrain.Instance.ChunksPerSide - 1 &&
                pointInChunk.z == Terrain.Instance.TilesPerChunkSide && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z + 1));

            // left
            if (pointInChunk.x == 0 && mainChunk.x > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z));

            // right
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && mainChunk.x < Terrain.Instance.ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z));

            // bottom
            if (pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x, mainChunk.z - 1));

            // top
            if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
                chunks.Add((mainChunk.x, mainChunk.z + 1));

            return chunks;
        }

        /// <summary>
        /// Gets all the point which neighbor the given point.
        /// </summary>
        /// <returns>A list of (x,z) indices of points that neighbor the given point.</returns>
        private readonly List<MapPoint> GetAllPointNeighbors()
        {
            List<MapPoint> neighbors = new();

            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    if (Terrain.Instance.IsPointInBounds((m_TileX + x, m_TileZ + z)))
                        neighbors.Add(new(m_TileX + x, m_TileZ + z));
                }
            }

            return neighbors;
        }

        #endregion
    }
}