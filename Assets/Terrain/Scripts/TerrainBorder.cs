using System;
using UnityEngine;
using Unity.Netcode;

namespace Populous
{
    /// <summary>
    /// The <c>TerrainBorder</c> class is a <c>MonoBehavior</c> which handles the creation and modification of 
    /// the four walls surrounding the terrain which would prevent the player from seeing under the terrain mesh.
    /// </summary>
    public class TerrainBorder : NetworkBehaviour
    {
        [SerializeField] private Material m_WallMaterial;

        private static TerrainBorder m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static TerrainBorder Instance { get => m_Instance; }

        private enum WallDirection { Left, Right, Top, Bottom }

        private readonly GameObject[] m_Walls = new GameObject[4];
        private readonly MeshData[] m_WallData = new MeshData[4];


        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;
        }


        #region Create Border Walls

        /// <summary>
        /// Creates and positions the four walls that border the terrain.
        /// </summary>
        public void Create()
        {
            WallDirection[] directions = (WallDirection[])Enum.GetValues(typeof(WallDirection));

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
        /// <param name="direction">The direction of the border wall.</param>
        /// <returns>A <c>MeshData</c> instance containing the data necessary to create the wall mesh.</returns>
        private MeshData GenerateWallData(WallDirection direction)
        {
            MeshData meshData = new(Terrain.Instance.UnitsPerSide, 1, isTerrain: false);
            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int x = 0; x < Terrain.Instance.TilesPerSide; ++x)
            {
                for (int i = 0; i < 4; ++i)
                {
                    int point = x + (int)MeshData.VertexOffsets[i].x;
                    meshData.AddVertex(vertexIndex + i, new(
                        point * Terrain.Instance.UnitsPerTileSide,
                        0,
                        MeshData.VertexOffsets[i].z * GetHeightForWallPoint(point, direction)
                    ));
                }

                meshData.AddTriangle(triangleIndex,
                    vertexIndex + MeshData.TriangleIndices_Standard[0],
                    vertexIndex + MeshData.TriangleIndices_Standard[direction == WallDirection.Bottom || direction == WallDirection.Right ? 1 : 2],
                    vertexIndex + MeshData.TriangleIndices_Standard[direction == WallDirection.Bottom || direction == WallDirection.Right ? 2 : 1]
                );

                meshData.AddTriangle(triangleIndex + 3,
                    vertexIndex + MeshData.TriangleIndices_Standard[3],
                    vertexIndex + MeshData.TriangleIndices_Standard[direction == WallDirection.Bottom || direction == WallDirection.Right ? 4 : 5],
                    vertexIndex + MeshData.TriangleIndices_Standard[direction == WallDirection.Bottom || direction == WallDirection.Right ? 5 : 4]
                );

                vertexIndex += 4;
                triangleIndex += 6;
            }

            return meshData;
        }

        /// <summary>
        /// Positions the wall object so that it is properly placed for its direction.
        /// </summary>
        /// <param name="wallTransform">The transform of the wall <c>GameObject</c>.</param>
        /// <param name="direction">The direction of the border wall.</param>
        private void PositionWall(Transform wallTransform, WallDirection direction)
        {
            switch (direction)
            {
                case WallDirection.Left:
                    wallTransform.Rotate(-90, 0, -90);
                    break;

                case WallDirection.Right:
                    wallTransform.Rotate(-90, 0, -90);
                    wallTransform.position = new Vector3(Terrain.Instance.UnitsPerSide, 0, 0);
                    break;

                case WallDirection.Top:
                    wallTransform.Rotate(-90, 0, 0);
                    wallTransform.position = new Vector3(0, 0, Terrain.Instance.UnitsPerSide);
                    break;

                case WallDirection.Bottom:
                    wallTransform.Rotate(-90, 0, 0);
                    break;
            }
        }

        #endregion


        /// <summary>
        /// Gets the height of the point of the terrain matching the given point on the border wall.
        /// </summary>
        /// <param name="point">The index of the point on the wall for which the height should be returned.</param>
        /// <param name="direction">The direction of the border wall.</param>
        /// <returns></returns>
        private int GetHeightForWallPoint(int point, WallDirection direction)
        {
            int height = 0;

            switch (direction)
            {
                case WallDirection.Left:
                    height = Terrain.Instance.GetPointHeight((0, point));
                    break;

                case WallDirection.Right:
                    height = Terrain.Instance.GetPointHeight((Terrain.Instance.TilesPerSide, point));
                    break;

                case WallDirection.Bottom:
                    height = Terrain.Instance.GetPointHeight((point, 0));
                    break;

                case WallDirection.Top:
                    height = Terrain.Instance.GetPointHeight((point, Terrain.Instance.TilesPerSide));
                    break;
            }

            return height >= Terrain.Instance.WaterLevel ? height : Terrain.Instance.WaterLevel;
        }


        #region Modify Border Walls

        /// <summary>
        /// Modifies the shape of the border walls in response to the changing height of a point on the edge of the terrain.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> on the terrain whose height has been changed.</param>
        public void ModifyWall(MapPoint point)
        {
            if (!point.IsOnEdge) return;

            int height = point.Y >= Terrain.Instance.WaterLevel ? point.Y : Terrain.Instance.WaterLevel;

            if (point.GridX == 0)
                ChangePointHeight(WallDirection.Left, point.GridZ, height);

            if (point.GridX == Terrain.Instance.TilesPerSide)
                ChangePointHeight(WallDirection.Right, point.GridZ, height);

            if (point.GridZ == 0)
                ChangePointHeight(WallDirection.Bottom, point.GridX, height);

            if (point.GridZ == Terrain.Instance.TilesPerSide)
                ChangePointHeight(WallDirection.Top, point.GridX, height);

            for (int i = 0; i < m_WallData.Length; ++i)
                m_WallData[i].SetMesh(m_Walls[i], m_WallMaterial);
        }


        /// <summary>
        /// Changes the height of a point on the wall in the given direction to the given height.
        /// </summary>
        /// <param name="direction">The direction of the border wall.</param>
        /// <param name="point">The index of the point on the wall whose height should be changed.</param>
        /// <param name="height">The new height for the point.</param>
        private void ChangePointHeight(WallDirection direction, int point, int height)
        {
            if (point >= 0 && point < Terrain.Instance.TilesPerSide)
                m_WallData[(int)direction].Vertices[point * MeshData.VERTICES_PER_TILE_STANDARD + 3].z = height;

            if (point - 1 >= 0)
                m_WallData[(int)direction].Vertices[(point - 1) * MeshData.VERTICES_PER_TILE_STANDARD + 2].z = height;
        }

        #endregion
    }
}