using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    public readonly struct TerrainTile
    {
        private readonly int m_X;
        public readonly int X { get => m_X; }

        private readonly int m_Z;
        public readonly int Z { get => m_Z; }

        private readonly (int x, int z) m_ChunkIndex;
        public readonly (int x, int z) ChunkIndex { get => m_ChunkIndex; }

        private readonly Vector3 m_CenterPosition;
        public readonly Vector3 CenterPosition { get => m_CenterPosition; }

        private readonly List<TerrainPoint> m_Corners;
        public readonly List<TerrainPoint> Corners 
        {
            get
            {
                if (m_Corners.Count == 0)
                {
                    // add the tile corners if there aren't any
                    for (int dz = 0; dz <= 1; ++dz)
                        for (int dx = 0; dx <= 1; ++dx)
                            m_Corners.Add(new(m_X + dx, m_Z + dz));
                }

                return m_Corners;
            }
        }


        public TerrainTile(int x, int z)
        {
            m_X = x;
            m_Z = z;

            m_ChunkIndex = (0, 0);
            m_CenterPosition = new();
            m_Corners = new();
        }
    }
}