using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>UnitMovementHandler</c> class handles the movement of an individual unit.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(Rigidbody))]
    public class UnitMovementHandler : NetworkBehaviour
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
        [Tooltip("How far away from a point can the unit be for it to register as having reached that point.")]
        [SerializeField] private float m_PositionLeeway = 0.5f;

        [Header("Roaming")]
        [Tooltip("How many tiles in front of itself in the direction the unit is going can it see.")]
        [SerializeField] private int m_ViewTileDistance = 5;
        [Tooltip("How many tiles to either side of itself in the direction the unit is going can it see.")]
        [SerializeField] private int m_ViewTileWidth = 3;
        [Tooltip("Used to make sure that the unit varies its roaming.")]
        [SerializeField] private int m_MaxStepsInRoamDirection = 10;

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

        /// <summary>
        /// True if the unit has the behavior "Go To Unit Magnet" but can't reach the unit magnet.
        /// </summary>
        private bool m_CannotFindMagnet;


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
        /// The <c>Unit</c> this unit is chasing, null if there is none.
        /// </summary>
        private Unit m_TargetUnit = null;
        /// <summary>
        /// The <c>Settlement</c> this unit is going to, null if there is none.
        /// </summary>
        private Settlement m_TargetSettlement = null;

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

            // there is no path, so choose an action
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

            // subscribe to events
            if (m_Unit.Faction == Faction.RED)
            {
                UnitManager.Instance.OnRedLeaderChange += SetGoToMagnetBehavior;
                GameController.Instance.OnRedMagnetMoved += SetGoToMagnetBehavior;
            }
            else if (m_Unit.Faction == Faction.BLUE)
            {
                UnitManager.Instance.OnBlueLeaderChange += SetGoToMagnetBehavior;
                GameController.Instance.OnBlueMagnetMoved += SetGoToMagnetBehavior;
            }

            StructureManager.Instance.OnStructureCreated += CheckTargetFreeTile;

            SetFreeRoam();
        }

        /// <summary>
        /// Unsubscribes from all the events when the movement handler is being destroyed.
        /// </summary>
        public void Cleanup()
        {
            if (m_Unit.Faction == Faction.RED)
            {
                UnitManager.Instance.OnRedLeaderChange -= SetGoToMagnetBehavior;
                GameController.Instance.OnRedMagnetMoved -= SetGoToMagnetBehavior;
            }
            else if (m_Unit.Faction == Faction.BLUE)
            {
                UnitManager.Instance.OnBlueLeaderChange -= SetGoToMagnetBehavior;
                GameController.Instance.OnBlueMagnetMoved -= SetGoToMagnetBehavior;
            }

            StructureManager.Instance.OnStructureCreated -= CheckTargetFreeTile; 
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
            (m_LastMoveState, m_CurrentMoveState) = (m_CurrentMoveState, pause ? MoveState.STOP : m_LastMoveState);
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

            // if we are following a unit, take the next step towards it
            else if (m_CurrentMoveState == MoveState.FOLLOW && m_TargetUnit)
                GetNextStepToFollowTarget();

            // if we have a settlement (that still exists) and we have reached the end of the path, we have reached the settlement
            // the activities in the settlement will be handled in the settlement by the collider
            else if (m_CurrentMoveState == MoveState.GO_TO_SETTLEMENT && m_TargetSettlement)
                SetFreeRoam();

            // if we have reached the unit magnet, roam around it
            else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET && m_IsUnitMagnetReached)
                WanderAroundCurrentPoint();

            // if we haven't reached the unit magnet, keep going towards it
            else if (m_CurrentMoveState == MoveState.GO_TO_MAGNET)
                SetGoToMagnetBehavior();

            else
            {
                SwitchMoveState(MoveState.FREE_MOVE);

                if (m_CurrentRoamDirection == (0, 0) || m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
                {
                    FreeRoam();
                    return;
                }

                // if the behavior is settle and we find a free tile, just go there
                if (m_Unit.Behavior == UnitBehavior.SETTLE && GoToFreeTile())
                    return;

                // if the behavior is gather or fight and there is a direction we can go in to find another unit/settlement, then go
                else if ((m_Unit.Behavior == UnitBehavior.GATHER || m_Unit.Behavior == UnitBehavior.FIGHT) && 
                    (GoToNearbyUnit() || GoToNearbySettlement() || GoInDirection()))
                    return;

                // otherwise just roam
                FreeRoam();
            }

        }

        #endregion


        #region Movement Along Path

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

            TerrainPoint current = m_Unit.ClosestTerrainPoint;

            // check if we have stepped on a point that is surrounded by a free tile
            if (m_CurrentMoveState == MoveState.GO_TO_FREE_TILE)
            {
                TerrainTile? targetTile = null;
                FindSurroundingFreeTile(current, ref targetTile);
                if (targetTile.HasValue)
                {
                    SwitchMoveState(MoveState.GO_TO_FREE_TILE);
                    m_TargetTile = targetTile.Value;
                    ClearPath();
                    return;
                }
            }

            TerrainPoint next = m_Path[m_PathIndex];

            // when Armageddon is active, the units can build their own land to cross water
            if (DivineInterventionController.Instance.IsArmageddon && next.IsUnderwater())
                Terrain.Instance.ModifyTerrain(next, lower: false);

            if (!DivineInterventionController.Instance.IsArmageddon && (next.IsUnderwater() || !IsTileCrossable(current, next)))
            {
                ClearPath();
                return;
            }

            // pick next target
            Vector3 target = m_Path[m_PathIndex].ToScenePosition();

            UnitManager.Instance.AddStepAtPoint(m_Unit.Faction, StartLocation);
            // check if the unit has lost followers
            if (m_PathIndex > 0) m_Steps++;
            if (m_Steps == UnitManager.Instance.UnitDecayRate)
            {
                m_Unit.LoseStrength(1);
                m_Steps = 0;
            }

            // rotate unit to face the next target
            m_Unit.Rotate_ClientRpc((target - Vector3.up * target.y) - (m_StartPosition - Vector3.up * m_StartPosition.y));

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
            if (!DivineInterventionController.Instance.IsArmageddon && end.IsUnderwater()) return false;

            int dx = end.X - start.X;
            int dz = end.Z - start.Z;

            if (Mathf.Abs(dx) != Mathf.Abs(dz)) return true;

            // get the coordinates of the tile
            (int x, int z) = (start.X, start.Z);
            if (dx > 0 && dz < 0) z -= 1;
            else if (dx < 0 && dz > 0) x -= 1;
            else if (dx < 0 && dz < 0)
            {
                x -= 1;
                z -= 1;
            }
            if (x < 0 || z < 0) return false;

            TerrainTile tile = new(x, z);
            if (!tile.IsInBounds() || tile.IsUnderwater()) return false;

            Structure structure = tile.GetStructure();
            if (!structure || structure.GetType() == typeof(Field) || structure.GetType() == typeof(Swamp))
                return true;

            return false;
        }

        #endregion


        #region Free Roam

        /// <summary>
        /// Tells the unit to roam freely.
        /// </summary>
        public void SetFreeRoam()
        {
            ClearPath();
            m_TargetTile = null;
            m_TargetUnit = null;
            m_TargetSettlement = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

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

            SetNewPath(new() { ChooseNextRoamTarget(currentLocation) });
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


        #region Follow Unit

        /// <summary>
        /// Make this unit go after the given unit.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> which we want to go after.</param>
        private void FollowUnit(Unit unit)
        {
            SwitchMoveState(MoveState.FOLLOW);
            m_TargetUnit = unit;
        }

        /// <summary>
        /// Gets the next step this unit should take, going after the target unit.
        /// </summary>
        private void GetNextStepToFollowTarget()
        {
            TerrainPoint? step = AStarPathfinder.FindNextStep(m_Unit.ClosestTerrainPoint, m_TargetUnit.ClosestTerrainPoint);

            if (!step.HasValue)
            {
                if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
                    m_CannotFindMagnet = true;

                SetFreeRoam();
                return;
            }

            if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
                m_CannotFindMagnet = false;

            SetNewPath(new List<TerrainPoint>() { step.Value });
        }

        /// <summary>
        /// Stops following the current target unit if it matches the given unit.
        /// </summary>
        /// <param name="targetUnit"></param>
        public void LoseTargetUnit(Unit targetUnit)
        {
            if (m_TargetUnit != targetUnit) return;
            SetFreeRoam();
        }

        #endregion


        #region Go To Settlement

        /// <summary>
        /// If a path to the given settlement can be found, sets the unit to follow that path.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> the unit should reach.</param>
        /// <returns>True if a path is found, false otherwise.</returns>
        private bool GoToSettlement(Settlement settlement)
        {
            SwitchMoveState(MoveState.GO_TO_SETTLEMENT);

            TerrainPoint current = m_Unit.ClosestTerrainPoint;
            List<TerrainPoint> path = AStarPathfinder.FindPath(current, settlement.OccupiedTile.GetClosestCorner(current));
            if (path == null || path.Count == 0)
            {
                if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
                    m_CannotFindMagnet = true;

                return false;
            }

            if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET)
                m_CannotFindMagnet = false;

            m_TargetSettlement = settlement;
            SetNewPath(path);
            return true;
        }

        /// <summary>
        /// Removes the given settlement as a target for the unit, if it is a target.
        /// </summary>
        /// <param name="targetSettlement">The <c>Settlement</c> that should be removed.</param>
        public void LoseTargetSettlement(Settlement targetSettlement)
        {
            if (m_TargetUnit != targetSettlement) return;
            SetFreeRoam();
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
            List<TerrainPoint> pathToTile = null;

            FindSurroundingFreeTile(current, ref target);

            if (!target.HasValue && m_CurrentRoamDirection != (0, 0) && (m_CurrentRoamDirection.x == 0 || m_CurrentRoamDirection.z == 0))
                FindFreeTile_Parallel();

            else if (!target.HasValue)
                FindFreeTile_Diagonal();

            if (target.HasValue)
            {
                SwitchMoveState(MoveState.GO_TO_FREE_TILE);
                m_TargetTile = target.Value;
                SetNewPath(pathToTile);
                return true;
            }

            return false;

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

                        if (TryGoToTile(new(
                                x: m_CurrentRoamDirection.x == 0 ? widthTarget : distanceTarget, 
                                z: m_CurrentRoamDirection.x == 0 ? distanceTarget : widthTarget))
                            ) 
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

                            if (TryGoToTile(new(targetX, targetZ)))
                                return;
                        }
                    }
                }
            }
        
            /// <summary>
            /// Returns true if the given tile is flat and there is a path to it.
            /// </summary>
            bool TryGoToTile(TerrainTile tile)
            {
                if (!tile.IsFree()) return false;

                List<TerrainPoint> path = AStarPathfinder.FindPath(current, tile.GetClosestCorner(current));
                if (path == null || path.Count == 0) return false;

                target = tile;
                pathToTile = path;
                return true;
            }
        }

        /// <summary>
        /// Get the tile the point the unit is standing on belongs to that is free, if such a tile exists.
        /// </summary>
        /// <param name="current">The <c>TerrainPoint</c> the unit is currently at.</param>
        /// <param name="target">An output parameter for the free <c>TerrainTile</c>, null if none is found.</param>
        private void FindSurroundingFreeTile(TerrainPoint current, ref TerrainTile? target)
        {
            for (int z = 0; z >= -1; --z)
            {
                for (int x = 0; x >= -1; --x)
                {
                    TerrainTile tile = new(current.X + x, current.Z + z);
                    if (!tile.IsFree()) continue;

                    target = tile;
                }
            }
        }

        /// <summary>
        /// Sets the unit's actions when it has reached a flat tile.
        /// </summary>
        private void OnFreeTileReached()
        {
            SwitchMoveState(MoveState.STOP);
            
            if (m_TargetTile.HasValue && m_TargetTile.Value.IsFree())
                StructureManager.Instance.CreateSettlement(m_TargetTile.Value, m_Unit.Faction);

            SetFreeRoam();
        }

        #endregion


        #region Gather / Fight

        /// <summary>
        /// Tries to find a roaming direction for the unit based on where it can find other units and settlements.
        /// </summary>
        /// <returns>True if such a direction is found, false otherwise.</returns>
        private bool GoInDirection()
        {
            Vector3 direction = m_Unit.GetDetectedDirection();
            (int x, int z) roamDirection = (Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
            if (roamDirection == (0, 0)) return false;

            m_CurrentRoamDirection = roamDirection;
            SetNewPath(new() { ChooseNextRoamTarget(m_Unit.ClosestTerrainPoint) });

            return true;
        }

        /// <summary>
        /// Tries to make the unit follow a nearby unit, if such a unit is found.
        /// </summary>
        /// <returns>True if a unit is nearby, false otherwise.</returns>
        private bool GoToNearbyUnit()
        {
            Unit unitInRange = m_Unit.GetUnitInChaseRange();
            if (!unitInRange) return false;

            FollowUnit(unitInRange);
            return true;
        }

        /// <summary>
        /// Tries to make the unit go to a nearby settlement, if such a settlement is found.
        /// </summary>
        /// <returns>True if a settlement is nearby, false otherwise.</returns>
        private bool GoToNearbySettlement()
        {
            Settlement settlementInRange = m_Unit.GetSettlementInChaseRange();
            if (!settlementInRange) return false;

            return GoToSettlement(settlementInRange);
        }

        #endregion


        #region Go To Unit Magnet

        /// <summary>
        /// Sets the unit to go to the unit magnet.
        /// </summary>
        public void SetGoToMagnetBehavior()
        {
            if (m_Unit.Behavior != UnitBehavior.GO_TO_MAGNET) return;

            m_IsUnitMagnetReached = false;
            SwitchMoveState(MoveState.GO_TO_MAGNET);

            // if we have a leader, all walkers follow the leader
            if (m_Unit.Type == UnitType.WALKER && GameController.Instance.HasLeader(m_Unit.Faction))
            {
                GoToLeader();
                return;
            }

            // else, go right to the magnet
            GoToMagnet();
        }

        /// <summary>
        /// Tries to find a path to the unit magnet.
        /// </summary>
        private void GoToMagnet()
        {
            TerrainPoint current = m_Unit.ClosestTerrainPoint;
            TerrainPoint magnet = GameController.Instance.GetUnitMagnetLocation(m_Unit.Faction);

            if (magnet == current)  // we're already at magnet
            {
                m_IsUnitMagnetReached = true;
                return;
            }

            List<TerrainPoint> path = AStarPathfinder.FindPath(current, magnet);
            if (path == null || path.Count == 0)
            {
                m_CannotFindMagnet = true;
                m_Unit.SetCannotFindUnitMagnetSymbol_ClientRpc(true);
                SetFreeRoam();
                return;
            }

            if (m_CannotFindMagnet == true)
            {
                m_CannotFindMagnet = false;
                m_Unit.SetCannotFindUnitMagnetSymbol_ClientRpc(false);
            }

            SetNewPath(path);
        }

        /// <summary>
        /// Sets the unit to go to the leader, if the leader exists.
        /// </summary>
        private void GoToLeader()
        {
            ILeader leader = GameController.Instance.GetLeader(m_Unit.Faction);

            if (leader.GetType() == typeof(Unit))
                FollowUnit((Unit)leader);

            else if (leader.GetType() == typeof(Settlement))
                GoToSettlement((Settlement)leader);

            else
                GoToMagnet();
        }

        /// <summary>
        /// Sets the unit to wander around its current location.
        /// </summary>
        private void WanderAroundCurrentPoint()
        {
            TerrainPoint point = m_Unit.ClosestTerrainPoint;

            List<TerrainPoint> reachableNeighbors = new();
            foreach (TerrainPoint neighbor in point.GetAllNeighbors())
                if (IsTileCrossable(point, neighbor))
                    reachableNeighbors.Add(neighbor);

            // goes to the neightbor, and then back to the starting point
            SetNewPath(new List<TerrainPoint> { reachableNeighbors[new Random().Next(0, reachableNeighbors.Count)], point });
        }

        #endregion


        #region React To Change

        /// <summary>
        /// 
        /// </summary>
        public void ReactToTerrainChange()
            => ReactToTerrainChange(new(0, 0), new(Terrain.Instance.TilesPerSide, Terrain.Instance.TilesPerSide));

        public void ReactToTerrainChange(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            UpdateTargetPointHeight(bottomLeft, topRight);
            CheckTargetFreeTile(bottomLeft, topRight);

            if (m_Unit.Behavior == UnitBehavior.GO_TO_MAGNET && m_CannotFindMagnet)
                SetGoToMagnetBehavior();
        }

        private void UpdateTargetPointHeight(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            Vector3 bottomLeftPosition = bottomLeft.ToScenePosition();
            Vector3 topRightPosition = topRight.ToScenePosition();

            if (!m_TargetPoint.HasValue || m_TargetPoint.Value.x < bottomLeftPosition.x || m_TargetPoint.Value.x > topRightPosition.x ||
                 m_TargetPoint.Value.z < bottomLeftPosition.z || m_TargetPoint.Value.z > topRightPosition.z)
                return;

            Vector3 target = m_TargetPoint.Value;

            TerrainPoint targetPoint = new(target);
            if (targetPoint.IsUnderwater())
            {
                ClearPath();
                return;
            }

            m_TargetPoint = new(
                target.x,
                !m_MoveToCenter ? EndLocation.GetHeight() : new TerrainTile(target.x, target.z).GetCenterHeight(),
                target.z
            );
        }

        private void CheckTargetFreeTile(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            // check if the tile is still free (if it was in the modified area)
            if (!m_TargetTile.HasValue || m_TargetTile.Value.X < bottomLeft.X - 1 || m_TargetTile.Value.X > topRight.X ||
                m_TargetTile.Value.Z < bottomLeft.Z - 1 || m_TargetTile.Value.Z > topRight.Z || m_TargetTile.Value.IsFree())
                return;

            SetFreeRoam();
        }

        private void CheckTargetFreeTile(Structure structure)
        {
            // check if the created structure is on the target tile
            if (!m_TargetTile.HasValue || structure.OccupiedTile != m_TargetTile.Value) return;
            SetFreeRoam();
        }

        #endregion

    }
}