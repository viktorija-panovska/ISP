
using System.Collections.Generic;
using UnityEngine;

public class DestroyedHouse : MonoBehaviour, IHouse
{
    public GameObject Object { get => gameObject; }
    public List<WorldLocation> Vertices { get; private set; }


    public void Initialize(List<WorldLocation> vertices)
    {
        Vertices = vertices;
    }


    public void DestroyHouse(bool spawnDestroyedHouse)
    {
        OldGameController.Instance.DestroyHouse(this, false);
    }
}
