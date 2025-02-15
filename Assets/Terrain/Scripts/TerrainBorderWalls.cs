using System;
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
        private enum WallSide { Left, Right, Top, Bottom }

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
            WallSide[] directions = (WallSide[])Enum.GetValues(typeof(WallSide));

            for (int i = 0; i < m_Walls.Length; ++i) 
            {
                m_Walls[i] = new GameObject();
                m_Walls[i].AddComponent<MeshFilter>();
                m_Walls[i].AddComponent<MeshRenderer>();
                m_Walls[i].transform.SetParent(transform);
                m_Walls[i].name = $"{directions[i]} Wall";

                m_WallData[i] = GenerateWallData(directions[i]);
                m_WallData[i].SetMesh(m_Walls[i], m_WallMaterial);
                PositionWall(m_Walls[i].transform, directions[i]);
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
                        MeshProperties.VertexOffsets[i].z * GetHeightForWallPoint(point, side)
                    ));
                }

                meshData.AddTriangle(triangleIndex,
                    vertexIndex + MeshProperties.TriangleIndices_Standard[0],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.Bottom || side == WallSide.Right ? 1 : 2],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.Bottom || side == WallSide.Right ? 2 : 1]
                );

                meshData.AddTriangle(triangleIndex + 3,
                    vertexIndex + MeshProperties.TriangleIndices_Standard[3],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.Bottom || side == WallSide.Right ? 4 : 5],
                    vertexIndex + MeshProperties.TriangleIndices_Standard[side == WallSide.Bottom || side == WallSide.Right ? 5 : 4]
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
                case WallSide.Left:
                    transform.Rotate(-90, 0, -90);
                    break;

                case WallSide.Right:
                    transform.Rotate(-90, 0, -90);
                    transform.position = new Vector3(Terrain.Instance.UnitsPerSide, 0, 0);
                    break;

                case WallSide.Top:
                    transform.Rotate(-90, 0, 0);
                    transform.position = new Vector3(0, 0, Terrain.Instance.UnitsPerSide);
                    break;

                case WallSide.Bottom:
                    transform.Rotate(-90, 0, 0);
                    break;
            }
        }

        #endregion


        #region Modify Border Walls

        /// <summary>
        /// Modifies the heights of the points on all four walls.
        /// </summary>
        public void ModifyAllWalls()
        {
            for (int z = 0; z <= Terrain.Instance.TilesPerSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerSide; ++x)
                {
                    TerrainPoint point = new(x, z);
                    if (point.IsOnEdge)
                        ModifyWallAtPoint(point);
                }
            }
        }


        /// <summary>
        /// Modifies the shape of the border walls in response to the changing height of a point on the edge of the terrain.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> on the terrain whose height has been changed.</param>
        public void ModifyWallAtPoint(TerrainPoint point)
        {
            if (!point.IsOnEdge) return;

            int height = point.Y >= Terrain.Instance.WaterLevel ? point.Y : Terrain.Instance.WaterLevel;

            if (point.GridX == 0)
                ChangePointHeight(WallSide.Left, point.GridZ, height);

            if (point.GridX == Terrain.Instance.TilesPerSide)
                ChangePointHeight(WallSide.Right, point.GridZ, height);

            if (point.GridZ == 0)
                ChangePointHeight(WallSide.Bottom, point.GridX, height);

            if (point.GridZ == Terrain.Instance.TilesPerSide)
                ChangePointHeight(WallSide.Top, point.GridX, height);

            for (int i = 0; i < m_WallData.Length; ++i)
                m_WallData[i].SetMesh(m_Walls[i], m_WallMaterial);
        }


        /// <summary>
        /// Changes the height of a point on the wall in the given direction to the given height.
        /// </summary>
        /// <param name="direction">The direction of the border wall.</param>
        /// <param name="point">The index of the point on the wall whose height should be changed.</param>
        /// <param name="height">The new height for the point.</param>
        private void ChangePointHeight(WallSide direction, int point, int height)
        {
            if (point >= 0 && point < Terrain.Instance.TilesPerSide)
                m_WallData[(int)direction].SetVertexHeight(point * MeshProperties.VERTICES_PER_TILE_STANDARD + 3, height);

            if (point - 1 >= 0)
                m_WallData[(int)direction].SetVertexHeight((point - 1) * MeshProperties.VERTICES_PER_TILE_STANDARD + 2, height);
        }

        #endregion



        /// <summary>
        /// Gets the height of the point on the terrain that corresponds to the given point on the border wall.
        /// </summary>
        /// <param name="point">The index of the point on the border wall for which the height should be returned.</param>
        /// <param name="side">The side of the terrain the border wall is covering.</param>
        /// <returns></returns>
        private int GetHeightForWallPoint(int point, WallSide side)
        {
            int height = 0;

            switch (side)
            {
                case WallSide.Left:
                    height = Terrain.Instance.GetPointHeight((0, point));
                    break;

                case WallSide.Right:
                    height = Terrain.Instance.GetPointHeight((Terrain.Instance.TilesPerSide, point));
                    break;

                case WallSide.Bottom:
                    height = Terrain.Instance.GetPointHeight((point, 0));
                    break;

                case WallSide.Top:
                    height = Terrain.Instance.GetPointHeight((point, Terrain.Instance.TilesPerSide));
                    break;
            }

            return height >= Terrain.Instance.WaterLevel ? height : Terrain.Instance.WaterLevel;
        }
    }
}