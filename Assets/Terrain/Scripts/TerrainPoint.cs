using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;
using System.Drawing;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainPoint</c> struct represents one point on the terrain grid.
    /// </summary>
    /// <remarks>It is also used to represent a tile on the terrain, where a tile is defined by it's bottom-left point.</remarks>
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

        public readonly bool IsInBounds 
        { 
            get => m_X >= 0 && m_X <= Terrain.Instance.TilesPerSide && m_Z >= 0 && m_Z <= Terrain.Instance.TilesPerSide; 
        }



        private readonly (int x, int z) m_Chunk;


        /// <summary>
        /// Gets the height of this point on the terrain.
        /// </summary>
        public readonly int Height { get => Terrain.Instance.GetPointHeight(m_Chunk, (m_X, m_Z)); }



        private List<TerrainPoint> m_Neighbors;
        /// <summary>
        /// Gets the list of neighboring points of this point if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<TerrainPoint> Neighbors
        {
            get
            {
                m_Neighbors ??= GetAllPointNeighbors();
                return m_Neighbors;
            }
        }

        /// <summary>
        /// True if the point is on the edge of the terrain, false otherwise.
        /// </summary>
        public readonly bool IsOnEdge 
        { 
            get => m_X == 0 || m_X == Terrain.Instance.TilesPerSide || m_Z == 0 || m_Z == Terrain.Instance.TilesPerSide; 
        }

        /// <summary>
        /// True if this is the last point either on the X axis or the Z axis of the terrain grid.
        /// </summary>
        public readonly bool IsLastPoint
        {
            get => m_X == Terrain.Instance.TilesPerSide || m_Z == Terrain.Instance.TilesPerSide;
        }


        #region Constructors

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used when the coordinates of the point on the grid need to be computed from a world position.
        /// </summary>
        /// <param name="x">The x coordinate of the world position.</param>
        /// <param name="z">The y coordinate of the world position.</param>
        /// <param name="getClosestPoint">True if the created point should be the closest grid point to the given position, 
        /// false if the point should represent the tile the unit is on.</param>
        public TerrainPoint(float x, float z, bool getClosestPoint = true)
            : this(
                  getClosestPoint 
                    ? Mathf.RoundToInt(x / Terrain.Instance.UnitsPerTileSide) 
                    : Mathf.Clamp(Mathf.FloorToInt(x / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide),
                  getClosestPoint 
                    ? Mathf.RoundToInt(z / Terrain.Instance.UnitsPerTileSide) 
                    : Mathf.Clamp(Mathf.FloorToInt(z / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide)
              ) { }

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used when we have a tuple of the point's position on the grid.
        /// </summary>
        /// <param name="gridPoint">The x coordinate of the world position.</param>
        public TerrainPoint((int x, int z) gridPoint) : this(gridPoint.x, gridPoint.z) { }

        /// <summary>
        /// A constructor for <c>TerrainPoint</c>, to be used when with the point's position on the grid.
        /// </summary>
        /// <param name="x">The x coordinate of the point on the grid.</param>
        /// <param name="z">The z coordinate of the point on the grid.</param>
        public TerrainPoint(int x, int z)
        {
            m_X = x;
            m_Z = z;

            m_Chunk = (
                x == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : x / Terrain.Instance.TilesPerChunkSide,
                z == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : z / Terrain.Instance.TilesPerChunkSide
            );

            m_Neighbors = null;
        }

        #endregion


        #region Conversion

        /// <summary>
        /// Gets the coordinates of the <c>TerrainPoint</c> in the game scene.
        /// </summary>
        /// <returns>A <c>Vector3</c> of the world position.</returns>
        public readonly Vector3 ToWorldPosition() 
            => new(m_X * Terrain.Instance.UnitsPerTileSide, Height, m_Z * Terrain.Instance.UnitsPerTileSide);

        /// <summary>
        /// Gets a string representation of the <c>TerrainPoint</c>.
        /// </summary>
        /// <returns>A <c>string</c> representation of the <c>TerrainPoint</c>.</returns>
        public override readonly string ToString() => $"MapPoint -> X: {m_X}, Z: {m_Z}";

        #endregion


        #region Equality

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
        public override readonly bool Equals(object obj) => obj.GetType() == typeof(TerrainPoint) && Equals((TerrainPoint)obj);

        #endregion


        #region Interface members

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






        #region Methods


        /// <summary>
        /// Checks whether the given point is in the water area of the terrain.
        /// </summary>
        /// <remarks>Note: Points on the shore belong to the land area of the terrain, although their height is the water level.</remarks>
        /// <param name="point">The <c>TerrainPoint</c> that should be checked.</param>
        /// <returns>True if the point is underwater, false otherwise.</returns>
        public readonly bool IsUnderwater()
        {
            if (Height > Terrain.Instance.WaterLevel)
                return false;

            //foreach (var neighbor in Neighbors)
            //    if (neighbor.Height > 0)
            //        return false;

            return true;
        }

        public readonly IEnumerable<TerrainPoint> GetNeighbors()
        {
            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    TerrainPoint point = new(m_X + x, m_Z + z);
                    if (!point.IsInBounds) continue;

                    yield return point;
                }
            }
        }


        /// <summary>
        /// Gets all the point which neighbor this point.
        /// </summary>
        /// <returns>A list of (x, z) indices of the points that neighbor this point.</returns>
        private readonly List<TerrainPoint> GetAllPointNeighbors()
        {
            List<TerrainPoint> neighbors = new();

            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    TerrainPoint point = new(m_X + x, m_Z + z);
                    if (!point.IsInBounds) continue;

                    neighbors.Add(point);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Gets the corner of the tile represented by this point that is closest to the given point.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> from which the distance should be calculated.</param>
        /// <returns></returns>
        public readonly TerrainPoint GetClosestTileCorner(TerrainPoint start)
        {
            TerrainPoint? closest = null;
            float distance = float.MaxValue;

            for (int z = 0; z <= 1; ++z)
            {
                for (int x = 0; x <= 1; ++x)
                {
                    TerrainPoint corner = new(X + x, Z + z);
                    float d = Vector3.Distance(start.ToWorldPosition(), corner.ToWorldPosition());

                    if (d < distance)
                    {
                        closest = corner;
                        distance = d;
                    }
                }
            }

            return closest.Value;
        }

        #endregion
    }
}