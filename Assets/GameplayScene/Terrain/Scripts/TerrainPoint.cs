using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>TerrainPoint</c> struct represents one point on the terrain grid.
    /// </summary>
    public struct TerrainPoint : IEquatable<TerrainPoint>, INetworkSerializable
    {
        private int m_X;
        /// <summary>
        /// Gets the X coordinate of this point on the terrain grid.
        /// </summary>
        public readonly int X { get => m_X; }

        private int m_Z;
        /// <summary>
        /// Gets the Z coordinate of this point on the terrain grid.
        /// </summary>
        public readonly int Z { get => m_Z; }


        #region Constructors

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used with the position of the point in the scene.
        /// </summary>
        /// <param name="position">The position of the point in the scene.</param>
        public TerrainPoint(Vector3 position)
            : this(Mathf.RoundToInt(position.x / Terrain.Instance.UnitsPerTileSide), 
                   Mathf.RoundToInt(position.z / Terrain.Instance.UnitsPerTileSide)) { }

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used with the point coordinates on the terrain grid as a tuple.
        /// </summary>
        /// <param name="point">The coordinates of the point on the grid.</param>
        public TerrainPoint((int x, int z) point) : this(point.x, point.z) { }

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used with the point coordinates on the terrain grid.
        /// </summary>
        /// <param name="x">The x coordinate of the point on the grid.</param>
        /// <param name="z">The z coordinate of the point on the grid.</param>
        public TerrainPoint(int x, int z)
        {
            m_X = x;
            m_Z = z;
        }

        #endregion


        #region Check Methods

        /// <summary>
        /// Checks whether the point is in the terrain.
        /// </summary>
        /// <returns>True if the point is within the bounds of the terrain, false otherwise.</returns>
        public readonly bool IsInBounds()
            => m_X >= 0 && m_X <= Terrain.Instance.TilesPerSide && m_Z >= 0 && m_Z <= Terrain.Instance.TilesPerSide;

        /// <summary>
        /// Checks whether the point is on the edge of the terrain.
        /// </summary>
        /// <returns>True if the point is on the edge of the terrain, false otherwise.</returns>
        public readonly bool IsOnEdge()
            => m_X == 0 || m_X == Terrain.Instance.TilesPerSide || m_Z == 0 || m_Z == Terrain.Instance.TilesPerSide;

        /// <summary>
        /// Checks whether the point is the last point on either axis of the terrain grid.
        /// </summary>
        /// <returns>True if this is the last point either on the X axis or the Z axis of the terrain grid.</returns>
        public readonly bool IsLastPoint()
            => m_X == Terrain.Instance.TilesPerSide || m_Z == Terrain.Instance.TilesPerSide;

        /// <summary>
        /// Checks whether the point has reached the maximum possible terrain height.
        /// </summary>
        /// <returns>True if the height of the point is the maximum possible height, false otherwise.</returns>
        public readonly bool IsAtMaxHeight() => GetHeight() == Terrain.Instance.MaxHeight;

        /// <summary>
        /// Checks whether the given point is in the water area of the terrain.
        /// </summary>
        /// <remarks>Note: Points on the shore belong to the land area of the terrain, although their height is the water level.</remarks>
        /// <returns>True if the point is underwater, false otherwise.</returns>
        public readonly bool IsUnderwater()
        {
            if (GetHeight() > Terrain.Instance.WaterLevel)
                return false;

            foreach (TerrainPoint neighbor in GetAllNeighbors())
                if (neighbor.GetHeight() > Terrain.Instance.WaterLevel)
                    return false;

            return true;
        }

        #endregion


        #region Get Methods

        /// <summary>
        /// Gets the height of this point on the terrain.
        /// </summary>
        public readonly int GetHeight() => Terrain.Instance.GetChunkByIndex(GetChunkIndex()).GetPointHeight(this);

        /// <summary>
        /// Gets the index of the chunk that this point is located in.
        /// </summary>
        /// <remarks>A point that's on the edge of a chunk belongs to multiple chunks, but this method only returns one.</remarks>
        /// <returns>The coordinates of the chunk in the terrain chunk grid.</returns>
        public readonly (int X, int Z) GetChunkIndex()
            => (m_X == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : m_X / Terrain.Instance.TilesPerChunkSide,
                m_Z == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : m_Z / Terrain.Instance.TilesPerChunkSide);

        /// <summary>
        /// Gets all the terrain points that neighbor the point.
        /// </summary>
        /// <returns>Yields a <c>TerrainPoint</c> that neighbors this point.</returns>
        public readonly IEnumerable<TerrainPoint> GetAllNeighbors()
        {
            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    TerrainPoint point = new(m_X + x, m_Z + z);
                    if (point.IsInBounds())
                        yield return point;
                }
            }
        }

        /// <summary>
        /// Gets the indices of all the terrain chunks the point is in.
        /// </summary>
        /// <returns>Yields the coordinates of a terrain chunk that this point is in.</returns>
        public readonly IEnumerable<(int X, int Z)> GetAllChunkIndices()
        {
            (int x, int z) chunk = GetChunkIndex();

            // coordinates of this point relative to the chunk
            (int x, int z) pointInChunk = (m_X - chunk.x * Terrain.Instance.TilesPerChunkSide, m_Z - chunk.z * Terrain.Instance.TilesPerChunkSide);

            yield return chunk;

            // bottom left of the current chunk
            if (pointInChunk == (0, 0) && chunk.x > 0 && chunk.z > 0)
                yield return (chunk.x - 1, chunk.z - 1);

            // bottom right of the current chunk
            if (pointInChunk == (Terrain.Instance.TilesPerChunkSide, 0) && chunk.x < Terrain.Instance.ChunksPerSide - 1 && chunk.z > 0)
                yield return (chunk.x + 1, chunk.z - 1);

            // top left of the current chunk
            if (pointInChunk == (0, Terrain.Instance.TilesPerChunkSide) && chunk.x > 0 && chunk.z < Terrain.Instance.ChunksPerSide - 1)
                yield return (chunk.x - 1, chunk.z + 1);

            // top right of the current chunk
            if (pointInChunk == (Terrain.Instance.TilesPerChunkSide, Terrain.Instance.TilesPerChunkSide) &&
                chunk.x < Terrain.Instance.ChunksPerSide - 1 && chunk.z < Terrain.Instance.ChunksPerSide - 1)
                yield return (chunk.x + 1, chunk.z + 1);

            // left
            if (pointInChunk.x == 0 && chunk.x > 0)
                yield return (chunk.x - 1, chunk.z);

            // right
            if (pointInChunk.x == Terrain.Instance.TilesPerChunkSide && chunk.x < Terrain.Instance.ChunksPerSide - 1)
                yield return (chunk.x + 1, chunk.z);

            // bottom
            if (pointInChunk.z == 0 && chunk.z > 0)
                yield return (chunk.x, chunk.z - 1);

            // top
            if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && chunk.z < Terrain.Instance.ChunksPerSide - 1)
                yield return (chunk.x, chunk.z + 1);
        }

        /// <summary>
        /// Gets all points that are a given number of tiles away from this point.
        /// </summary>
        /// <param name="distance">The distance in tiles from the center to the points that should be returned.</param>
        /// <returns>Yields a <c>TerrainPoint</c> that is the given distance away from this point.</returns>
        public readonly IEnumerable<TerrainPoint> GetAllPointsAtDistance(int distance)
        {
            (int x, int z) = (m_X - distance, m_Z - distance);
            (int dx, int dz) = (1, 0);

            for (int side = 0; side < 4; ++side)
            {
                for (int i = 0; i < 2 * distance; ++i)
                {
                    TerrainPoint point = new(x, z);

                    if (point.IsInBounds())
                        yield return point;

                    (x, z) = (x + dx, z + dz);
                }

                (dx, dz) = (-dz, dx);
            }
        }

        public readonly int GetStepsByFaction(Faction faction) => UnitManager.Instance.GetStepsAtPoint(faction, this);

        #endregion


        #region Utility Methods

        /// <summary>
        /// Gets the coordinates of the <c>TerrainPoint</c> in the game scene.
        /// </summary>
        /// <returns>A <c>Vector3</c> of the scene position.</returns>
        public readonly Vector3 ToScenePosition()
            => new(m_X * Terrain.Instance.UnitsPerTileSide, GetHeight(), m_Z * Terrain.Instance.UnitsPerTileSide);

        /// <summary>
        /// Gets a string representation of the <c>TerrainPoint</c>.
        /// </summary>
        /// <returns>A <c>string</c> representation of the <c>TerrainPoint</c>.</returns>
        public override readonly string ToString() => $"Terrain Point -> X: {m_X}, Z: {m_Z}";

        /// <summary>
        /// Tests whether two <c>TerrainPoint</c>s are equal to each other.
        /// </summary>
        /// <param name="a">The first <c>TerrainPoint</c> operand.</param>
        /// <param name="b">The second <c>TerrainPoint</c> operand.</param>
        /// <returns>True if the two operands are equal to each other, false otherwise.</returns>
        public static bool operator ==(TerrainPoint a, TerrainPoint b) => a.Equals(b);

        /// <summary>
        /// Tests whether two <c>TerrainPoint</c>s are not equal to each other.
        /// </summary>
        /// <param name="a">The first <c>TerrainPoint</c> operand.</param>
        /// <param name="b">The second <c>TerrainPoint</c> operand.</param>
        /// <returns>True if the two operands are not equal to each other, false otherwise.</returns>
        public static bool operator !=(TerrainPoint a, TerrainPoint b) => !a.Equals(b);

        /// <summary>
        /// Tests whether an object is equal to this <c>TerrainPoint</c>.
        /// </summary>
        /// <param name="obj">The object which is being compared against this <c>TerrainPoint</c>.</param>
        /// <returns>True if the object is a <c>TerrainPoint</c> and is equal to this <c>TerrainPoint</c>, false otherwise.</returns>
        public override readonly bool Equals(object obj) 
            => obj.GetType() == typeof(TerrainPoint) && Equals((TerrainPoint)obj);

        /// <summary>
        /// Tests whether a <c>TerrainPoint</c> is equal to this <c>TerrainPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <param name="other">The <c>TerrainPoint</c> instance which is being compared against this <c>TerrainPoint</c>.</param>
        /// <returns>True if the coordinates of the <c>TerrainPoint</c>s are equal, false otherwise.</returns>
        public readonly bool Equals(TerrainPoint other) => m_X == other.X && m_Z == other.Z;

        /// <summary>
        /// Gets the hash code for the current <c>TerrainPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <returns>The integer hash code of the current <c>TerrainPoint</c>.</returns>
        public override readonly int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Serializes the <c>TerrainPoint</c> coordinates.
        /// </summary>
        /// <remarks>Interface member of <c>INetworkSerializable</c>.</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer">The <c>BufferSerializer</c> to be used in the serialization.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_X);
            serializer.SerializeValue(ref m_Z);
        }

        #endregion
    }
}