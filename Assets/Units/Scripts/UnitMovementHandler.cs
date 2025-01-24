using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>UnitMovementHandler</c> class is a <c>MonoBehavior</c> which handles the movement of a unit.
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
            /// The unit is travelling to its faction symbol.
            /// </summary>
            GO_TO_SYMBOL
        }

        [SerializeField] private float m_MoveSpeed = 40f;
        [SerializeField] private float m_PositionError = 0.5f;

        [Header("Roaming")]
        [SerializeField] private int m_RoamingViewTileDistance = 5;
        [SerializeField] private int m_RoamingViewTileWidth = 3;
        [SerializeField] private int m_MaxStepsInRoamDirection = 10;
        [SerializeField] private int m_MaxChaseSteps = 10;

        /// <summary>
        /// The <c>TerrainPoint</c> closest to the starting point of the current movement.
        /// </summary>
        public TerrainPoint StartLocation { get; private set; }
        /// <summary>
        /// The <c>TerrainPoint</c> closest to the current target point.
        /// </summary>
        public TerrainPoint EndLocation { get => !m_TargetPoint.HasValue ? StartLocation : new(m_TargetPoint.Value.x, m_TargetPoint.Value.z, getClosestPoint: true); }

        private Unit m_Unit;
        private Rigidbody m_Rigidbody;

        private MoveState m_LastMoveState = MoveState.STOP;
        private MoveState m_CurrentMoveState = MoveState.STOP;

        private bool m_SymbolReached;
        /// <summary>
        /// True if the unit is travelling to the faction symbol and has reached it, false otherwise.
        /// </summary>
        public bool SymbolReached { get => m_SymbolReached; set => m_SymbolReached = value; }

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

        // Follow
        /// <summary>
        /// A <c>TerrainPoint</c> representing the tile the unit is moving towards, null if there is no such tile.
        /// </summary>
        private TerrainPoint? m_TargetTile;
        /// <summary>
        /// The target this unit is going after, null if no such target exists.
        /// </summary>
        private Unit m_TargetUnit = null;


        private void FixedUpdate()
        {
            if (m_CurrentMoveState == MoveState.STOP) return;

            Vector3 currentPosition = transform.position;

            if (m_TargetPoint.HasValue && Vector3.Distance(currentPosition, m_TargetPoint.Value) > m_PositionError)
                m_Rigidbody.MovePosition(currentPosition + (m_MoveSpeed * Time.deltaTime * (m_TargetPoint.Value - currentPosition).normalized));
            else if (m_Path != null)            // there are more steps to take along the path
                ChooseNextPathTarget();
            else                                // we have reached the end of the path
                ChooseNextAction();
        }


        #region Control

        /// <summary>
        /// Sets up the necessary properties for the unit's movement and starts to roam.
        /// </summary>
        public void InitializeMovement()
        {
            m_Unit = GetComponent<Unit>();
            m_Rigidbody = GetComponent<Rigidbody>();

            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;

            Roam();
        }

        /// <summary>
        /// Pauses or unpauses the unit's movement.
        /// </summary>
        /// <param name="pause">True if the movement should be paused, false if the movement should be resumed.</param>
        public void Pause(bool pause)
        {
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
        /// Sets the movement state to roam.
        /// </summary>
        public void SetRoam() => SwitchMoveState(MoveState.ROAM);

        /// <summary>
        /// Switches the movement state to the given state.
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
            if (m_CurrentMoveState == MoveState.GO_TO_FLAT_SPACE && m_TargetTile.HasValue &&
                Terrain.Instance.IsTileFlat((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)) &&
                !Terrain.Instance.IsTileOccupied((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)))
                FlatTileReached();
            else if (m_CurrentMoveState == MoveState.GO_TO_SETTLEMENT && m_TargetTile.HasValue &&
                Terrain.Instance.HasTileSettlement((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)))
                SettlementReached();
            else if (m_CurrentMoveState == MoveState.FOLLOW && m_TargetUnit)
                GetNextStepToFollowTarget();
            else if (m_CurrentMoveState == MoveState.GO_TO_SYMBOL && m_SymbolReached)
                WanderAroundPoint();
            else if (m_CurrentMoveState == MoveState.GO_TO_SYMBOL)
                GoToSymbol();
            else
                Roam();
        }

        #endregion


        #region Roaming

        /// <summary>
        /// Makes the unit wander around the map.
        /// </summary>
        private void Roam()
        {
            UnsetPath();
            SwitchMoveState(MoveState.ROAM);

            TerrainPoint currentLocation = m_Unit.ClosestMapPoint;
            UnitManager.Instance.AddStepAtPoint(m_Unit.Team, (currentLocation.GridX, currentLocation.GridZ));

            //if (m_RoamSteps == UnitManager.Instance.DecayRate)
            //{
            //    m_Unit.LoseStrength(1);
            //    m_RoamSteps = 0;
            //}

            m_RoamSteps++;

            if (CheckUnitStateBehavior(currentLocation))
                return;

            // No flat tile found, so we roam. We only go a certain number of steps in one direction.
            if (m_RoamStepsInDirection <= m_MaxStepsInRoamDirection && m_CurrentRoamDirection != (0, 0))
                m_RoamStepsInDirection++;
            else
                ChooseRoamDirection(currentLocation);

            SetPath(ChooseNextRoamTarget(currentLocation).ToWorldPosition());
        }

        /// <summary>
        /// Checks whether the normal roaming bahevior can be modified based on the 
        /// current state of the unit, and performs the modifications if so.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>True if the normal roam behavior has been modified, false if not.</returns>
        private bool CheckUnitStateBehavior(TerrainPoint currentLocation)
        {
            // If we are settling, try to find an free tile in the vicinity.
            if (m_Unit.Behavior == UnitBehavior.SETTLE)
            {
                TerrainPoint? targetTile = FindFreeTile(currentLocation);

                if (targetTile.HasValue)
                {
                    SwitchMoveState(MoveState.GO_TO_FLAT_SPACE);
                    m_TargetTile = targetTile;
                    SetPath(targetTile.Value.GetClosestTileCorner(currentLocation).ToWorldPosition());
                    return true;
                }
            }

            // If we are battling or gathering, go in the direction of other units, if some are detected.
            if (m_Unit.Behavior == UnitBehavior.FIGHT || m_Unit.Behavior == UnitBehavior.GATHER)
            {
                Unit unitInRange = m_Unit.GetUnitInRange();

                // if we have another unit that's close enough, follow it.
                if (unitInRange)
                {
                    FollowUnit(unitInRange);
                    return true;
                }

                Settlement settlementInRange = m_Unit.GetSettlementInRange();

                if (settlementInRange)
                {
                    SwitchMoveState(MoveState.GO_TO_SETTLEMENT);
                    m_TargetTile = settlementInRange.OccupiedTile;
                    SetPath(m_TargetTile.Value.GetClosestTileCorner(currentLocation).ToWorldPosition());
                    return true;
                }
                
                Vector3 direction = m_Unit.GetEnemyDirection();

                // to avoid just going back and forth
                if (direction != Vector3.zero && (-direction.x, -direction.z) != m_CurrentRoamDirection)
                {
                    m_CurrentRoamDirection = (Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
                    SetPath(ChooseNextRoamTarget(currentLocation).ToWorldPosition());
                    return true;
                }
            }

            if (m_Unit.Behavior == UnitBehavior.GO_TO_SYMBOL)
            {
                GoToSymbol();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Searches the surroundings of the current location depending on the roam direction to find a free tile.
        /// </summary>
        /// <param name="currentLocation">The <c>TerrainPoint</c> of the current location of this unit.</param>
        /// <returns>A <c>TerrainPoint</c> representing the free tile that was found, null if no such tile was found.</returns>
        private TerrainPoint? FindFreeTile(TerrainPoint currentLocation)
        {
            UnitBehavior state = m_Unit.Behavior;
            TerrainPoint? target = null;

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
                        if (!Terrain.Instance.IsPointInBounds((currentLocation.GridX + x, currentLocation.GridZ + z)) ||
                            Terrain.Instance.IsLastPoint((currentLocation.GridX + x, currentLocation.GridZ + z)))
                            continue;

                        TerrainPoint point = new(currentLocation.GridX + x, currentLocation.GridZ + z);

                        if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitBehavior.SETTLE &&
                            (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                            continue;

                        target = point;
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
                    int distanceTarget = m_CurrentRoamDirection.x == 0 ? (currentLocation.GridZ + m_CurrentRoamDirection.z * dist) : (currentLocation.GridX + m_CurrentRoamDirection.x * dist);

                    if (distanceTarget < 0 || distanceTarget >= Terrain.Instance.TilesPerSide) continue;

                    for (int width = -m_RoamingViewTileWidth; width < m_RoamingViewTileWidth; ++width)
                    {
                        int widthTarget = m_CurrentRoamDirection.x == 0 ? (currentLocation.GridX + width) : (currentLocation.GridZ + width);

                        if (widthTarget < 0 || widthTarget >= Terrain.Instance.TilesPerSide) continue;

                        TerrainPoint point = m_CurrentRoamDirection.x == 0 ? new(widthTarget, distanceTarget) : new(distanceTarget, widthTarget);

                        if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitBehavior.SETTLE &&
                            (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                            continue;

                        target = point;
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
                            int targetX = currentLocation.GridX + m_CurrentRoamDirection.x * dx;
                            int targetZ = currentLocation.GridZ + m_CurrentRoamDirection.z * dz;

                            if (!Terrain.Instance.IsPointInBounds((targetX, targetZ)) || Terrain.Instance.IsLastPoint((targetX, targetZ)))
                                continue;

                            TerrainPoint point = new(targetX, targetZ);

                            if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitBehavior.SETTLE &&
                                (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                                continue;

                            target = point;
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
                    (int x, int z) target = (currentLocation.GridX + dx, currentLocation.GridZ + dz);
                    if (!Terrain.Instance.IsPointInBounds(target)) continue;

                    TerrainPoint point = new(target.x, target.z);

                    if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || !Terrain.Instance.CanCrossTile(currentLocation, point))
                        continue;

                    availableDirections.Add((dx, dz));
                }
            }

            if (m_CurrentRoamDirection == (0, 0))
                AddValidDirections(m_RoamDirections);
            else
            {
                int currentDirection = Array.IndexOf(m_RoamDirections, m_CurrentRoamDirection);

                // forward directions
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
                (int x, int z) gridPoint = (currentLocation.GridX + availableDirections[i].x, currentLocation.GridZ + availableDirections[i].z);

                if (gridPoint.x < 0 || gridPoint.x > Terrain.Instance.TilesPerSide || gridPoint.z < 0 || gridPoint.z > Terrain.Instance.TilesPerSide)
                {
                    steps[i] = int.MaxValue;
                    return;
                }

                int stepsAtPoint = UnitManager.Instance.GetStepsAtPoint(m_Unit.Team, gridPoint);
                steps[i] = stepsAtPoint;

                if (stepsAtPoint < minStep)
                {
                    minStep = stepsAtPoint;
                    minIndex = i;
                }
            }

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
            (int x, int z) target = (currentLocation.GridX + m_CurrentRoamDirection.x, currentLocation.GridZ + m_CurrentRoamDirection.z);
            TerrainPoint targetLocation = new(target.x, target.z);

            if (!Terrain.Instance.IsPointInBounds(target) || !Terrain.Instance.CanCrossTile(currentLocation, targetLocation) ||
                Terrain.Instance.IsTileUnderwater(target))
            {
                m_RoamStepsInDirection = 0;
                ChooseRoamDirection(currentLocation);
                target = (currentLocation.GridX + m_CurrentRoamDirection.x, currentLocation.GridZ + m_CurrentRoamDirection.z);
                targetLocation = new(target.x, target.z);
            }

            return targetLocation;
        }

        #endregion


        #region Path Movement

        /// <summary>
        /// Gets the path from the current position to the given end position and sets it up as the current path the unit should follow.
        /// </summary>
        /// <param name="end"></param>
        private void SetPath(Vector3 end) => SetPath(Pathfinder.FindPath(m_Unit.ClosestMapPoint, new(end.x, end.z, getClosestPoint: true))); 

        /// <summary>
        /// Sets the path the unit should follow and initializes the movement along it.
        /// </summary>
        /// <param name="path">A list of <c>MapPoints</c> representing the path.</param>
        private void SetPath(List<TerrainPoint> path)
        {
            m_Path = path;
            m_PathIndex = 0;
            m_TargetPoint = m_Path[m_PathIndex].ToWorldPosition();
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
            if (m_PathIndex >= m_Path.Count)
            {
                UnsetPath();
                return;
            }

            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;

            // pick next target
            Vector3 target = m_Path[m_PathIndex].ToWorldPosition();

            // rotate unit to face the next target
            m_Unit.Rotate/*ClientRpc*/((target - Vector3.up * target.y) - (m_StartPosition - Vector3.up * m_StartPosition.y));

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

            return new(x, GetCenterPositionHeight(x, z), z);
        }

        private int GetCenterPositionHeight(float x, float z) 
            => Terrain.Instance.GetTileCenterHeight(((int)(x / Terrain.Instance.UnitsPerTileSide), (int)(z / Terrain.Instance.UnitsPerTileSide)));

        public void UpdateTargetPointHeight()
        {
            if (!m_TargetPoint.HasValue) return;

            Vector3 target = m_TargetPoint.Value;
            m_TargetPoint = new(target.x, !m_MoveToCenter ? EndLocation.Y : GetCenterPositionHeight(target.x, target.z), target.z);
        }

        #endregion


        #region Follow

        /// <summary>
        /// Make this unit go after the leader of its faction, if a leader exists.
        /// </summary>
        public void FollowLeader() => FollowUnit(GameController.Instance.GetLeaderUnit(m_Unit.Team));

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
            TerrainPoint? step = Pathfinder.Follow(m_Unit.ClosestMapPoint, m_TargetUnit.ClosestMapPoint);

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
        public void GoToSymbol()
        {
            m_SymbolReached = false;
            SwitchMoveState(MoveState.GO_TO_SYMBOL);

            if (m_Unit.Class == UnitClass.LEADER || !GameController.Instance.HasLeader(m_Unit.Team))
                SetPath(StructureManager.Instance.GetSymbolPosition(m_Unit.Team));
            else if (GameController.Instance.IsLeaderInSettlement(m_Unit.Team))
                SetPath(GameController.Instance.GetLeaderSettlement(m_Unit.Team).transform.position);
            else
                FollowLeader();
        }

        /// <summary>
        /// Sets the unit's actions when it has reached a flat tile.
        /// </summary>
        /// <param name="tile">The <c>TerrainPoint</c> representing the found free tile.</param>
        private void FlatTileReached()
        {
            SwitchMoveState(MoveState.STOP);
            StructureManager.Instance.CreateSettlement(m_TargetTile.Value, m_Unit.Team);

            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        private void SettlementReached()
        {
            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        public void StopMovingToTile(TerrainPoint tile)
        {
            if (m_TargetTile != tile) return;

            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        public void CheckIfTargetTileFlat()
        {
            if (!m_TargetTile.HasValue || Terrain.Instance.IsTileFlat((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)))
                return;

            m_TargetTile = null;
            SwitchMoveState(MoveState.ROAM);
        }

        /// <summary>
        /// Sets the unit to wander around its current location.
        /// </summary>
        private void WanderAroundPoint()
        {
            TerrainPoint lastPoint = m_Unit.ClosestMapPoint;
            SetPath(new List<TerrainPoint> { GetNeighboringPoint(lastPoint), lastPoint });
        }

        /// <summary>
        /// Gets a neightbor of the given point that can be reached by a unit starting from that point.
        /// </summary>
        /// <param name="point">The starting <c>TerrainPoint</c>.</param>
        /// <returns>A <c>TerrainPoint</c> representing the neighboring point.</returns>
        private TerrainPoint GetNeighboringPoint(TerrainPoint point)
        {
            Random random = new();
            List<TerrainPoint> neighbors = point.Neighbors;
            TerrainPoint? neighbor = null;

            int neighborsCount = neighbors.Count;
            for (int i = 0; i < neighborsCount; ++i)
            {
                TerrainPoint choice = neighbors[random.Next(neighbors.Count)];

                neighbors.Remove(choice);
                if (Terrain.Instance.CanCrossTile(point, choice))
                {
                    neighbor = choice;
                    break;
                }
            }

            return neighbor.Value;
        }

        #endregion
    }
}