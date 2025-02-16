namespace Populous
{

    public static class MeshProperties
    {
        /// <summary>
        /// The number of vertices in a single tile of a normal mesh (the tile consists of two triangles with shared vertices)
        /// </summary>
        public const int VERTICES_PER_TILE_STANDARD = 4;
        /// <summary>
        /// The number of vertices in a single tile of a normal mesh (the tile consists of four triangles with duplicate vertices)
        /// </summary>
        public const int VERTICES_PER_TILE_TERRAIN = 12;

        /// <summary>
        /// An array of the offsets of the vertices in a tile.
        /// </summary>
        public static readonly (float x, float z)[] VertexOffsets = new (float x, float z)[]
        {
            (0, 0),         // bottom-left
            (1, 0),         // bottom-right
            (1, 1),         // top-right
            (0, 1),         // top-left
            (0.5f, 0.5f)    // center
        };

        /// <summary>
        /// An array of indices from the <c>VertexOffsets</c> array representing points in a tile, in the order 
        /// in which they need to be inserted in the <c>Triangles</c> array to form a tile of a standard mesh 
        /// (consists of two triangles with shared vertices)
        /// </summary>
        public static readonly int[] TriangleIndices_Standard = new int[]
        {
            0, 2, 1,        // bottom-left, top-right, bottom-right
            0, 3, 2         // bottom-left, top-left, top-right
        };

        /// <summary>
        /// An array of indices from the <c>VertexOffsets</c> array representing points in a tile, in the order 
        /// in which they need to be inserted in the <c>Triangles</c> array to form a tile of the terrain mesh
        /// (consists of four triangles with duplicate vertices).
        /// </summary>
        public static readonly int[] TriangleIndices_Terrain = new int[]
        {
            0, 4, 1,    // bottom left, center, bottom right
            1, 4, 2,    // bottom right, center, top right
            2, 4, 3,    // top right, center, top left
            3, 4, 0     // top left, center, bottom left
        };

        /// <summary>
        /// An array of arrays where each array represents one point of the tile and contains the offsets
        /// which when added to the index of the first vertex of the tile (bottom-left) in the <c>Vertices</c> 
        /// array, get the index of vertices that are sit at that point in the tile.
        /// </summary>
        /// <remarks>Used to find all the vertices that share a point on the tile, in the case of a terrain mesh.</remarks>
        public static readonly int[][] SharedVertexOffsets = new int[5][]
        {
            new int[2] { 0, 11 },        // bottom left
            new int[2] { 2, 3  },        // bottom right
            new int[2] { 8, 9 },         // top left
            new int[2] { 5, 6 },         // top right
            new int[4] { 1, 4, 7, 10 }   // center
        };
    }
}