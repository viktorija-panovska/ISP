using System;
using Unity.Netcode;

namespace Populous
{
    public struct Tile : IEquatable<Tile>, INetworkSerializable
    {
        private int m_X;
        private int m_Z;

        public readonly int X { get => m_X; }
        public readonly int Z { get => m_Z; }


        private MapPoint[] m_Corners;

        public bool Equals(Tile other)
            => m_X == other.X && m_Z == other.Z;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            
        }
    }
}