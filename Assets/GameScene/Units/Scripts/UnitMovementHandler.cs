using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>UnitMovementHandler</c> class handles the movement of an individual unit.
    /// </summary>
    public class UnitMovementHandler : MonoBehaviour
    {
        /// <summary>
        /// States of movement a unit can be in.
        /// </summary>
        private enum MoveState
        {
            /// <summary>
            /// The unit is not moving.
            /// </summary>
            STOP,
            /// <summary>
            /// The unit is moving around freely, without a destination.
            /// </summary>
            FREE_MOVE,
            /// <summary>
            /// The unit is following another unit.
            /// </summary>
            FOLLOW,
            /// <summary>
            /// The unit is travelling to a flat space.
            /// </summary>
            GO_TO_FREE_TILE,
            /// <summary>
            /// The unit is travelling to a settlement.
            /// </summary>
            GO_TO_SETTLEMENT,
            /// <summary>
            /// The unit is travelling to its faction's unit magnet.
            /// </summary>
            GO_TO_MAGNET
        }


        #region Inspector Fields

        [SerializeField] private float m_MoveSpeed = 40f;
        [SerializeField] private float m_PositionLeeway = 0.5f;

        [Header("Roaming")]
        [SerializeField] private int m_ViewTileDistance = 5;
        [SerializeField] private int m_ViewTileWidth = 3;
        [SerializeField] private int m_MaxStepsInRoamDirection = 10;
        [SerializeField] private int m_MaxChaseSteps = 10;

        #endregion


        #region Class Fields

        /// <summary>
        /// A reference to the unit this movement handler belongs to.
        /// </summary>
        private Unit m_Unit;
        /// <summary>
        /// A reference to the rigidbody of the unit this movement handler belongs to.
        /// </summary>
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// The <c>TerrainPoint</c> closest to the starting point of the current movement.
        /// </summary>
        public TerrainPoint StartLocation { get; private set; }
        /// <summary>
        /// The <c>TerrainPoint</c> closest to the current target point.
        /// </summary>
        public TerrainPoint EndLocation { get => !m_TargetPoint.HasValue ? StartLocation : new(m_TargetPoint.Value); }

        /// <summary>
        /// The previous movement state of the unit.
        /// </summary>
        private MoveState m_LastMoveState = MoveState.STOP;
        /// <summary>
        /// The current movement state of the unit.
        /// </summary>
        private MoveState m_CurrentMoveState = MoveState.STOP;

        private bool m_IsUnitMagnetReached;
        /// <summary>
        /// True if the unit is travelling to the unit magnet and has reached it, false otherwise.
        /// </summary>
        public bool IsUnitMagnetReached { get => m_IsUnitMagnetReached; set => m_IsUnitMagnetReached = value; }

        /// <summary>
        /// Total steps taken, used for the decay rate.
        /// </summary>
        private int m_Steps;


        #region Moving along a path

        /// <summary>
        /// A list of <c>TerrainPoints</c> that the unit should visit in order.
        /// </summary>
        private List<TerrainPoint> m_Path;
        /// <summary>
        /// The index in the <c>Path</c> of the point that the unit is currently travelling to.
        /// </summary>
        private int m_PathIndex = 0;
        /// <summary>
        /// True if the current step the unit is taking is to the center of a tile, false otherwise.
        /// </summary>
        private bool m_MoveToCenter;
        /// <summary>
        /// The position of the point in the path the player is going towards, null if no such point exists.
        /// </summary>
        private Vector3? m_TargetPoint;

        #endregion


        #region Roaming

        /// <summary>
        /// All the possible directions the unit could roam in.
        /// </summary>
        private readonly (int x, int z)[] m_RoamDirections = new (int, int)[]
        {
            (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)
        };
        /// <summary>
        /// The direction the unit is roaming in.
        /// </summary>
        private (int x, int z) m_CurrentRoamDirection;
        /// <summary>
        /// The number of steps the unit has roamed in one direction.
        /// </summary>
        private int m_RoamStepsInDirection;

        #endregion


        #region Target

        /// <summary>
        /// A <c>TerrainTile</c> representing the tile the unit is moving towards, null if the unit is not going towards a specific tile.
        /// </summary>
        private TerrainTile? m_TargetTile;
        /// <summary>
        /// The <c>Unit</c> target this unit is chasing, null if the unit isn't chasing anyone.
        /// </summary>
        private Unit m_TargetUnit = null;

        #endregion

        #endregion


        private void FixedUpdate()
        {
            if (m_CurrentMoveState == MoveState.STOP) return;

            Vector3 currentPosition = transform.position;

            // there is a target and we haven't reached it
            if (m_TargetPoint.HasValue && Vector3.Distance(currentPosition, m_TargetPoint.Value) > m_PositionLeeway)
            {
                m_Rigidbody.MovePosition(currentPosition + (m_MoveSpeed * Time.deltaTime * (m_TargetPoint.Value - currentPosition).normalized));
                return;
            }

            // there is a path to follow
            if (m_Path != null)
            {
                // we went through all the points on the path
                if (m_PathIndex < m_Path.Count)
                {
                    ChooseNextPathTarget();
                    return;
                }

                ClearPath();
            }

            ChooseNextAction();
        }


        #region Control

        /// <summary>
        /// Sets up the necessary properties for the unit's movement and starts the movement.
        /// </summary>
        public void InitializeMovement()
        {
            m_Unit = GetComponent<Unit>();
            m_Rigidbody = GetComponent<Rigidbody>();

            SwitchMoveState(MoveState.FREE_MOVE);
        }

        /// <summary>
        /// Switches the movement behavior to the given behavior.
        /// </summary>
        /// <param name="state">The <c>MoveState</c> which should be set.</param>
        private void SwitchMoveState(MoveState state)
        {
            if (state == m_CurrentMoveState) return;

            m_LastMoveState = m_CurrentMoveState;
            m_CurrentMoveState = state;
        }

        /// <summary>
        /// Pauses or unpauses the unit's movement.
        /// </summary>
        /// <param name="pause">True if the movement should be paused, false if the movement should be resumed.</param>
        public void Pause(bool pause)
        {
            // already paused
            if (pause && m_CurrentMoveState == MoveState.STOP) return;

            (m_CurrentMoveState, m_LastMoveState) = pause ? (m_CurrentMoveState, MoveState.STOP) : (m_LastMoveState, m_CurrentMoveState);
        }

        /// <summary>
        /// Chooses what the unit should do next once it has reached the end of the current path.
        /// </summary>
        private void ChooseNextAction()
        {
            if (m_CurrentMoveState == MoveState.STOP) return;

            // if we are going to a flat space that still exists and is still free, we have reached the tile
            if (m_CurrentMoveState == MoveState.GO_TO_FREE_TILE && m_TargetTile.HasValue && m_TargetTile.Value.IsFree())
                OnFreeTileReached();

            //// if we have a settlement (that still exists) and we have reached the end of the path, we have reached the settlement
            //else if (m_CurrentMoveState == MoveState.GO_TO_SETTLEMENT && m_TargetTile.HasValue && m_TargetTile.Value.HasSettlement())
            //    OnSettlementReached();

            //// if we are following a unit, take the next step towards it
            //else if (m_CurrentMoveState == MoveState.FOLLOW && m_TargetUnit)
            //    GetNextStepToFollowTarget();

            //// if we have reached the unit magnet, roam around it
            //else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET && m_IsUnitMagnetReached)
            //    WanderAroundPoint();

            //// if we haven't reached the unit magnet, keep going towards it
            //else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET)
            //    GoToUnitMagnet();

            else
            {
                SwitchMoveState(MoveState.FREE_MOVE);

                // if the behavior is settle and we find a free tile, just go there
                if (m_CurrentRoamDirection != (0, 0) && m_Unit.Behavior == UnitBehavior.SETTLE && GoToFreeTile()) 
                    return;


                // otherwise just roam
                else FreeRoam();
            }

        }

        #endregion


        #region Movement Along Path

        /// <summary>
        /// Gets the path from the current position to the given end position and sets it as the path the unit should follow.
        /// </summary>
        /// <param name="end">The <c>Vector3</c> position of the end point.</param>
        private void GoTo(Vector3 end) => SetNewPath(AStarPathfinder.FindPath(m_Unit.ClosestTerrainPoint, new(end)));

        /// <summary>
        /// Sets the path the unit should follow and initializes the movement along it.
        /// </summary>
        /// <param name="path">A list of <c>TerrainPoint</c>s representing the path.</param>
        private void SetNewPath(List<TerrainPoint> path)
        {
            m_Path = path;
            m_PathIndex = 0;
        }

        /// <summary>
        /// Removes the current path.
        /// </summary>
        private void ClearPath()
        {
            m_Path = null;
            m_PathIndex = 0;
            m_TargetPoint = null;
        }


        /// <summary>
        /// Sets the next target point the unit should go to along the path.
        /// </summary>
        private void ChooseNextPathTarget()
        {
            StartLocation = m_Unit.ClosestTerrainPoint;
            Vector3 m_StartPosition = StartLocation.ToScenePosition();

            // pick next target
            Vector3 target = m_Path[m_PathIndex].ToScenePosition();

            UnitManager.Instance.AddStepAtPoint(m_Unit.Faction, StartLocation);
            // check if the unit has lost followers
            if (m_PathIndex > 0) m_Steps++;
            if (m_Steps == UnitManager.Instance.UnitDecayRate)
            {
                //m_Unit.LoseStrength(1);
                m_Steps = 0;
            }

            // rotate unit to face the next target
            m_Unit.Rotate/*_ClientRpc*/((target - Vector3.up * target.y) - (m_StartPosition - Vector3.up * m_StartPosition.y));

            // if we are moving diagonally across the tile, the next step will be to the center of the tile
            if (!m_MoveToCenter && m_StartPosition.x != target.x && m_StartPosition.z != target.z)
            {
                m_MoveToCenter = true;
                m_TargetPoint = ComputeCenterPosition(m_StartPosition, target);
            }
            else
            {
                m_MoveToCenter = false;
                m_PathIndex++;
                m_TargetPoint = target;
            }
        }

        /// <summary>
        /// Gets the center point of a tile with the given diagonal corners.
        /// </summary>
        /// <param name="a">The position of one corner of the tile.</param>
        /// <param name="b">The position of the corner of the tile diagonal to <c>a</c>.</param>
        /// <returns>The <c>Vector3</c> of the position of the center of the tile.</returns>
        private Vector3 ComputeCenterPosition(Vector3 a, Vector3 b)
        {
            float dx = (b.x - a.x) / Terrain.Instance.UnitsPerTileSide;
            float dz = (b.z - a.z) / Terrain.Instance.UnitsPerTileSide;

            float x = a.x + dx * (Terrain.Instance.UnitsPerTileSide / 2);
            float z = a.z + dz * (Terrain.Instance.UnitsPerTileSide / 2);

            return new TerrainTile(x, z).GetCenterPosition();
        }

        /// <summary>
        /// Given two corners of a tile, checks if the unit can get from one of them to the other.
        /// </summary>
        /// <param name="start">A <c>TerrainPoint</c> representing the start corner.</param>
        /// <param name="end">A <c>TerrainPoint</c> representing the end corner.</param>
        /// <returns>True if the tile can be crossed, false otherwise.</returns>
        public static bool IsTileCrossable(TerrainPoint start, TerrainPoint end)
        {
            if (GameController.Instance.IsArmageddon) return true;

            if (end.IsUnderwater()) return false;

            int dx = end.X - start.X;
            int dz = end.Z - start.Z;

            // we are moving along the edge of the tile and not into the water, so its fine
            if (Mathf.Abs(dx) != Mathf.Abs(dz)) return true;

            (int x, int z) = (start.X, start.Z);

            if (dx > 0 && dz < 0)
                z -= 1;
            else if (dx < 0 && dz > 0)
                x -= 1;
            else if (dx < 0 && dz < 0)
            {
                x -= 1;
                z -= 1;
            }

            if (x < 0 || z < 0)
                return false;

            Structure structure = new TerrainTile(x, z).GetStructure();
            if (!structure || structure.GetType() == typeof(Field) || structure.GetType() == typeof(Swamp))
                return true;

            return false;
        }


        /// <summary>
        /// Updates the height of the target point of the movement.
        /// </summary>
        /// <remarks>Called after terrain modification.</remarks>
        public void UpdateTargetPointHeight(Vector3 bottomLeftPosition, Vector3 topRightPosition)
        {
            if (!m_TargetPoint.HasValue ||
                (transform.position.x < bottomLeftPosition.x || transform.position.x > topRightPosition.x ||
                transform.position.z < bottomLeftPosition.z || transform.position.z > topRightPosition.z)) 
                return;

            Vector3 target = m_TargetPoint.Value;
            m_TargetPoint = new(
                target.x, 
                !m_MoveToCenter ? EndLocation.GetHeight() : new TerrainTile(target.x, target.z).GetCenterHeight(), 
                target.z
            );
        }

        #endregion


        #region Free Roam

        /// <summary>
        /// Controls the unit's roaming.
        /// </summary>
        private void FreeRoam()
        {
            TerrainPoint currentLocation = m_Unit.ClosestTerrainPoint;

            // We only go a certain number of steps in one direction to create more variety
            if (m_RoamStepsInDirection <= m_MaxStepsInRoamDirection && m_CurrentRoamDirection != (0, 0))
                m_RoamStepsInDirection++;
            else
                ChooseRoamDirection(currentLocation);

            GoTo(ChooseNextRoamTarget(currentLocation).ToScenePosition());
        }

        /// <summary>
        /// Finds a new direction for the unit to roam in, only staying in the same direction if none other is available.
        /// </summary>
        /// <param name="current">The <c>TerrainPoint</c> of the current location of this unit.</param>
        private void ChooseRoamDirection(TerrainPoint current)
        {
            // get all directions the player could go in from this point
            List<(int x, int z)> availableDirections = GetAvailableDirections(current);

            if (availableDirections.Count == 0)
            {
                m_CurrentRoamDirection = (0, 0);
                return;
            }

            // get neighbor that has been visited the least amount of times
            int[] steps = new int[availableDirections.Count];
            int minStep = int.MaxValue;
            int minIndex = -1;

            for (int i = 0; i < availableDirections.Count; ++i)
            {
                steps[i] = new TerrainPoint(current.X + availableDirections[i].x, current.Z + availableDirections[i].z)
                    .GetStepsByFaction(m_Unit.Faction);

                if (steps[i] < minStep)
                {
                    minStep = steps[i];
                    minIndex = i;
                }
            }

            // if all points have been visited the same number of times, just pick a random direction
            if (steps.All(x => steps[0] == x))
                m_CurrentRoamDirection = availableDirections[new Random().Next(0, availableDirections.Count)];
            else
                m_CurrentRoamDirection = availableDirections[minIndex];
        }

        /// <summary>
        /// Finds all the directions the unit can move in from the given starting point.
        /// </summary>
        /// <param name="start">The <c>TerrainPoint</c> the unit is currently standing at.</param>
        /// <returns>A list of (dx, dz)-tuples representing directions of movement.</returns>
        private List<(int, int)> GetAvailableDirections(TerrainPoint start)
        {
            List<(int x, int z)> availableDirections = new();

            // adds only the directions that lead to a point that can be reached to the availabe directions list
            void AddValidDirections((int, int)[] d)
            {
                foreach ((int dx, int dz) in d)
                {
                    TerrainPoint point = new(start.X + dx, start.Z + dz);
                    if (!point.IsInBounds() || point.IsUnderwater() || !IsTileCrossable(start, point))
                        continue;

                    availableDirections.Add((dx, dz));
                }
            }

            if (m_CurrentRoamDirection == (0, 0))
                AddValidDirections(m_RoamDirections);
            else
            {
                int currentDirection = Array.IndexOf(m_RoamDirections, m_CurrentRoamDirection);

                // prioritize forward directions
                AddValidDirections(new (int, int)[] {
                    m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, -1, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, +1, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, -2, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, +2, m_RoamDirections.Length)]
                });

                // backwards directions or continue in the direction we already are going down
                if (availableDirections.Count == 0)
                    AddValidDirections(new (int, int)[] {
                        m_RoamDirections[currentDirection],
                        m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, -3, m_RoamDirections.Length)],
                        m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, +3, m_RoamDirections.Length)]
                    });

                // only backtrack the way we came if there are no other options
                if (availableDirections.Count == 0)
                    AddValidDirections(new (int, int)[] {
                        m_RoamDirections[GameUtils.GetNextArrayIndex(currentDirection, +4, m_RoamDirections.Length)]
                    });
            }

            return availableDirections;
        }

        /// <summary>
        /// Finds the next target for the unit to roam to, given the current location and the direction of roaming.
        /// </summary>
        /// <param name="current">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>The <c>TerrainPoint</c> of the target location.</returns>
        private TerrainPoint ChooseNextRoamTarget(TerrainPoint current)
        {
            TerrainPoint target = new(current.X + m_CurrentRoamDirection.x, current.Z + m_CurrentRoamDirection.z);

            if (!target.IsInBounds() || target.IsUnderwater() || !IsTileCrossable(current, target))
            {
                m_RoamStepsInDirection = 0;
                ChooseRoamDirection(current);
                target = new(current.X + m_CurrentRoamDirection.x, current.Z + m_CurrentRoamDirection.z);
            }

            return target;
        }

        #endregion


        #region Settle

        /// <summary>
        /// Searches the surroundings of the point the unit is currently at to find free spaces.
        /// </summary>
        /// <returns>True if a free tile was found, false otherwise.</returns>
        private bool GoToFreeTile()
        {
            TerrainPoint current = m_Unit.ClosestTerrainPoint;
            TerrainTile? target = null;

            FindFreeTile_Surrounding();

            if (!target.HasValue && m_CurrentRoamDirection.x == 0 && m_CurrentRoamDirection.z == 0 && m_CurrentRoamDirection != (0, 0))
                FindFreeTile_Parallel();

            else if (!target.HasValue)
                FindFreeTile_Diagonal();

            if (target.HasValue)
            {
                SwitchMoveState(MoveState.GO_TO_FREE_TILE);
                m_TargetTile = target.Value;
                GoTo(target.Value.GetClosestCorner(current).ToScenePosition());
                return true;
            }

            return false;

            /// <summary>
            /// Searches the tiles immediately around the current location.
            /// </summary>
            void FindFreeTile_Surrounding()
            {
                for (int z = 0; z >= -1; --z)
                {
                    for (int x = 0; x >= -1; --x)
                    {
                        TerrainTile tile = new(current.X + x, current.Z + z);
                        if (!tile.IsFree()) continue;

                        target = tile;
                        return;
                    }
                }
            }

            /// <summary>
            /// Searches the tiles in front of the unit based on the direction it is facing, if that direction is up, down, left, or right.
            /// </summary>
            void FindFreeTile_Parallel()
            {
                for (int dist = 1; dist < m_ViewTileDistance; ++dist)
                {
                    int distanceTarget = m_CurrentRoamDirection.x == 0 ? (current.Z + m_CurrentRoamDirection.z * dist) : (current.X + m_CurrentRoamDirection.x * dist);

                    if (distanceTarget < 0 || distanceTarget >= Terrain.Instance.TilesPerSide) continue;

                    for (int width = -m_ViewTileWidth; width < m_ViewTileWidth; ++width)
                    {
                        int widthTarget = m_CurrentRoamDirection.x == 0 ? (current.X + width) : (current.Z + width);

                        if (widthTarget < 0 || widthTarget >= Terrain.Instance.TilesPerSide) continue;

                        TerrainTile tile = m_CurrentRoamDirection.x == 0 ? new(widthTarget, distanceTarget) : new(distanceTarget, widthTarget);
                        if (!tile.IsFree()) continue;

                        target = tile;
                        return;
                    }
                }
            }

            /// <summary>
            /// Searches the tiles in front of the unit based on the direction it is facing, if that direction is up-left, up-right, down-left, or down-right.
            /// </summary>
            void FindFreeTile_Diagonal()
            {
                // this is done because we want to get the closest tiles first
                for (int dist = 1; dist < m_ViewTileDistance; ++dist)
                {
                    for (int z = 0; z <= dist; ++z)
                    {
                        (int, int)[] directions;

                        if (z == dist)
                            directions = new (int, int)[] { (dist, dist) };
                        else
                            directions = new (int, int)[] { (z, dist), (dist, z) };

                        foreach ((int dx, int dz) in directions)
                        {
                            int targetX = current.X + m_CurrentRoamDirection.x * dx;
                            int targetZ = current.Z + m_CurrentRoamDirection.z * dz;

                            TerrainTile tile = new(targetX, targetZ);
                            if (!tile.IsFree()) continue;
                            target = tile;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the unit's actions when it has reached a flat tile.
        /// </summary>
        private void OnFreeTileReached()
        {
            SwitchMoveState(MoveState.STOP);
            StructureManager.Instance.CreateSettlement(m_TargetTile.Value, m_Unit.Faction);

            RemoveTargetTile();
        }


        /// <summary>
        /// Check if the target tile was modified by a terrain modification and go back to roaming if it was.
        /// </summary>
        public void CheckTargetTile() 
            => CheckTargetTile(new(0, 0), new(Terrain.Instance.TilesPerSide, Terrain.Instance.TilesPerSide));

        /// <summary>
        /// Check if the target tile was in the modified area and modified by a terrain modification, and go back to roaming if it was.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        public void CheckTargetTile(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            if (!m_TargetTile.HasValue ||
                m_TargetTile.Value.X < bottomLeft.X - 1 || m_TargetTile.Value.X > topRight.X ||
                m_TargetTile.Value.Z < bottomLeft.Z - 1 || m_TargetTile.Value.Z > topRight.Z ||
                m_TargetTile.Value.IsFree())
                return;

            RemoveTargetTile();
        }

        /// <summary>
        /// 
        /// </summary>
        private void RemoveTargetTile()
        {
            m_TargetTile = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

        #endregion


        #region Go To Unit Magnet

        /// <summary>
        /// Sets the unit to go to the unit magnet.
        /// </summary>
        public void GoToUnitMagnet()
        {
            m_IsUnitMagnetReached = false;
            SwitchMoveState(MoveState.GO_TO_MAGNET);

            // The leader and units that don't have a leader, go straight to the magnet
            if (m_Unit.Type == UnitType.LEADER || !GameController.Instance.HasLeader(m_Unit.Faction))
                GoTo(GameController.Instance.GetUnitMagnetPosition(m_Unit.Faction));

            // If the leader is in a settlement, go to the settlement. The first unit there will become the leader
            else if (GameController.Instance.HasLeaderSettlement(m_Unit.Faction))
                GoTo(GameController.Instance.GetLeaderSettlement(m_Unit.Faction).transform.position);

            else
                FollowLeader();
        }

        /// <summary>
        /// Sets the unit to wander around its current location.
        /// </summary>
        private void WanderAroundPoint()
        {
            TerrainPoint lastPoint = m_Unit.ClosestTerrainPoint;

            // goes to the neightbor, and then back to the starting point
            SetNewPath(new List<TerrainPoint> { GetNeighboringPoint(lastPoint), lastPoint });
        }


        /// <summary>
        /// Gets a neighbor of the given point that can be reached by a unit starting from that point.
        /// </summary>
        /// <param name="point">The starting <c>TerrainPoint</c>.</param>
        /// <returns>A <c>TerrainPoint</c> representing the neighboring point.</returns>
        private TerrainPoint GetNeighboringPoint(TerrainPoint point)
        {
            Random random = new();
            //List<TerrainPoint> neighbors = point.Neighbors;
            TerrainPoint? neighbor = null;

            //int neighborsCount = neighbors.Count;
            //for (int i = 0; i < neighborsCount; ++i)
            //{
            //    TerrainPoint choice = neighbors[random.Next(neighbors.Count)];

            //    neighbors.Remove(choice);
            //    if (Terrain.Instance.IsTileCornerReachable(point, choice))
            //    {
            //        neighbor = choice;
            //        break;
            //    }
            //}

            return neighbor.Value;
        }


        #endregion




        #region 

        /// <summary>
        /// Stops the unit's movement to the given tile, if that tile is the unit's target.
        /// </summary>
        /// <param name="tile">The target tile.</param>
        public void StopMovingToTile(TerrainTile tile)
        {
            if (m_TargetTile != tile) return;

            m_TargetTile = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

        #region

        /// <summary>
        /// Checks whether the normal roaming bahevior should be modified based on the current behavior of the unit, and performs the modifications if so.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>True if the normal roam behavior has been modified, false if not.</returns>
        private bool CheckUnitBehavior(TerrainPoint currentLocation)
        {

            // If we are battling or gathering, go in the direction of other units or settlements, if some are detected.
            if (m_Unit.Behavior == UnitBehavior.FIGHT || m_Unit.Behavior == UnitBehavior.GATHER)
            {
                Unit unitInRange = m_Unit.GetUnitInChaseRange();
                if (unitInRange)
                {
                    FollowUnit(unitInRange);
                    return true;
                }

                Settlement settlementInRange = m_Unit.GetSettlementInChaseRange();
                if (settlementInRange)
                {
                    SwitchMoveState(MoveState.GO_TO_SETTLEMENT);
                    //m_TargetTile = settlementInRange.OccupiedTile;
                    GoTo(m_TargetTile.Value.GetClosestCorner(currentLocation).ToScenePosition());
                    return true;
                }
                
                Vector3 direction = m_Unit.GetDetectedDirection();
                if (direction != Vector3.zero)
                {
                    m_CurrentRoamDirection = (Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
                    GoTo(ChooseNextRoamTarget(currentLocation).ToScenePosition());
                    return true;
                }
            }

            return false;
        }



        #endregion



        #region Follow

        /// <summary>
        /// Make this unit go after the leader of its faction, if a leader exists.
        /// </summary>
        /// 

        /// make it so it distinguishes between unit leader and settlement leader
        public void FollowLeader() => FollowUnit(GameController.Instance.GetLeaderUnit(m_Unit.Faction));

        /// <summary>
        /// Make this unit go after the given unit.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> which we want to go after.</param>
        private void FollowUnit(Unit unit)
        {
            SwitchMoveState(MoveState.FOLLOW);
            m_TargetUnit = unit;
            GetNextStepToFollowTarget();
        }

        /// <summary>
        /// Gets the next step this unit should take, going after the target unit.
        /// </summary>
        private void GetNextStepToFollowTarget()
        {
            TerrainPoint? step = AStarPathfinder.FindNextStep(m_Unit.ClosestTerrainPoint, m_TargetUnit.ClosestTerrainPoint);

            if (!step.HasValue) return;
            SetNewPath(new List<TerrainPoint>() { step.Value });
        }

        /// <summary>
        /// Stops following the current target unit if it matches the given unit.
        /// </summary>
        /// <param name="targetUnit"></param>
        public void StopFollowingUnit(Unit targetUnit)
        {
            if (m_TargetUnit != targetUnit)
                return;

            StopFollowingUnit();
        }

        /// <summary>
        /// Stops following the current target unit.
        /// </summary>
        public void StopFollowingUnit() 
        {
            m_TargetUnit = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

        #endregion




        /// <summary>
        /// Sets the unit's actions when it has reached a settlement.
        /// </summary>
        private void OnSettlementReached()
        {
            m_TargetTile = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

        #endregion
    }
}