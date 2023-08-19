using System.Collections.Generic;
using UnityEngine;

public class Helpers
{
    public static int Modulo(int a, int b) => (a % b + b) % b;

    public static int NextArrayIndex(int start, int increment, int arrayLength)
        => (start + increment + arrayLength) % arrayLength;

    public static float Round(float value)
        => (value % 0.5) == 0 ? Mathf.Ceil(value) : Mathf.Round(value);

    public static (WorldLocation location, float distance) GetClosestVertex(Vector3 position, List<WorldLocation> vertices)
    {
        WorldLocation? closest = null;
        float distance = float.MaxValue;

        foreach (WorldLocation vertex in vertices)
        {
            float d = Vector3.Distance(position, new(vertex.X, WorldMap.Instance.GetHeight(vertex), vertex.Z));

            if (d < distance)
            {
                closest = vertex;
                distance = d;
            }
        }

        return (closest.Value, distance);
    }
}
