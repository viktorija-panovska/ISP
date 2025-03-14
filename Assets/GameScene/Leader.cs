using UnityEngine;

namespace Populous
{
    public interface ILeader
    {
        public GameObject GameObject { get; }

        public void SetLeader(bool isLeader);
    }
}