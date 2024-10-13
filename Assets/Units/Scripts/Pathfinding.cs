using System.Collections.Generic;
using UnityEngine;


namespace Populous
{
    public class PathNode
    {
        public MapPoint Location;

        public PathNode PrevNode;

        public float GCost;
        public float HCost;
        public float FCost { get => GCost + HCost; }


        public PathNode(MapPoint location, PathNode prevNode, float gCost = float.MaxValue, float hCost = 0)
        {
            Location = location;

            PrevNode = prevNode;

            GCost = gCost;
            HCost = hCost;
        }
    }


    public static class Pathfinding
    {
        private const int STRAIGHT_COST = 10;
        private const int DIAGONAL_COST = 14;


        public static List<MapPoint> FindPath(MapPoint start, MapPoint end)
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

                if (nodes[current].Location.TileX == end.TileX &&
                    nodes[current].Location.TileZ == end.TileZ)
                    return GetPath(nodes[current]);

                openList.Remove(current);
                closedList.Add(current);

                foreach (Vector2 neighbor in GetNeighborNodes(nodes[current], ref nodes))
                {
                    // node has already been visited
                    if (closedList.Contains(neighbor))
                        continue;

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


        private static Vector2 GetKey(MapPoint location) => new(location.TileX, location.TileZ);


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

            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0)) continue;

                    int x = currentNode.Location.TileX + xOffset;
                    int z = currentNode.Location.TileZ + zOffset;


                    if (x < 0 || x > Terrain.Instance.TilesPerSide || z < 0 || z > Terrain.Instance.TilesPerSide)
                        continue;

                    MapPoint newLocation = new(x, z);

                    if (!newLocation.IsOnEdge && Terrain.Instance.IsCrossingStructure(currentNode.Location, newLocation))
                        continue;

                    Vector2 key = GetKey(newLocation);

                    if (!allNodes.ContainsKey(key))
                        allNodes.Add(key, new PathNode(newLocation, prevNode: currentNode));

                    neighbors.Add(key);
                }
            }

            return neighbors;
        }


        private static float GetDistanceCost(MapPoint start, MapPoint end)
        {
            float x = Mathf.Abs(start.TileX - end.TileX);
            float z = Mathf.Abs(start.TileZ - end.TileZ);

            return DIAGONAL_COST * Mathf.Min(x, z) + STRAIGHT_COST * Mathf.Abs(x - z);
        }


        private static List<MapPoint> GetPath(PathNode endNode)
        {
            List<MapPoint> path = new() { endNode.Location };

            PathNode currentNode = endNode;
            while (currentNode.PrevNode != null)
            {
                path.Add(currentNode.PrevNode.Location);
                currentNode = currentNode.PrevNode;
            }

            path.Reverse();

            return path;
        }


        public static MapPoint? FollowUnit(MapPoint start, MapPoint unit)
        {
            MapPoint? next = null;
            float minCost = GetDistanceCost(start, unit);

            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0)) continue;

                    int x = start.TileX + xOffset;
                    int z = start.TileZ + zOffset;

                    if (x < 0 || x > Terrain.Instance.TilesPerSide || z < 0 || z > Terrain.Instance.TilesPerSide)
                        continue;

                    MapPoint newLocation = new(x, z);

                    if (!newLocation.IsOnEdge && Terrain.Instance.IsCrossingStructure(start, newLocation))
                        continue;

                    float cost = GetDistanceCost(newLocation, unit);

                    if (cost <= minCost)
                    {
                        minCost = cost;
                        next = newLocation;
                    }
                }
            }

            return next;
        }
    }
}