using System.Collections.Generic;
using UnityEngine;


public class PathNode
{
    public WorldLocation Location;

    public PathNode PrevNode;

    public int GCost;
    public int HCost;
    public int FCost { get => GCost + HCost; }


    public PathNode(WorldLocation location, PathNode prevNode, int gCost = int.MaxValue, int hCost = 0)
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

    private static readonly (int dx, int dy, int dz)[] neighborDirections 
        = { (0, 0, 1),  (1, 0, 0),  (0, 0, -1),  (-1, 0, 0),
            (0, 1, 1),  (1, 1, 0),  (0, 1, -1),  (-1, 1, 0),
            (0, -1, 1), (1, -1, 0), (0, -1, -1), (-1, -1, 0)};



    public static List<WorldLocation> FindPath(WorldLocation start, WorldLocation end)
    {
        Dictionary<string, PathNode> nodes = new Dictionary<string, PathNode>();
        List<string> openList = new List<string>();
        HashSet<string> closedList = new HashSet<string>();

        // add start node to open list
        PathNode startNode = new PathNode(
            start,
            prevNode: null,
            gCost: 0, 
            hCost: GetDistanceCost(start.X, start.Z, end.X, end.Z)
        );

        string startKey = GetKey(startNode.Location);
        nodes.Add(startKey, startNode);
        openList.Add(startKey);

        while (openList.Count > 0)
        {
            string current = GetNodeWithLowestFCost(nodes, openList);

            if (nodes[current].Location.X == end.X && nodes[current].Location.Z == end.Z)
                return GetPath(nodes[current]);

            openList.Remove(current);
            closedList.Add(current);

            foreach (string neighbor in GetNeighborNodes(nodes[current], ref nodes))
            {
                // node has already been visited
                if (closedList.Contains(neighbor))
                    continue;

                if (!IsSpaceWalkable(nodes[neighbor].Location))
                {
                    closedList.Add(neighbor);
                    continue;
                }

                int gCost = nodes[current].GCost + GetDistanceCost(nodes[current].Location.X, nodes[current].Location.Z, 
                                                                   nodes[neighbor].Location.X, nodes[neighbor].Location.Z);
                
                if (gCost < nodes[neighbor].GCost)
                {
                    nodes[neighbor].PrevNode = nodes[current];
                    nodes[neighbor].GCost = gCost;
                    nodes[neighbor].HCost = GetDistanceCost(nodes[neighbor].Location.X, nodes[neighbor].Location.Z, end.X, end.Z);

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }

        return null;
    }


    private static string GetKey(WorldLocation location)
        => $"{location.Chunk.LocationInMap.x}{location.Chunk.LocationInMap.z}{location.X}{location.Y}{location.Z}";


    private static string GetNodeWithLowestFCost(Dictionary<string, PathNode> allNodes, List<string> openList)
    {
        string lowestFCostNode = openList[0];

        for (int i = 1; i < openList.Count; ++i)
            if (allNodes[openList[i]].FCost < allNodes[lowestFCostNode].FCost)
                lowestFCostNode = openList[i];

        return lowestFCostNode;
    }


    private static List<string> GetNeighborNodes(PathNode currentNode, ref Dictionary<string, PathNode> allNodes)
    {
        List<string> neighbors = new List<string>();

        foreach ((int dx, int dy, int dz) in neighborDirections)
        {
            // this works only for single chunk
            // check for neighboring chunks too

            WorldLocation newLocation = new WorldLocation(currentNode.Location.Chunk, 
                                                          currentNode.Location.X + dx, 
                                                          currentNode.Location.Y + dy, 
                                                          currentNode.Location.Z + dz);
            string key = GetKey(newLocation);

            if (!allNodes.ContainsKey(key))
                allNodes.Add(key, new PathNode(newLocation, prevNode: currentNode));
                
            neighbors.Add(key);
        }

        return neighbors;
    }


    private static bool IsSpaceWalkable(WorldLocation location)
    {
        if (location.Y == 0)
            return false;

        BlockType blockType = location.Chunk.GetBlockAtIndex(location.X, location.Y, location.Z);

        if (blockType == BlockType.None || blockType == BlockType.Air || blockType == BlockType.Water || 
           (blockType == BlockType.Grass && location.Chunk.GetBlockAtIndex(location.X, location.Y + 1, location.Z) == BlockType.Grass))
            return false;

        return true;
    }


    private static int GetDistanceCost(int startX, int startZ, int endX, int endZ)
    {
        int x = Mathf.Abs(startX - endX);
        int z = Mathf.Abs(startZ - endZ);

        return DiagonalCost * Mathf.Min(x, z) + StraightCost * Mathf.Abs(x - z);
    }


    private static List<WorldLocation> GetPath(PathNode endNode)
    {
        List<WorldLocation> path = new List<WorldLocation>();
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
