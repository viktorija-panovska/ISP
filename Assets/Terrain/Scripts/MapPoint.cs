using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>MapPoint</c> struct represents one point on the grid of the terrain.
    /// </summary>
    /// <remarks>It is also used to represent a tile on the terrain, where a tile is defined by it's bottom-left point.</remarks>
    public struct MapPoint : IEquatable<MapPoint>, INetworkSerializable
    {
        private int m_GridX;
        /// <summary>
        /// Gets the X coordinate of this point on the terrain grid.
        /// </summary>
        public readonly int GridX { get => m_GridX; }

        private int m_GridZ;
        /// <summary>
        /// Gets the Z coordinate of this point on the terrain grid.
        /// </summary>
        public readonly int GridZ { get => m_GridZ; }

        /// <summary>
        /// Gets the X coordinate of the world position of this point.
        /// </summary>
        public readonly int X { get => m_GridX * Terrain.Instance.UnitsPerTileSide; }
        /// <summary>
        /// Gets the Z coordinate of the world position of this point.
        /// </summary>
        public readonly int Z { get => m_GridZ * Terrain.Instance.UnitsPerTileSide; }

        /// <summary>
        /// Gets the height of this point on the terrain.
        /// </summary>
        public readonly int Y
        {
            get
            {
                int y = m_TouchingChunks == null
                        ? Terrain.Instance.GetPointHeight((m_GridX, m_GridZ))
                        : Terrain.Instance.GetPointHeight(m_TouchingChunks[0], (m_GridX, m_GridZ));

                return Mathf.Clamp(y, Terrain.Instance.WaterLevel, Terrain.Instance.MaxHeight);
            }
        }

        private List<(int x, int z)> m_TouchingChunks;
        /// <summary>
        /// Gets the list of chunks which share this point if it is available, creates it then returns it otherwise.
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
        /// Gets the list of neighboring points of this point if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<MapPoint> Neighbors
        {
            get
            {
                m_Neighbors ??= GetAllPointNeighbors();
                return m_Neighbors;
            }
        }

        private List<MapPoint> m_TileCorners;
        /// <summary>
        /// Gets the list of points at the corners of the tile represented by this point if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<MapPoint> TileCorners
        {
            get
            {
                m_TileCorners ??= GetTileCorners();
                return m_TileCorners;
            }
        }

        /// <summary>
        /// True if the point is on the edge of the terrain, false otherwise.
        /// </summary>
        public readonly bool IsOnEdge 
        { 
            get => m_GridX == 0 || m_GridX == Terrain.Instance.TilesPerSide || m_GridZ == 0 || m_GridZ == Terrain.Instance.TilesPerSide; 
        }

        /// <summary>
        /// True if this is the last point either on the X axis or the Z axis of the terrain grid.
        /// </summary>
        public readonly bool IsLastPoint
        {
            get => m_GridX == Terrain.Instance.TilesPerSide || m_GridZ == Terrain.Instance.TilesPerSide;
        }

        /// <summary>
        /// Gets the position of the center of the tile represented by this point.
        /// </summary>
        public readonly Vector3 TileCenter 
        { 
            get => new(
                (m_GridX + 0.5f) * Terrain.Instance.TilesPerSide, 
                Terrain.Instance.GetTileCenterHeight((m_GridX, m_GridZ)),
                (m_GridZ + 0.5f) * Terrain.Instance.TilesPerSide
            ); 
        }


        #region Constructors

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when the coordinates of the point on the grid need to be computed from a world position.
        /// </summary>
        /// <param name="x">The x coordinate of the world position.</param>
        /// <param name="z">The y coordinate of the world position.</param>
        /// <param name="getClosestPoint">True if the created point should be the closest grid point to the given position, 
        /// false if the point should represent the tile the unit is on.</param>
        public MapPoint(float x, float z, bool getClosestPoint)
            : this(
                  getClosestPoint ? Mathf.RoundToInt(x / Terrain.Instance.UnitsPerTileSide) : Mathf.Clamp(Mathf.FloorToInt(x / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide),
                  getClosestPoint ? Mathf.RoundToInt(z / Terrain.Instance.UnitsPerTileSide) : Mathf.Clamp(Mathf.FloorToInt(z / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide)
              ) { }

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when we have a tuple of the point's position on the grid.
        /// </summary>
        /// <param name="gridPoint">The x coordinate of the world position.</param>
        public MapPoint((int x, int z) gridPoint) : this(gridPoint.x, gridPoint.z) { }

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when with the point's position on the grid.
        /// </summary>
        /// <param name="x">The x coordinate of the point on the grid.</param>
        /// <param name="z">The z coordinate of the point on the grid.</param>
        public MapPoint(int x, int z)
        {
            m_GridX = x;
            m_GridZ = z;

            m_TouchingChunks = null;
            m_Neighbors = null;
            m_TileCorners = null;
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
        public readonly bool Equals(MapPoint other) => m_GridX == other.GridX && m_GridZ == other.GridZ;

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
            serializer.SerializeValue(ref m_GridX);
            serializer.SerializeValue(ref m_GridZ);
        }

        #endregion


        #region Methods

        /// <summary>
        /// Gets all the chunks which share this point.
        /// </summary>
        /// <returns>A list of (x, z) indices of the chunks that contain this point.</returns>
        private readonly List<(int x, int z)> GetAllTouchingChunks()
        {
            (int x, int z) mainChunk = Terrain.Instance.GetChunkIndex((m_GridX, m_GridZ));

            List<(int x, int z)> chunks = new() { mainChunk };

            (int x, int z) pointInChunk = Terrain.Instance.GetChunkByIndex(mainChunk).GetPointInChunk((m_GridX, m_GridZ));

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
        /// Gets all the point which neighbor this point.
        /// </summary>
        /// <returns>A list of (x, z) indices of the points that neighbor this point.</returns>
        private readonly List<MapPoint> GetAllPointNeighbors()
        {
            List<MapPoint> neighbors = new();

            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    if (Terrain.Instance.IsPointInBounds((m_GridX + x, m_GridZ + z)))
                        neighbors.Add(new(m_GridX + x, m_GridZ + z));
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Gets the corner of the tile represented by this point that is closest to the given point.
        /// </summary>
        /// <param name="start">The <c>MapPoint</c> from which the distance should be calculated.</param>
        /// <returns></returns>
        public readonly MapPoint GetClosestTileCorner(MapPoint start)
        {
            MapPoint? closest = null;
            float distance = float.MaxValue;

            for (int z = 0; z <= 1; ++z)
            {
                for (int x = 0; x <= 1; ++x)
                {
                    MapPoint corner = new(GridX + x, GridZ + z);
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

        /// <summary>
        /// Gets the points at the corners of the tile represented by this point.
        /// </summary>
        /// <returns>A list of <c>MapPoint</c>s representing the corners of this tile.</returns>
        private readonly List<MapPoint> GetTileCorners()
        {
            List<MapPoint> corners = new();

            for (int z = 0; z <= 1; ++z)
                for (int x = 0; x <= 1; ++x)
                    corners.Add(new(GridX + x, GridZ + z));

            return corners;
        }

        #endregion
    }
}