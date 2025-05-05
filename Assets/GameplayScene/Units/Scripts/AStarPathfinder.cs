using System.Collections.Generic;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>IPathfinder</c> interface defines methods necessary for classes which implement a pathfinding algorithm for the units.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Finds a sequence of points the unit should travel through to get to the desired end.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that the unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that should be reached.</param>
        /// <returns>A list of <c>TerrainPoint</c>s making up the path.</returns>
        public static List<TerrainPoint> FindPath(TerrainPoint start, TerrainPoint end) { return null; }
        /// <summary>
        /// Finds the next point that the unit should go to on its path to the given end.
        /// </summary>
        /// <remarks>Used when following another unit.</remarks>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that is the final destination..</param>
        /// <returns>A <c>TerrainPoint</c> which this unit should go to next, null if no such point is found.</returns>
        public static TerrainPoint? FindNextStep(TerrainPoint start, TerrainPoint end) { return null; }
    }


    /// <summary>
    /// The <c>Pathfinder</c> class handles the pathfinding for the movement of a unit using the A* algorthm.
    /// </summary>
    public class AStarPathfinder : IPathfinder
    {
        /// <summary>
        /// The <c>PathNode</c> class represents a node to be used for the A* pathfinding algorithm.
        /// </summary>
        private class PathNode
        {
            /// <summary>
            /// The point on the terrain grid this node represents.
            /// </summary>
            public TerrainPoint Point;
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
                Point = location;
                PrevNode = prevNode;
                GCost = gCost;
                HCost = hCost;
            }
        }

        /// <summary>
        /// The cost for moving straight from one node to the next.
        /// </summary>
        private const int STRAIGHT_COST = 10;
        /// <summary>
        /// The cost for moving diagonally from one node to the next.
        /// </summary>
        private const int DIAGONAL_COST = 14;

        /// <summary>
        /// Finds a sequence of points the unit should travel through to get to the desired end.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> that the unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that should be reached.</param>
        /// <returns>A list of <c>TerrainPoint</c>s making up the path.</returns>
        public static List<TerrainPoint> FindPath(TerrainPoint start, TerrainPoint end)
        {
            if (!DivineInterventionController.Instance.IsArmageddon && end.IsUnderwater()) return null;

            if (start == end) return new() { end };

            Dictionary<Vector2, PathNode> nodes = new();        // all nodes
            List<Vector2> openList = new();                     // nodes on the frontier - nodes that need to be explored
            HashSet<Vector2> closedList = new();                // nodes that have already been explored

            // add start node to open list
            PathNode startNode = new(start, prevNode: null, gCost: 0, hCost: GetDistanceCost(start, end));
            Vector2 startKey = GetKey(startNode.Point);
            nodes.Add(startKey, startNode);
            openList.Add(startKey);

            while (openList.Count > 0)
            {
                Vector2 current = GetNodeWithLowestFCost(nodes, openList);

                // we found the target, end the algorithm
                if (nodes[current].Point.X == end.X && nodes[current].Point.Z == end.Z)
                    return GetPath(nodes[current]);

                openList.Remove(current);
                closedList.Add(current);

                foreach (Vector2 neighbor in GetNeighborNodes(nodes[current], ref nodes))
                {
                    // node has already been visited
                    if (closedList.Contains(neighbor)) continue;

                    float gCost = nodes[current].GCost + GetDistanceCost(nodes[current].Point, nodes[neighbor].Point);

                    if (gCost < nodes[neighbor].GCost)
                    {
                        nodes[neighbor].PrevNode = nodes[current];
                        nodes[neighbor].GCost = gCost;
                        nodes[neighbor].HCost = GetDistanceCost(nodes[neighbor].Point, end);

                        if (!openList.Contains(neighbor))
                            openList.Add(neighbor);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the next point that the unit should go to on its path to the given end.
        /// </summary>
        /// <remarks>Used when following another unit.</remarks>
        /// <param name="start">The <c>TerrainPoint</c> that this unit is at.</param>
        /// <param name="end">The <c>TerrainPoint</c> that is the final destination.</param>
        /// <returns>A <c>TerrainPoint</c> which this unit should go to next, null if no such point is found.</returns>
        public static TerrainPoint? FindNextStep(TerrainPoint start, TerrainPoint end)
        {
            if (start == end) return end;

            TerrainPoint? next = null;
            float minCost = GetDistanceCost(start, end);

            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0)) continue;

                    int x = start.X + xOffset;
                    int z = start.Z + zOffset;

                    TerrainPoint newLocation = new(x, z);

                    if (!newLocation.IsInBounds() || !UnitMovementHandler.IsTileCrossable(start, newLocation))
                        continue;

                    float cost = GetDistanceCost(newLocation, end);

                    if (cost <= minCost)
                    {
                        minCost = cost;
                        next = newLocation;
                    }
                }
            }

            return next;
        }


        /// <summary>
        /// Creates a key to represent the given terrain point.
        /// </summary>
        /// <param name="location">The given <c>TerrainPoint</c>.</param>
        /// <returns>A <c>Vector2</c> that will serve as the key for that point.</returns>
        private static Vector2 GetKey(TerrainPoint location) => new(location.X, location.Z);

        /// <summary>
        /// Finds the node in the open list that has the lowest estimated cost if a path to the destination is taken through it.
        /// </summary>
        /// <param name="allNodes">A dictionary mapping node keys to <c>PathNodes</c>.</param>
        /// <param name="openList">A list of the node keys of the nodes that are ready for exploration.</param>
        /// <returns>The <c>Vector2</c> node key of the node in the open list with the lowest F-cost.</returns>
        private static Vector2 GetNodeWithLowestFCost(Dictionary<Vector2, PathNode> allNodes, List<Vector2> openList)
        {
            Vector2 lowestFCostNode = openList[0];

            for (int i = 1; i < openList.Count; ++i)
                if (allNodes[openList[i]].FCost < allNodes[lowestFCostNode].FCost)
                    lowestFCostNode = openList[i];

            return lowestFCostNode;
        }

        /// <summary>
        /// Gets all the visitable neighboring points of the point represented by the current node.
        /// </summary>
        /// <param name="currentNode">The current <c>PathNode</c>.</param>
        /// <param name="allNodes"></param>
        /// <returns></returns>
        private static List<Vector2> GetNeighborNodes(PathNode currentNode, ref Dictionary<Vector2, PathNode> allNodes)
        {
            List<Vector2> neighbors = new();

            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0)) continue;

                    int x = currentNode.Point.X + xOffset;
                    int z = currentNode.Point.Z + zOffset;

                    TerrainPoint neighbor = new(x, z);

                    if (!neighbor.IsInBounds() || !UnitMovementHandler.IsTileCrossable(currentNode.Point, neighbor))
                        continue;

                    Vector2 key = GetKey(neighbor);

                    if (!allNodes.ContainsKey(key))
                        allNodes.Add(key, new PathNode(neighbor, prevNode: currentNode));

                    neighbors.Add(key);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Computes the cost of travelling from the given start point to the given end point.
        /// </summary>
        /// <param name="start">The start <c>TerrainPoint</c>.</param>
        /// <param name="end">The end <c>TerrainPoint</c>.</param>
        /// <returns>The cost of movement between the points.</returns>
        private static float GetDistanceCost(TerrainPoint start, TerrainPoint end)
        {
            float x = Mathf.Abs(start.X - end.X);
            float z = Mathf.Abs(start.Z - end.Z);

            return DIAGONAL_COST * Mathf.Min(x, z) + STRAIGHT_COST * Mathf.Abs(x - z);
        }

        /// <summary>
        /// Gets the final list of points representing the path, in the correct order.
        /// </summary>
        /// <param name="endNode">The final <c>PathNode</c>.</param>
        /// <returns>A list of <c>TerrainPoint</c>s representing the path.</returns>
        private static List<TerrainPoint> GetPath(PathNode endNode)
        {
            List<TerrainPoint> path = new() { endNode.Point };

            PathNode currentNode = endNode;

            // exclude the first point, because that will be the point we are currently at
            while (currentNode.PrevNode != null && currentNode.PrevNode.PrevNode != null)
            {
                path.Add(currentNode.PrevNode.Point);
                currentNode = currentNode.PrevNode;
            }

            path.Reverse();

            return path;
        }
    }
}