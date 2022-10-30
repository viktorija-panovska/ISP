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
        => $"{location.X}{location.Y}{location.Z}";


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
            (int x, int y, int z) = (currentNode.Location.X + dx, currentNode.Location.Y + dy, currentNode.Location.Z + dz);

            if (x < 0 || x >= Chunk.Width * WorldMap.ChunkNumber ||
                y < 0 || y >= Chunk.Height ||
                z < 0 || z >= Chunk.Width * WorldMap.ChunkNumber)
                continue;

            WorldLocation newLocation = new WorldLocation(x, y, z);

            string key = GetKey(newLocation);

            if (!allNodes.ContainsKey(key))
                allNodes.Add(key, new PathNode(newLocation, prevNode: currentNode));
                
            neighbors.Add(key);
        }

        return neighbors;
    }


    private static bool IsSpaceWalkable(WorldLocation location)
    {
        if (location.BlockIndex.y == 0)
            return false;

        Chunk chunk = WorldMap.Instance.GetChunkAtIndex(location.ChunkIndex.x, location.ChunkIndex.z);
        BlockType blockType = chunk.GetBlockAtIndex(location.BlockIndex.x, location.BlockIndex.y, location.BlockIndex.z);

        if (blockType == BlockType.None || blockType == BlockType.Air || blockType == BlockType.Water || 
           (blockType == BlockType.Grass && chunk.GetBlockAtIndex(location.BlockIndex.x, location.BlockIndex.y + 1, location.BlockIndex.z) == BlockType.Grass))
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
