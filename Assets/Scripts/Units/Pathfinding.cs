using System.Collections.Generic;
using UnityEngine;


public class PathNode
{
    public WorldLocation Location;

    public PathNode PrevNode;

    public float GCost;
    public float HCost;
    public float FCost { get => GCost + HCost; }


    public PathNode(WorldLocation location, PathNode prevNode, float gCost = float.MaxValue, float hCost = 0)
    {
        Location = location;

        PrevNode = prevNode;

        GCost = gCost;
        HCost = hCost;
    }
}


public static class Pathfinding
{
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    private static readonly (int, int)[] neighborDirections
        = { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1) };


    public static List<WorldLocation> FindPath(WorldLocation start, WorldLocation end) 
    {
        Dictionary<Vector2, PathNode> nodes = new();
        List<Vector2> openList = new();
        HashSet<Vector2> closedList = new();

        // add start node to open list
        PathNode startNode = new(
            start,
            prevNode: null,
            gCost: 0,
            hCost: GetDistanceCost(start, end)
        );

        Vector2 startKey = GetKey(startNode.Location);
        nodes.Add(startKey, startNode);
        openList.Add(startKey);

        while (openList.Count > 0)
        {
            Vector2 current = GetNodeWithLowestFCost(nodes, openList);

            if (nodes[current].Location.X == end.X && 
                nodes[current].Location.Z == end.Z)
                return GetPath(nodes[current]);

            openList.Remove(current);
            closedList.Add(current);

            foreach (Vector2 neighbor in GetNeighborNodes(nodes[current], ref nodes))
            {
                // node has already been visited
                if (closedList.Contains(neighbor))
                    continue;

                // node cannot be reached
                if (!IsReachable(nodes[current], nodes[neighbor]))
                {
                    closedList.Add(neighbor);
                    continue;
                }

                float gCost = nodes[current].GCost + GetDistanceCost(nodes[current].Location, nodes[neighbor].Location);

                if (gCost < nodes[neighbor].GCost)
                {
                    nodes[neighbor].PrevNode = nodes[current];
                    nodes[neighbor].GCost = gCost;
                    nodes[neighbor].HCost = GetDistanceCost(nodes[neighbor].Location, end);

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }

        return null;
    }


    private static Vector2 GetKey(WorldLocation location) => new(location.X, location.Z);


    private static Vector2 GetNodeWithLowestFCost(Dictionary<Vector2, PathNode> allNodes, List<Vector2> openList)
    {
        Vector2 lowestFCostNode = openList[0];

        for (int i = 1; i < openList.Count; ++i)
            if (allNodes[openList[i]].FCost < allNodes[lowestFCostNode].FCost)
                lowestFCostNode = openList[i];

        return lowestFCostNode;
    }


    private static List<Vector2> GetNeighborNodes(PathNode currentNode, ref Dictionary<Vector2, PathNode> allNodes)
    {
        List<Vector2> neighbors = new();

        foreach ((int xOffset, int zOffset) in neighborDirections)
        {
            float x = currentNode.Location.X + (xOffset * Chunk.TileWidth);
            float z = currentNode.Location.Z + (zOffset * Chunk.TileWidth);

            if (x < 0 || x > WorldMap.Width ||
                z < 0 || z > WorldMap.Width)
                continue;

            WorldLocation newLocation = new(x, z);

            Vector2 key = GetKey(newLocation);

            if (!allNodes.ContainsKey(key))
                allNodes.Add(key, new PathNode(newLocation, prevNode: currentNode));

            neighbors.Add(key);
        }
        return neighbors;
    }


    private static bool IsReachable(PathNode current, PathNode neighbor)
    {
        float currentY = WorldMap.GetVertexHeight(current.Location);
        float neighborY = WorldMap.GetVertexHeight(neighbor.Location);

        return Mathf.Abs(currentY - neighborY) <= Chunk.StepHeight;
    }


    private static float GetDistanceCost(WorldLocation start, WorldLocation end)
    {
        float x = Mathf.Abs(start.X - end.X);
        float z = Mathf.Abs(start.Z - end.Z);

        return DiagonalCost * Mathf.Min(x, z) + StraightCost * Mathf.Abs(x - z);
    }


    private static List<WorldLocation> GetPath(PathNode endNode)
    {
        List<WorldLocation> path = new();
        path.Add(endNode.Location);

        PathNode currentNode = endNode;
        while (currentNode.PrevNode != null)
        {
            path.Add(currentNode.PrevNode.Location);
            currentNode = currentNode.PrevNode;
        }

        path.Reverse();
        return path;
    }
}