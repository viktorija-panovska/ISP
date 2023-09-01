
using System.Collections.Generic;
using UnityEngine;

public class DestroyedHouse : MonoBehaviour, IHouse
{
    public List<WorldLocation> Vertices { get; private set; }


    public void Initialize(List<WorldLocation> vertices)
    {
        Vertices = vertices;
    }


    public void DestroyHouse(bool spawnDestroyedHouse)
    {
        GameController.Instance.DestroyHouse(this, true);
    }
}
