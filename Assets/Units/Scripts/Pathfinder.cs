using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>IPathfinder</c> interface defines methods necessary for classes which implement a pathfinding algorithm for the units.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Finds a sequence of points this unit should travel through to get to the desired target.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that should be reached.</param>
        /// <returns>A list of <c>TerrainPoint</c>s making up the path.</returns>
        public static List<TerrainPoint> FindPath(TerrainPoint start, TerrainPoint end) { return null; }
        /// <summary>
        /// Finds the next point that this unit should go to on its path to follow the given target.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="target">The <c>TerrainPoint</c> that the unit that should be followed is at.</param>
        /// <returns>A <c>TerrainPoint</c> which this unit should go to next, null if no such point is found.</returns>
        public static TerrainPoint? Follow(TerrainPoint start, TerrainPoint target) { return null; }
    }


    /// <summary>
    /// The <c>Pathfinder</c> class handles the pathfinding for the movement of a unit.
    /// </summary>
    public class Pathfinder : IPathfinder
    {
        /// <summary>
        /// The <c>PathNode</c> class represents a node to be used for the A* pathfinding algorithm.
        /// </summary>
        private class PathNode
        {
            /// <summary>
            /// The point on the terrain grid this node represents.
            /// </summary>
            public TerrainPoint Location;
            /// <summary>
            /// The <c>PathNode</c> that we came from.
            /// </summary>
            public PathNode PrevNode;
            /// <summary>
            /// The movement cost to get from the start to this node.
            /// </summary>
            public float GCost;
            /// <summary>
            /// The estimated movement cost to get from this node to the end.
            /// </summary>
            public float HCost;
            /// <summary>
            /// The total estimated movement cost of taking the path through this node.
            /// </summary>
            public float FCost { get => GCost + HCost; }

            /// <summary>
            /// Constructor for the <c>PathNode</c> class.
            /// </summary>
            /// <param name="location">The point on the terrain grid this node represents.</param>
            /// <param name="prevNode">The <c>PathNode</c> that we came from.</param>
            /// <param name="gCost">The movement cost to get from the start to this node.</param>
            /// <param name="hCost">The estimated movement cost to get from this node to the end.</param>
            public PathNode(TerrainPoint location, PathNode prevNode, float gCost = float.MaxValue, float hCost = 0)
            {
                Location = location;
                PrevNode = prevNode;
                GCost = gCost;
                HCost = hCost;
            }
        }


        private const int STRAIGHT_COST = 10;
        private const int DIAGONAL_COST = 14;


        /// <summary>
        /// Finds a sequence of points this unit should travel through to get to the desired target.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that should be reached.</param>
        /// <returns>A list of <c>TerrainPoint</c>s making up the path.</returns>
        public static List<TerrainPoint> FindPath(TerrainPoint start, TerrainPoint end)
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

                if (nodes[current].Location.GridX == end.GridX &&
                    nodes[current].Location.GridZ == end.GridZ)
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


        /// <summary>
        /// Finds the next point that this unit should go to on its path to follow the given target.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="target">The <c>TerrainPoint</c> that the unit that should be followed is at.</param>
        /// <returns>A <c>TerrainPoint</c> which this unit should go to next, null if no such point is found.</returns>
        public static TerrainPoint? Follow(TerrainPoint start, TerrainPoint target)
        {
            TerrainPoint? next = null;
            float minCost = GetDistanceCost(start, target);

            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0)) continue;

                    int x = start.GridX + xOffset;
                    int z = start.GridZ + zOffset;

                    if (x < 0 || x > Terrain.Instance.TilesPerSide || z < 0 || z > Terrain.Instance.TilesPerSide)
                        continue;

                    TerrainPoint newLocation = new(x, z);

                    if (!newLocation.IsOnEdge && !Terrain.Instance.CanCrossTile(start, newLocation))
                        continue;

                    float cost = GetDistanceCost(newLocation, target);

                    if (cost <= minCost)
                    {
                        minCost = cost;
                        next = newLocation;
                    }
                }
            }

            return next;
        }


        private static Vector2 GetKey(TerrainPoint location) => new(location.GridX, location.GridZ);


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

                    int x = currentNode.Location.GridX + xOffset;
                    int z = currentNode.Location.GridZ + zOffset;


                    if (x < 0 || x > Terrain.Instance.TilesPerSide || z < 0 || z > Terrain.Instance.TilesPerSide)
                        continue;

                    TerrainPoint newLocation = new(x, z);

                    if (!Terrain.Instance.CanCrossTile(currentNode.Location, newLocation))
                        continue;

                    Vector2 key = GetKey(newLocation);

                    if (!allNodes.ContainsKey(key))
                        allNodes.Add(key, new PathNode(newLocation, prevNode: currentNode));

                    neighbors.Add(key);
                }
            }

            return neighbors;
        }


        private static float GetDistanceCost(TerrainPoint start, TerrainPoint end)
        {
            float x = Mathf.Abs(start.GridX - end.GridX);
            float z = Mathf.Abs(start.GridZ - end.GridZ);

            return DIAGONAL_COST * Mathf.Min(x, z) + STRAIGHT_COST * Mathf.Abs(x - z);
        }


        private static List<TerrainPoint> GetPath(PathNode endNode)
        {
            List<TerrainPoint> path = new() { endNode.Location };

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
}