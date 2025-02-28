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
            /// The unit is roaming.
            /// </summary>
            ROAM,
            /// <summary>
            /// The unit is following another unit.
            /// </summary>
            FOLLOW,
            /// <summary>
            /// The unit is travelling to a flat space.
            /// </summary>
            GO_TO_FLAT_SPACE,
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
        [SerializeField] private int m_RoamingViewTileDistance = 5;
        [SerializeField] private int m_RoamingViewTileWidth = 3;
        [SerializeField] private int m_MaxStepsInRoamDirection = 10;
        [SerializeField] private int m_MaxChaseSteps = 10;

        #endregion


        #region Class Fields

        /// <summary>
        /// The <c>TerrainPoint</c> closest to the starting point of the current movement.
        /// </summary>
        public TerrainPoint StartLocation { get; private set; }
        /// <summary>
        /// The <c>TerrainPoint</c> closest to the current target point.
        /// </summary>
        public TerrainPoint EndLocation { get => !m_TargetPoint.HasValue ? StartLocation : new(m_TargetPoint.Value); }

        private Unit m_Unit;
        private Rigidbody m_Rigidbody;

        private MoveState m_LastMoveState = MoveState.STOP;
        private MoveState m_CurrentMoveState = MoveState.STOP;

        private bool m_MagnetReached;
        /// <summary>
        /// True if the unit is travelling to the faction symbol and has reached it, false otherwise.
        /// </summary>
        public bool IsUnitMagnetReached { get => m_MagnetReached; set => m_MagnetReached = value; }

        // Path MovementDirection
        private List<TerrainPoint> m_Path;
        private int m_PathIndex = 0;
        private Vector3 m_StartPosition;
        private Vector3? m_TargetPoint;
        /// <summary>
        /// True if the current step the unit is taking is to the center of a tile, false otherwise.
        /// </summary>
        private bool m_MoveToCenter;

        // Roaming
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
        /// Total steps taken while roaming
        /// </summary>
        private int m_RoamSteps;
        /// <summary>
        /// The number of steps the unit has roamed in one roam direction.
        /// </summary>
        private int m_RoamStepsInDirection;

        // FindNextStep
        /// <summary>
        /// A <c>TerrainTile</c> representing the tile the unit is moving towards, null if there is no such tile.
        /// </summary>
        private TerrainTile? m_TargetTile;
        public TerrainTile? TargetTile { get => m_TargetTile; }
        /// <summary>
        /// The target this unit is going after, null if no such target exists.
        /// </summary>
        private Unit m_TargetUnit = null;

        #endregion


        #region Event Functions

        private void FixedUpdate()
        {
            if (m_CurrentMoveState == MoveState.STOP) return;

            Vector3 currentPosition = transform.position;

            if (m_TargetPoint.HasValue && Vector3.Distance(currentPosition, m_TargetPoint.Value) > m_PositionLeeway)
                m_Rigidbody.MovePosition(currentPosition + (m_MoveSpeed * Time.deltaTime * (m_TargetPoint.Value - currentPosition).normalized));
            else if (m_Path != null)            // there are more steps to take along the path
                ChooseNextPathTarget();
            else                                // we have reached the end of the path
                ChooseNextAction();
        }

        #endregion


        #region Control

        /// <summary>
        /// Sets up the necessary properties for the unit's movement and starts the roaming.
        /// </summary>
        public void InitializeMovement()
        {
            m_Unit = GetComponent<Unit>();
            m_Rigidbody = GetComponent<Rigidbody>();

            m_StartPosition = m_Unit.ClosestTerrainPoint.ToScenePosition();
            StartLocation = m_Unit.ClosestTerrainPoint;

            Roam();
        }

        /// <summary>
        /// Pauses or unpauses the unit's movement.
        /// </summary>
        /// <param name="pause">True if the movement should be paused, false if the movement should be resumed.</param>
        public void Pause(bool pause)
        {
            // already paused
            if (pause && m_CurrentMoveState == MoveState.STOP) return;

            if (pause)
            {
                m_LastMoveState = m_CurrentMoveState;
                m_CurrentMoveState = MoveState.STOP;
            }
            else
            {
                (m_CurrentMoveState, m_LastMoveState) = (m_LastMoveState, m_CurrentMoveState);
            }
        }

        /// <summary>
        /// Sets the movement behavior to roam.
        /// </summary>
        public void StartRoaming() 
        {
            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM); 
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
        /// Chooses what the unit should do next once it has reached the end of the current path.
        /// </summary>
        private void ChooseNextAction()
        {
            // if we have a flat space (that still exists) and we have reached the end of the path, we have reached the tile
            if (m_CurrentMoveState == MoveState.GO_TO_FLAT_SPACE && m_TargetTile.HasValue &&
                m_TargetTile.Value.IsFlat() && !m_TargetTile.Value.IsOccupied())
                OnFlatTileReached();

            // if we have a settlement (that still exists) and we have reached the end of the path, we have reached the settlement
            else if (m_CurrentMoveState == MoveState.GO_TO_SETTLEMENT && m_TargetTile.HasValue && m_TargetTile.Value.HasSettlement())
                OnSettlementReached();

            // if we are following a unit, take the next step towards it
            else if (m_CurrentMoveState == MoveState.FOLLOW && m_TargetUnit)
                GetNextStepToFollowTarget();

            // if we have reached the unit magnet, roam around it
            else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET && m_MagnetReached)
                WanderAroundPoint();

            // if we haven't reached the unit magnet, keep going towards it
            else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET)
                GoToMagnet();

            // otherwise continue roaming
            else
                Roam();
        }

        #endregion


        #region Roaming

        /// <summary>
        /// Controls the unit's roaming.
        /// </summary>
        private void Roam()
        {
            UnsetPath();    
            SwitchMoveState(MoveState.ROAM);

            TerrainPoint currentLocation = m_Unit.ClosestTerrainPoint;
            UnitManager.Instance.AddStepAtPoint(m_Unit.Faction, currentLocation);

            if (m_RoamSteps == UnitManager.Instance.UnitDecayRate)
            {
                m_Unit.LoseStrength(1);
                m_RoamSteps = 0;
            }

            m_RoamSteps++;

            // if the normal roaming behavior is modified, then this function will take care of that
            if (CheckUnitBehavior(currentLocation))
                return;

            // No modifications, so we roam. We only go a certain number of steps in one direction to create more variety
            if (m_RoamStepsInDirection <= m_MaxStepsInRoamDirection && m_CurrentRoamDirection != (0, 0))
                m_RoamStepsInDirection++;
            else
                ChooseRoamDirection(currentLocation);

            SetPath(ChooseNextRoamTarget(currentLocation).ToScenePosition());
        }

        /// <summary>
        /// Checks whether the normal roaming bahevior should be modified based on the current behavior of the unit, and performs the modifications if so.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>True if the normal roam behavior has been modified, false if not.</returns>
        private bool CheckUnitBehavior(TerrainPoint currentLocation)
        {
            // If we are settling, try to find a free tile in the vicinity.
            if (m_Unit.Behavior == UnitBehavior.SETTLE)
            {
                TerrainTile? targetTile = FindFreeTile(currentLocation);

                if (targetTile.HasValue)
                {
                    SwitchMoveState(MoveState.GO_TO_FLAT_SPACE);
                    m_TargetTile = targetTile;
                    SetPath(targetTile.Value.GetClosestCorner(currentLocation).ToScenePosition());
                    return true;
                }
            }

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
                    SetPath(m_TargetTile.Value.GetClosestCorner(currentLocation).ToScenePosition());
                    return true;
                }
                
                Vector3 direction = m_Unit.GetDetectedDirection();
                if (direction != Vector3.zero)
                {
                    m_CurrentRoamDirection = (Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
                    SetPath(ChooseNextRoamTarget(currentLocation).ToScenePosition());
                    return true;
                }
            }

            if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
            {
                GoToMagnet();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Searches the surroundings of the current location depending on the roam direction to find a free tile.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>A <c>TerrainPoint</c> representing the free tile that was found, null if no such tile was found.</returns>
        private TerrainTile? FindFreeTile(TerrainPoint currentLocation)
        {
            UnitBehavior behavior = m_Unit.Behavior;
            TerrainTile? target = null;

            FindFreeTile_Surrounding();

            if (!target.HasValue && m_CurrentRoamDirection.x == 0 && m_CurrentRoamDirection.z == 0 && m_CurrentRoamDirection != (0, 0))
                FindFreeTile_Parallel();
            else if (!target.HasValue)
                FindFreeTile_Diagonal();

            return target;

            /// <summary>
            /// Searches the tiles immediately around the current location.
            /// </summary>
            void FindFreeTile_Surrounding()
            {
                for (int z = 0; z >= -1; --z)
                {
                    for (int x = 0; x >= -1; --x)
                    {
                        TerrainTile tile = new(currentLocation.X + x, currentLocation.Z + z);

                        if (!tile.IsInBounds())
                            continue;

                        if (tile.IsUnderwater() || (behavior == UnitBehavior.SETTLE &&
                            (!tile.IsUnderwater()) || tile.IsOccupied()))
                            continue;

                        target = tile;
                        return;
                    }
                }
            }

            /// <summary>
            /// Searches the tiles in front of the unit based on the direction it is facing, 
            /// if that direction is up, down, left, or right.
            /// </summary>
            void FindFreeTile_Parallel()
            {
                for (int dist = 1; dist < m_RoamingViewTileDistance; ++dist)
                {
                    int distanceTarget = m_CurrentRoamDirection.x == 0 ? (currentLocation.Z + m_CurrentRoamDirection.z * dist) : (currentLocation.X + m_CurrentRoamDirection.x * dist);

                    if (distanceTarget < 0 || distanceTarget >= Terrain.Instance.TilesPerSide) continue;

                    for (int width = -m_RoamingViewTileWidth; width < m_RoamingViewTileWidth; ++width)
                    {
                        int widthTarget = m_CurrentRoamDirection.x == 0 ? (currentLocation.X + width) : (currentLocation.Z + width);

                        if (widthTarget < 0 || widthTarget >= Terrain.Instance.TilesPerSide) continue;

                        TerrainTile tile = m_CurrentRoamDirection.x == 0 ? new(widthTarget, distanceTarget) : new(distanceTarget, widthTarget);

                        if (tile.IsUnderwater() || (behavior == UnitBehavior.SETTLE &&
                            (!tile.IsFlat() || tile.IsOccupied())))
                            continue;

                        target = tile;
                        return;
                    }
                }
            }

            /// <summary>
            /// Searches the tiles in front of the unit based on the direction it is facing, 
            /// if that direction is up-left, up-right, down-left, or down-right.
            /// </summary>
            void FindFreeTile_Diagonal()
            {
                for (int dist = 1; dist < m_RoamingViewTileDistance; ++dist)
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
                            int targetX = currentLocation.X + m_CurrentRoamDirection.x * dx;
                            int targetZ = currentLocation.Z + m_CurrentRoamDirection.z * dz;

                            TerrainTile tile = new(targetX, targetZ);

                            if (!tile.IsInBounds())
                                continue;

                            if (tile.IsUnderwater() || (behavior == UnitBehavior.SETTLE &&
                                (!tile.IsFlat() || tile.IsOccupied())))
                                continue;

                            target = tile;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds a new direction for the unit to roam in, only staying in the same direction if none other is available.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        private void ChooseRoamDirection(TerrainPoint currentLocation)
        {
            List<(int x, int z)> availableDirections = new();

            void AddValidDirections((int, int)[] d)
            {
                foreach ((int dx, int dz) in d)
                {
                    TerrainPoint point = new(currentLocation.X + dx, currentLocation.Z + dz);

                    if (!point.IsInBounds()) continue;
                    if (point.IsUnderwater()/* || !Terrain.Instance.IsTileCornerReachable(currentLocation, tile)*/)
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


            if (availableDirections.Count == 0)
            {
                m_CurrentRoamDirection = (0, 0);
                return;
            }

            int[] steps = new int[availableDirections.Count];
            int minStep = int.MaxValue;
            int minIndex = -1;

            for (int i = 0; i < availableDirections.Count; ++i)
            {
                TerrainPoint gridPoint = new(currentLocation.X + availableDirections[i].x, currentLocation.Z + availableDirections[i].z);

                if (gridPoint.X < 0 || gridPoint.X > Terrain.Instance.TilesPerSide || 
                    gridPoint.X < 0 || gridPoint.Z > Terrain.Instance.TilesPerSide)
                {
                    steps[i] = int.MaxValue;
                    continue;
                }

                steps[i] = UnitManager.Instance.GetStepsAtPoint(m_Unit.Faction, gridPoint);
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
        /// Finds the next target for the unit to roam to, given the current location and the direction of roaming.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>The <c>TerrainPoint</c> of the target location.</returns>
        private TerrainPoint ChooseNextRoamTarget(TerrainPoint currentLocation)
        {
            (int x, int z) target = (currentLocation.X + m_CurrentRoamDirection.x, currentLocation.Z + m_CurrentRoamDirection.z);
            TerrainPoint targetLocation = new(target.x, target.z);

            if (!targetLocation.IsInBounds() ||/* !Terrain.Instance.IsTileCornerReachable(currentLocation, targetLocation) ||*/
                targetLocation.IsUnderwater())
            {
                m_RoamStepsInDirection = 0;
                ChooseRoamDirection(currentLocation);
                target = (currentLocation.X + m_CurrentRoamDirection.x, currentLocation.Z + m_CurrentRoamDirection.z);
                targetLocation = new(target.x, target.z);
            }

            return targetLocation;
        }

        #endregion


        #region Path Movement

        /// <summary>
        /// Gets the path from the current position to the given end position and sets it up as the current path the unit should follow.
        /// </summary>
        /// <param name="end">The <c>Vector3</c> position of the end point.</param>
        private void SetPath(Vector3 end) => SetPath(AStarPathfinder.FindPath(m_Unit.ClosestTerrainPoint, new(end))); 

        /// <summary>
        /// Sets the path the unit should follow and initializes the movement along it.
        /// </summary>
        /// <param name="path">A list of <c>MapPoints</c> representing the path.</param>
        private void SetPath(List<TerrainPoint> path)
        {
            m_Path = path;
            m_PathIndex = 0;
            m_TargetPoint = m_Path[m_PathIndex].ToScenePosition();
        }

        /// <summary>
        /// Removes the current path.
        /// </summary>
        private void UnsetPath()
        {
            m_Path = null;
            m_PathIndex = 0;
            m_TargetPoint = null;
        }

        /// <summary>
        /// Sets the next target point the unit should go along the path, either the next point on 
        /// the grid or the center of a tile if the unit is crossing along the tile's diagonal.
        /// </summary>
        private void ChooseNextPathTarget()
        {
            // we have reached the end
            if (m_PathIndex >= m_Path.Count)
            {
                UnsetPath();
                return;
            }

            m_StartPosition = m_Unit.ClosestTerrainPoint.ToScenePosition();
            StartLocation = m_Unit.ClosestTerrainPoint;

            // pick next target
            Vector3 target = m_Path[m_PathIndex].ToScenePosition();

            // rotate unit to face the next target
            m_Unit.Rotate/*_ClientRpc*/((target - Vector3.up * target.y) - (m_StartPosition - Vector3.up * m_StartPosition.y));

            // if we are moving on a diagonal, the next step will be to the center of the tile
            if (!m_MoveToCenter && m_StartPosition.x != target.x && m_StartPosition.z != target.z)
            {
                m_MoveToCenter = true;
                m_TargetPoint = ComputeCenterPosition(m_StartPosition, target);
            }
            else
            {
                // this was the move to the center, so the next one will be a move to the edge
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

            return new(x, GetCenterPositionHeight((x, z)), z);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tile">The (x, z)-coordinates of the given tile </param>
        /// <returns></returns>
        private float GetCenterPositionHeight((float x, float z) tile) 
            => new TerrainTile((int)(tile.x / Terrain.Instance.UnitsPerTileSide), (int)(tile.z / Terrain.Instance.UnitsPerTileSide)).GetCenterHeight();

        /// <summary>
        /// 
        /// </summary>
        public void UpdateTargetPointHeight()
        {
            if (!m_TargetPoint.HasValue) return;

            Vector3 target = m_TargetPoint.Value;
            m_TargetPoint = new(target.x, !m_MoveToCenter ? EndLocation.GetHeight() : GetCenterPositionHeight((target.x, target.z)), target.z);
        }

        #endregion


        #region Follow

        /// <summary>
        /// Make this unit go after the leader of its faction, if a leader exists.
        /// </summary>
        /// 

        /// make it so it distinguishes between unit leader and settlement leader
        public void FollowLeader() => FollowUnit(UnitManager.Instance.GetLeaderUnit(m_Unit.Faction));

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
            SetPath(new List<TerrainPoint>() { step.Value });
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
            SwitchMoveState(MoveState.ROAM);
        }

        #endregion


        #region Special Circumstances

        /// <summary>
        /// Sets the unit to go to the faction symbol.
        /// </summary>
        public void GoToMagnet()
        {
            m_MagnetReached = false;
            SwitchMoveState(MoveState.GO_TO_MAGNET);

            if (m_Unit.Type == UnitType.LEADER || !GameController.Instance.HasLeader(m_Unit.Faction))
                SetPath(GameController.Instance.GetUnitMagnetPosition(m_Unit.Faction));
            else if (StructureManager.Instance.HasSettlementLeader(m_Unit.Faction))
                SetPath(StructureManager.Instance.GetLeaderSettlement(m_Unit.Faction).transform.position);
            else
                FollowLeader();
        }

        /// <summary>
        /// Sets the unit's actions when it has reached a flat tile.
        /// </summary>
        private void OnFlatTileReached()
        {
            SwitchMoveState(MoveState.STOP);
            //StructureManager.Instance.CreateSettlement(m_TargetTile.Value, m_Unit.Team);

            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        /// <summary>
        /// Sets the unit's actions when it has reached a settlement.
        /// </summary>
        private void OnSettlementReached()
        {
            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        /// <summary>
        /// Stops the unit's movement to the given tile, if that tile is the unit's target.
        /// </summary>
        /// <param name="tile">The target tile.</param>
        public void StopMovingToTile(TerrainTile tile)
        {
            //if (m_TargetTile != tile) return;

            //m_TargetTile = null;
            //SwitchMoveState(MoveState.ROAM);
        }

        /// <summary>
        /// Sets the unit to wander around its current location.
        /// </summary>
        private void WanderAroundPoint()
        {
            TerrainPoint lastPoint = m_Unit.ClosestTerrainPoint;

            // goes to the neightbor, and then back to the starting point
            SetPath(new List<TerrainPoint> { GetNeighboringPoint(lastPoint), lastPoint });
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



        /// <summary>
        /// Checks
        /// </summary>
        /// <param name="start">A <c>TerrainPoint</c> representing the start corner.</param>
        /// <param name="end">A <c>TerrainPoint</c> representing the end corner.</param>
        /// <returns>True if no <c>Structure</c> or water is crossed, false otherwise.</returns>
        public static bool IsStepTargetReachable(TerrainPoint start, TerrainPoint end)
        {
            // we are going into the water, so it is not reachable
            // when Armageddon is active, the units can go into the water
            if (!GameController.Instance.IsArmageddon && end.IsUnderwater()) return false;

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

            Structure structure = StructureManager.Instance.GetStructureOnTile(new(x, z));
            if (!structure || structure.GetType() == typeof(Field) || structure.GetType() == typeof(Swamp))
                return true;

            return false;
        }
    }
}