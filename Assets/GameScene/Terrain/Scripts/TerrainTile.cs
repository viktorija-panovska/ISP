using System;
using System.Collections.Generic;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>TerrainTile</c> struct represents one tile on the terrain grid.
    /// </summary>
    public readonly struct TerrainTile : IEquatable<TerrainTile>
    {
        private readonly int m_X;
        /// <summary>
        /// Gets the X coordinate of this tile on the terrain grid.
        /// </summary>
        public readonly int X { get => m_X; }

        private readonly int m_Z;
        /// <summary>
        /// Gets the Z coordinate of this tile on the terrain grid.
        /// </summary>
        public readonly int Z { get => m_Z; }


        #region Constructors

        /// <summary>
        /// A constructor for <c>TerrainTile</c>, to be used with the position of the tile in the scene.
        /// </summary>
        /// <param name="x">The x coordinate of the scene position.</param>
        /// <param name="z">The y coordinate of the scene position.</param>
        public TerrainTile(float x, float z)
            : this(Mathf.Clamp(Mathf.FloorToInt(x / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide),
                   Mathf.Clamp(Mathf.FloorToInt(z / Terrain.Instance.UnitsPerTileSide), 0, Terrain.Instance.UnitsPerChunkSide)) { }

        /// <summary>
        /// A constructor for <c>TerrainTile</c>, to be used with the tile coordinates on the terrain grid as a tuple.
        /// </summary>
        /// <param name="point">The coordinates of the tile on the grid.</param>
        public TerrainTile((int x, int z) tile) : this(tile.x, tile.z) { }

        /// <summary>
        /// A constructor for <c>TerrainTile</c>, to be used with the tile coordinates on the terrain grid.
        /// </summary>
        /// <param name="x">The x coordinate of the tile on the grid.</param>
        /// <param name="z">The z coordinate of the tile on the grid.</param>
        public TerrainTile(int x, int z)
        {
            m_X = x;
            m_Z = z;
        }

        #endregion


        #region Check Methods

        /// <summary>
        /// Checks whether the tile is in the terrain.
        /// </summary>
        /// <returns>True if the tile is within the bounds of the terrain, false otherwise.</returns>
        public readonly bool IsInBounds()
            => m_X >= 0 && m_X < Terrain.Instance.TilesPerSide && m_Z >= 0 && m_Z < Terrain.Instance.TilesPerSide;

        /// <summary>
        /// Checks whether all the corners of the tile are at the same height.
        /// </summary>
        /// <returns>True if the tile is flat, false otherwise.</returns>
        public readonly bool IsFlat()
        {
            int height = -1;
            // if any corner has a different height, it is not flat
            foreach (TerrainPoint corner in GetCorners())
            {
                int cornerHeight = corner.GetHeight();

                if (height >= 0 && cornerHeight != height)
                    return false;

                height = cornerHeight;
            }

            return true;
        }

        /// <summary>
        /// Checks whether there is a structure on the tile.
        /// </summary>
        /// <returns>True if the tile is occupied, false otherwise.</returns>
        public readonly bool IsOccupied() => GetStructure();

        /// <summary>
        /// Checks whether the tile can be built on.
        /// </summary>
        /// <returns>True if the tile is free, false otherwise.</returns>
        public readonly bool IsFree() => IsFlat() && !IsOccupied();

        /// <summary>
        /// Checks whether all the corner points of the given tile are on or below the water level.
        /// </summary>
        /// <returns>True if the tile is underwater, false otherwise.</returns>
        public readonly bool IsUnderwater()
        {
            // if any corner is above water level, the tile isn't underwater
            foreach (TerrainPoint corner in GetCorners())
                if (corner.GetHeight() > Terrain.Instance.WaterLevel)
                    return false;

            return true;
        }

        /// <summary>
        /// Checks whether there is a settlement on the tile.
        /// </summary>
        /// <returns>True if the tile is occupied by a settlement, false otherwise.</returns>
        public readonly bool HasSettlement() => GetStructure().GetType() == typeof(Settlement);

        #endregion


        #region Get Methods

        /// <summary>
        /// Gets the height of the center of the tile.
        /// </summary>
        /// <returns>The height of the tile center.</returns>
        public readonly float GetCenterHeight() => Terrain.Instance.GetChunkByIndex(GetChunkIndex()).GetTileCenterHeight(this);

        /// <summary>
        /// Gets the position in the scene of the center of the tile.
        /// </summary>
        /// <returns>A <c>Vector3</c> of the position of the tile center.</returns>
        public readonly Vector3 GetCenterPosition() => new(
            (m_X + 0.5f) * Terrain.Instance.UnitsPerTileSide, 
            GetCenterHeight(),
            (m_Z + 0.5f) * Terrain.Instance.UnitsPerTileSide
        );

        /// <summary>
        /// Gets the index of the chunk that this tile is located in.
        /// </summary>
        /// <returns>The coordinates of the chunk in the terrain chunk grid.</returns>
        public readonly (int X, int Z) GetChunkIndex()
            => (m_X / Terrain.Instance.TilesPerChunkSide, m_Z / Terrain.Instance.TilesPerChunkSide);

        /// <summary>
        /// Gets the structure that occupies the tile.
        /// </summary>
        /// <returns>The <c>Structure</c> that occupies this tile if there is one, <c>null</c> otherwise.</returns>
        public readonly Structure GetStructure() => StructureManager.Instance.GetStructureOnTile(this);

        /// <summary>
        /// Gets the corner of the tile that is closest to the given point.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> from which the distance should be calculated.</param>
        /// <returns>The <c>TerrainPoint</c> of the corner of this tile that is closest to the given point.</returns>
        public readonly TerrainPoint GetClosestCorner(TerrainPoint point)
        {
            TerrainPoint? closest = null;
            float distance = float.MaxValue;

            foreach (TerrainPoint corner in GetCorners())
            {
                float d = Vector3.Distance(point.ToScenePosition(), corner.ToScenePosition());

                if (d < distance)
                {
                    closest = corner;
                    distance = d;
                }
            }

            return closest.Value;
        }

        /// <summary>
        /// Gets the terrain points on the corners of the given tile.
        /// </summary>
        /// <returns>Yields a <c>TerrainPoint</c> at the corner of this tile.</returns>
        public readonly IEnumerable<TerrainPoint> GetCorners()
        {
            (int x, int z) bottomLeft = (m_X, m_Z);

            if (bottomLeft.x >= Terrain.Instance.TilesPerSide)
                bottomLeft.x -= 1;

            if (bottomLeft.z >= Terrain.Instance.TilesPerSide)
                bottomLeft.z -= 1;

            for (int z = 0; z <= 1; ++z)
                for (int x = 0; x <= 1; ++x)
                    yield return new(bottomLeft.x + x, bottomLeft.z + z);
        }

        #endregion


        #region Utility Methods

        /// <summary>
        /// Gets a string representation of the <c>TerrainPoint</c>.
        /// </summary>
        /// <returns>A <c>string</c> representation of the <c>TerrainPoint</c>.</returns>
        public override readonly string ToString() => $"TerrainTile -> X: {m_X}, Z: {m_Z}";

        /// <summary>
        /// Tests whether two <c>TerrainTile</c>s are equal to each other.
        /// </summary>
        /// <param name="a">The first <c>TerrainTile</c> operand.</param>
        /// <param name="b">The second <c>TerrainTile</c> operand.</param>
        /// <returns>True if the two operands are equal to each other, false otherwise.</returns>
        public static bool operator ==(TerrainTile a, TerrainTile b) => a.Equals(b);

        /// <summary>
        /// Tests whether two <c>TerrainTile</c>s are not equal to each other.
        /// </summary>
        /// <param name="a">The first <c>TerrainTile</c> operand.</param>
        /// <param name="b">The second <c>TerrainTile</c> operand.</param>
        /// <returns>True if the two operands are not equal to each other, false otherwise.</returns>
        public static bool operator !=(TerrainTile a, TerrainTile b) => !a.Equals(b);

        /// <summary>
        /// Tests whether an object is equal to this <c>TerrainTile</c>.
        /// </summary>
        /// <param name="obj">The object which is being compared against this <c>TerrainTile</c>.</param>
        /// <returns>True if the object is a <c>TerrainTile</c> and is equal to this <c>TerrainTile</c>, false otherwise.</returns>
        public override readonly bool Equals(object obj) => obj.GetType() == typeof(TerrainTile) && Equals((TerrainTile)obj);

        /// <summary>
        /// Tests whether a <c>TerrainTile</c> is equal to this <c>TerrainTile</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <param name="other">The <c>TerrainTile</c> instance which is being compared against this <c>TerrainTile</c>.</param>
        /// <returns>True if the coordinates of the <c>TerrainTile</c>s are equal, false otherwise.</returns>
        public readonly bool Equals(TerrainTile other) => m_X == other.X && m_Z == other.Z;

        /// <summary>
        /// Gets the hash code for the current <c>TerrainTile</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <returns>The integer hash code of the current <c>TerrainTile</c>.</returns>
        public override readonly int GetHashCode() => base.GetHashCode();

        #endregion
    }
}