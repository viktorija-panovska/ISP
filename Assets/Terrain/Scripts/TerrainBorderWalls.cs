using System;
using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainBorderWalls</c> class handles the creation and modification of the four walls surrounding the terrain that hide the underside of the terrain.
    /// </summary>
    public class TerrainBorderWalls : MonoBehaviour
    {
        [Tooltip("A black-colored material for the border walls.")]
        [SerializeField] private Material m_WallMaterial;

        private static TerrainBorderWalls m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static TerrainBorderWalls Instance { get => m_Instance; }

        /// <summary>
        /// The side of the terrain that the wall is covering.
        /// </summary>
        private enum WallSide { LEFT, RIGHT, TOP, BOTTOM }

        /// <summary>
        /// An array containing the GameObjects of each of the four walls.
        /// </summary>
        /// <remarks>Index 0 holds the left wall, 1 the right wall, 2 the top wall, and 3 the bottom wall. Can be indexed with the <c>WallSide</c> enum.</remarks>
        private readonly GameObject[] m_Walls = new GameObject[4];
        /// <summary>
        /// An array containing the MeshData of each of the four walls.
        /// </summary>
        /// <remarks>Index 0 holds the left wall, 1 the right wall, 2 the top wall, and 3 the bottom wall. Can be indexed with the <c>WallSide</c> enum.</remarks>
        private readonly MeshData[] m_WallData = new MeshData[4];


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }


        #region Create Border Walls

        /// <summary>
        /// Creates and positions the four walls that border the terrain.
        /// </summary>
        public void Create()
        {
            WallSide[] sides = (WallSide[])Enum.GetValues(typeof(WallSide));

            for (int i = 0; i < m_Walls.Length; ++i) 
            {
                m_Walls[i] = new GameObject();
                m_Walls[i].AddComponent<MeshFilter>();
                m_Walls[i].AddComponent<MeshRenderer>();
                m_Walls[i].transform.SetParent(transform);
                m_Walls[i].name = $"{sides[i]} Wall";

                m_WallData[i] = GenerateWallData(sides[i]);
                m_WallData[i].SetMesh(m_Walls[i], m_WallMaterial);
                PositionWall(m_Walls[i].transform, sides[i]);
            }
        }

        /// <summary>
        /// Generates all the required data to construct the mesh of the wall that lies in the given direction.
        /// </summary>
        /// <param name="side">The side of the terrain that the border wall should cover.</param>
        /// <returns>A <c>MeshData</c> instance containing the data necessary to create the wall mesh.</returns>
        private MeshData GenerateWallData(WallSide side)
        {
            MeshData meshData = new(Terrain.Instance.UnitsPerSide, 1, isTerrain: false);
            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int x = 0; x < Terrain.Instance.TilesPerSide; ++x)
            {
                for (int i = 0; i < 4; ++i)
                {
                    int point = x + (int)MeshProperties.VertexOffsets[i].x;
                    meshData.AddVertex(vertexIndex + i, new(
                        point * Terrain.Instance.UnitsPerTileSide,
                        0,
                        MeshProperties.VertexOffsets[i].z * GetHeightForWallPoint(side, point)
                    ));
                }

                meshData.AddTriangle(triangleIndex,
                    vertexIndex + MeshProperties.TriangleIndices_Standard[0],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.BOTTOM || side == WallSide.RIGHT ? 1 : 2],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.BOTTOM || side == WallSide.RIGHT ? 2 : 1]
                );

                meshData.AddTriangle(triangleIndex + 3,
                    vertexIndex + MeshProperties.TriangleIndices_Standard[3],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.BOTTOM || side == WallSide.RIGHT ? 4 : 5],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.BOTTOM || side == WallSide.RIGHT ? 5 : 4]
                );

                vertexIndex += 4;
                triangleIndex += 6;
            }

            return meshData;
        }

        /// <summary>
        /// Positions the wall object so that it properly covers the side of the terrain it should cover.
        /// </summary>
        /// <param name="transform">The transform of the wall's <c>GameObject</c>.</param>
        /// <param name="side">The side of the terrain that the wall should be covering..</param>
        private void PositionWall(Transform transform, WallSide side)
        {
            switch (side)
            {
                case WallSide.LEFT:
                    transform.Rotate(-90, 0, -90);
                    break;

                case WallSide.RIGHT:
                    transform.Rotate(-90, 0, -90);
                    transform.position = new Vector3(Terrain.Instance.UnitsPerSide, 0, 0);
                    break;

                case WallSide.TOP:
                    transform.Rotate(-90, 0, 0);
                    transform.position = new Vector3(0, 0, Terrain.Instance.UnitsPerSide);
                    break;

                case WallSide.BOTTOM:
                    transform.Rotate(-90, 0, 0);
                    break;
            }
        }

        #endregion


        #region Modify Border Walls

        /// <summary>
        /// Updates the heights of the points on all four walls.
        /// </summary>
        public void UpdateAllWalls()
        {
            WallSide[] sides = (WallSide[])Enum.GetValues(typeof(WallSide));

            foreach (WallSide side in sides)
                for (int i = 0; i < Terrain.Instance.TilesPerSide; ++i)
                    ChangePointHeight(side, i, GetHeightForWallPoint(side, i));

            for (int i = 0; i < m_WallData.Length; ++i)
                m_WallData[i].SetMesh(m_Walls[i], m_WallMaterial);
        }

        /// <summary>
        /// Updates the heights of the points that fall within the area bordered by the given points.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        public void ModifyWallsInArea(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            if (bottomLeft.X == 0)
            {
                // from bottom z to top z along the left wall
                for (int z = bottomLeft.Z; z <= topRight.Z; ++z)
                    ChangePointHeight(WallSide.LEFT, z, GetHeightForWallPoint(WallSide.LEFT, z));

                m_WallData[(int)WallSide.LEFT].SetMesh(m_Walls[(int)WallSide.LEFT], m_WallMaterial);
            }

            if (bottomLeft.Z == 0)
            {
                // from bottom x to top x along the bottom wall
                for (int x = bottomLeft.X; x <= topRight.X; ++x)
                    ChangePointHeight(WallSide.BOTTOM, x, GetHeightForWallPoint(WallSide.BOTTOM, x));

                m_WallData[(int)WallSide.BOTTOM].SetMesh(m_Walls[(int)WallSide.BOTTOM], m_WallMaterial);
            }

            if (topRight.X == Terrain.Instance.TilesPerSide)
            {
                // from bottom z to top z along the right wall
                for (int z = bottomLeft.Z; z <= topRight.Z; ++z)
                    ChangePointHeight(WallSide.RIGHT, z, GetHeightForWallPoint(WallSide.RIGHT, z));

                m_WallData[(int)WallSide.RIGHT].SetMesh(m_Walls[(int)WallSide.RIGHT], m_WallMaterial);
            }

            if (topRight.Z == Terrain.Instance.TilesPerSide)
            {
                // from bottom x to top x along the top wall
                for (int x = bottomLeft.X; x <= topRight.X; ++x)
                    ChangePointHeight(WallSide.TOP, x, GetHeightForWallPoint(WallSide.TOP, x));

                m_WallData[(int)WallSide.TOP].SetMesh(m_Walls[(int)WallSide.TOP], m_WallMaterial);
            }
        }

        /// <summary>
        /// Changes the height of a point on the wall that covers the given side of the terrain to the given height..
        /// </summary>
        /// <param name="side">The side of the terrain that the wall covers.</param>
        /// <param name="point">The index of the point on the wall whose height should be changed.</param>
        /// <param name="height">The new height for the point.</param>
        private void ChangePointHeight(WallSide side, int point, int height)
        {
            if (point < 0 || point > Terrain.Instance.TilesPerSide)
                return;

            // vertices that need to be modified
            List<int> vertices = new();

            if (point >= 0 && point < Terrain.Instance.TilesPerSide)
                vertices.Add(point * MeshProperties.VERTICES_PER_TILE_STANDARD + 3);

            if (point - 1 >= 0)
                vertices.Add((point - 1) * MeshProperties.VERTICES_PER_TILE_STANDARD + 2);

            foreach (int vertex in vertices)
            {
                Vector3 oldPosition = m_WallData[(int)side].GetVertexPosition(vertex);
                m_WallData[(int)side].SetVertexPosition(vertex, new(oldPosition.x, oldPosition.y, height));
            }
        }

        #endregion


        /// <summary>
        /// Gets the height of the point on the terrain that corresponds to the given point on the border wall.
        /// </summary>
        /// <param name="side">The side of the terrain the border wall is covering.</param>
        /// <param name="point">The index of the point on the border wall for which the height should be returned.</param>
        /// <returns></returns>
        private int GetHeightForWallPoint(WallSide side, int point)
        {
            int height = 0;

            switch (side)
            {
                case WallSide.LEFT:
                    height = Terrain.Instance.GetPointHeight(new(0, point));
                    break;

                case WallSide.RIGHT:
                    height = Terrain.Instance.GetPointHeight(new(Terrain.Instance.TilesPerSide, point));
                    break;

                case WallSide.BOTTOM:
                    height = Terrain.Instance.GetPointHeight(new(point, 0));
                    break;

                case WallSide.TOP:
                    height = Terrain.Instance.GetPointHeight(new(point, Terrain.Instance.TilesPerSide));
                    break;
            }

            return height >= Terrain.Instance.WaterLevel ? height : Terrain.Instance.WaterLevel;
        }

    }
}