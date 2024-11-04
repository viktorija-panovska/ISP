using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using System;

namespace Populous
{
    public class UnitMovementHandler : MonoBehaviour
    {
        private enum MoveState
        {
            STOP,
            FREE_MOVE,
            FOLLOW_FLAT_SPACE,
        }

        [SerializeField] private float m_MoveSpeed = 40f;
        [SerializeField] private float m_PositionError = 0.5f;

        [Header("Roaming")]
        [SerializeField] private int m_RoamingViewTileDistance = 5;
        [SerializeField] private int m_RoamingViewTileWidth = 3;
        [SerializeField] private int m_MaxStepsInRoamDirection = 10;
        [SerializeField] private int m_MaxStepsInBattleDirection = 10;
        [SerializeField] private int m_MaxChaseSteps = 10;

        public MapPoint StartLocation { get; private set; }
        public MapPoint EndLocation { get => m_TargetPoint == null ? StartLocation : new(m_TargetPoint.Value.x, m_TargetPoint.Value.z); }

        private Unit m_Unit;
        private Rigidbody m_Rigidbody;

        // Following path
        private Vector3 m_StartPosition;
        private List<MapPoint> m_Path;
        private Vector3? m_TargetPoint;
        private int m_TargetPointIndex = 0;
        private Unit m_UnitToFollow = null;

        private bool m_FlagReached;
        public bool FlagReached { get => m_FlagReached; set => m_FlagReached = value; }

        private bool m_MoveToCenter;
        private (int x, int z) m_RoamDirection;
        private int m_StepsTakenInDirection;
        private readonly (int x, int z)[] m_RoamDirections = new (int, int)[] { (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1) };
        private MapPoint? m_TargetTile;

        private int m_BattleStepsTakenInDirection;
        private int m_ChaseSteps;

        private MoveState m_LastMoveState = MoveState.STOP;
        private MoveState m_CurrentMoveState = MoveState.STOP;


        #region MonoBehaviors

        private void FixedUpdate()
        {
            if (m_TargetPoint == null || m_CurrentMoveState == MoveState.STOP) return;

            Vector3 currentPosition = transform.position;

            if (Vector3.Distance(currentPosition, m_TargetPoint.Value) > m_PositionError)
                m_Rigidbody.MovePosition(currentPosition + (m_MoveSpeed * Time.deltaTime * (m_TargetPoint.Value - currentPosition).normalized));
            else if (m_Path != null)
                ChooseNextPathTarget();
            else
                m_TargetPoint = null;
        }

        #endregion


        #region Control

        public void InitializeMovement()
        {
            m_Unit = GetComponent<Unit>();
            m_Rigidbody = GetComponent<Rigidbody>();

            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;

            SwitchMoveState(MoveState.FREE_MOVE);
            RoamToSettle();
        }

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

        private void SwitchMoveState(MoveState state)
        {
            m_LastMoveState = m_CurrentMoveState;
            m_CurrentMoveState = state;
        }

        #endregion


        #region Path Movement

        public void SetPath(Vector3 end) => SetPath(Pathfinding.FindPath(m_Unit.ClosestMapPoint, new(end.x, end.z)));

        private void SetPath(List<MapPoint> path)
        {
            m_Path = path;
            m_TargetPointIndex = 0;
            m_TargetPoint = m_Path[m_TargetPointIndex].ToWorldPosition();
        }

        private void ChooseNextPathTarget()
        {
            if (m_TargetPointIndex >= m_Path.Count)
            {
                ChooseNextAction();
                return;
            }

            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;

            // pick next target
            Vector3 target = m_Path[m_TargetPointIndex].ToWorldPosition();

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
                m_TargetPointIndex++;
                m_TargetPoint = target;
            }
        }

        private void ChooseNextAction()
        {
            if (m_Unit.IsKnight)
            {
                if (m_UnitToFollow)
                    GetNextStepToTargetUnit();
                else
                {
                    Unit leader = UnitManager.Instance.GetLeader(m_Unit.Team == Team.RED ? Team.BLUE : Team.RED);

                    if (leader)
                        FollowUnit(leader);
                    else
                        RoamToBattleOrGather();
                }

                return;
            }

            if (m_Unit.CurrentState == UnitState.SETTLE)
            {
                if (m_CurrentMoveState == MoveState.FOLLOW_FLAT_SPACE && m_TargetTile.HasValue &&
                    Terrain.Instance.IsTileFlat((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)) &&
                    !Terrain.Instance.IsTileOccupied((m_TargetTile.Value.GridX, m_TargetTile.Value.GridZ)))
                    FlatSpaceFound(m_TargetTile.Value);
                else
                    RoamToSettle();
            }
            else if (m_UnitToFollow && (m_Unit.CurrentState == UnitState.GO_TO_FLAG || m_Unit.CurrentState == UnitState.BATTLE))
                GetNextStepToTargetUnit();
            else if (m_Unit.CurrentState == UnitState.BATTLE || m_Unit.CurrentState == UnitState.GATHER)
                RoamToBattleOrGather();
            else if (m_FlagReached && m_Unit.CurrentState == UnitState.GO_TO_FLAG)
                WanderAroundPoint();
            else
                ResetPath();
        }

        private void ResetPath()
        {
            m_Path = null;
            m_TargetPointIndex = 0;
            m_TargetPoint = null;
        }

        private Vector3 ComputeCenterPosition(Vector3 a, Vector3 b)
        {
            float dx = (b.x - a.x) / Terrain.Instance.UnitsPerTileSide;
            float dz = (b.z - a.z) / Terrain.Instance.UnitsPerTileSide;

            float x = a.x + dx * (Terrain.Instance.UnitsPerTileSide / 2);
            float z = a.z + dz * (Terrain.Instance.UnitsPerTileSide / 2);

            int y = Terrain.Instance.GetTileCenterHeight(
                ((int)(x / (Terrain.Instance.UnitsPerTileSide)),
                 (int)(z / (Terrain.Instance.UnitsPerTileSide)))
            );

            return new(x, y, z);
        }

        private MapPoint GetNeighboringPoint(MapPoint point)
        {
            Random random = new();
            List<MapPoint> neighbors = point.Neighbors;
            MapPoint? neighbor = null;

            while (neighbor == null)
            {
                MapPoint choice = neighbors[random.Next(neighbors.Count)];
                if (Terrain.Instance.CanCrossTile(point, choice))
                    neighbor = choice;
            }

            return neighbor.Value;
        }

        #endregion


        #region Follow Unit

        public void FollowLeader() => FollowUnit(UnitManager.Instance.GetLeader(m_Unit.Team));

        public void FollowUnit(Unit unit)
        {
            m_UnitToFollow = unit;
            GetNextStepToTargetUnit();
        }

        public void StopFollowingUnit(Unit unit)
        {
            if (m_UnitToFollow != unit)
                return;

            m_UnitToFollow = null;
        }

        public void StopFollowingUnit()
        {
            m_UnitToFollow = null;
        }

        private void GetNextStepToTargetUnit()
        {
            MapPoint? step = Pathfinding.FollowUnit(m_Unit.ClosestMapPoint, m_UnitToFollow.ClosestMapPoint);

            if (step == null) return;
            SetPath(new List<MapPoint>() { step.Value });
        }


        #endregion


        #region Special Circumstances

        private void WanderAroundPoint()
        {
            MapPoint lastPoint = m_Unit.ClosestMapPoint;
            SetPath(new List<MapPoint> { GetNeighboringPoint(lastPoint), lastPoint });
        }

        private void FlatSpaceFound(MapPoint tile)
        {
            SwitchMoveState(MoveState.STOP);
            StructureManager.Instance.CreateSettlement(tile, m_Unit.Team);
            m_TargetTile = null;
            SwitchMoveState(MoveState.FREE_MOVE);
        }

        #endregion


        #region Roaming

        public void RoamToSettle()
        {
            ResetPath();

            MapPoint currentLocation = m_Unit.ClosestMapPoint;
            MapPoint? targetTile = FindFreeTile(currentLocation);

            if (targetTile.HasValue)
            {
                if (m_Unit.CurrentState == UnitState.SETTLE)
                    SwitchMoveState(MoveState.FOLLOW_FLAT_SPACE);

                m_TargetTile = targetTile;
                SetPath(targetTile.Value.GetClosestTileCorner(currentLocation).ToWorldPosition());
                return;
            }

            // Roam
            if (m_StepsTakenInDirection <= m_MaxStepsInRoamDirection && m_RoamDirection != (0, 0))
                m_StepsTakenInDirection++;
            else
                ChooseRoamDirection(currentLocation);

            Vector3 target = ChooseNextRoamTarget(currentLocation).ToWorldPosition();
            SetPath(target);
        }

        public void RoamToBattleOrGather()
        {
            ResetPath();

            MapPoint currentLocation = m_Unit.ClosestMapPoint;

            if (m_BattleStepsTakenInDirection == 0 || m_BattleStepsTakenInDirection > m_MaxStepsInBattleDirection || m_RoamDirection == (0, 0))
                ChooseRoamDirection(currentLocation);
            else
                m_BattleStepsTakenInDirection++;

            Vector3 target = ChooseNextRoamTarget(currentLocation).ToWorldPosition();
            SetPath(target);
        }

        private MapPoint? FindFreeTile(MapPoint currentLocation)
        {
            UnitState state = m_Unit.CurrentState;
            MapPoint? target = null;

            FindFreeTile_Surrounding();

            // we are standing next to either a flat space or a house
            if (target == null && m_RoamDirection.x == 0 && m_RoamDirection.z == 0 && m_RoamDirection != (0, 0))
                FindFreeTile_Parallel();
            else if (target == null)
                FindFreeTile_Diagonal();

            return target;


            void FindFreeTile_Surrounding()
            {
                for (int z = 0; z >= -1; --z)
                {
                    for (int x = 0; x >= -1; --x)
                    {
                        if (!Terrain.Instance.IsPointInBounds((currentLocation.GridX + x, currentLocation.GridZ + z)))
                            continue;

                        MapPoint point = new(currentLocation.GridX + x, currentLocation.GridZ + z);

                        if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitState.SETTLE && 
                            (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                            continue;

                        target = point;
                        return;
                    }
                }
            }

            void FindFreeTile_Parallel()
            {
                for (int dist = 1; dist < m_RoamingViewTileDistance; ++dist)
                {
                    int distanceTarget = m_RoamDirection.x == 0 ? (currentLocation.GridZ + m_RoamDirection.z * dist) : (currentLocation.GridX + m_RoamDirection.x * dist);

                    if (distanceTarget < 0 || distanceTarget >= Terrain.Instance.TilesPerSide) continue;

                    for (int width = -m_RoamingViewTileWidth; width < m_RoamingViewTileWidth; ++width)
                    {
                        int widthTarget = m_RoamDirection.x == 0 ? (currentLocation.GridX + width) : (currentLocation.GridZ + width);

                        if (widthTarget < 0 || widthTarget >= Terrain.Instance.TilesPerSide) continue;

                        MapPoint point = m_RoamDirection.x == 0 ? new(widthTarget, distanceTarget) : new(distanceTarget, widthTarget);

                        if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitState.SETTLE && 
                            (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                            continue;

                        target = point;
                        return;
                    }
                }
            }

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
                            int targetX = currentLocation.GridX + m_RoamDirection.x * dx;
                            int targetZ = currentLocation.GridZ + m_RoamDirection.z * dz;

                            if (!Terrain.Instance.IsPointInBounds((targetX, targetZ)))
                                continue;

                            MapPoint point = new(targetX, targetZ);

                            if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || (state == UnitState.SETTLE && 
                                (!Terrain.Instance.IsTileFlat((point.GridX, point.GridZ)) || Terrain.Instance.IsTileOccupied((point.GridX, point.GridZ)))))
                                continue;

                            target = point;
                            return;
                        }
                    }
                }
            }
        }

        private void ChooseRoamDirection(MapPoint currentLocation)
        {
            if (m_Unit.CurrentState == UnitState.BATTLE || m_Unit.CurrentState == UnitState.GATHER)
            {
                Vector3 direction = m_Unit.GetRoamingDirection();

                if (direction != Vector3.zero)
                {
                    m_RoamDirection = (Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
                    return;
                }
            }

            List<(int, int)> availableDirections = new();

            void AddValidDirections((int, int)[] d)
            {
                foreach ((int dx, int dz) in d)
                {
                    (int x, int z) target = (currentLocation.GridX + dx, currentLocation.GridZ + dz);
                    if (!Terrain.Instance.IsPointInBounds(target)) continue;

                    MapPoint point = new(target.x, target.z);

                    if (Terrain.Instance.IsTileUnderwater((point.GridX, point.GridZ)) || !Terrain.Instance.CanCrossTile(currentLocation, point))
                        continue;

                    availableDirections.Add((dx, dz));
                }
            }

            if (m_RoamDirection == (0, 0))
                AddValidDirections(m_RoamDirections);
            else
            {
                int currentDirection = Array.IndexOf(m_RoamDirections, m_RoamDirection);

                // forward directions
                AddValidDirections(new (int, int)[] {
                    m_RoamDirections[GameUtils.NextArrayIndex(currentDirection, -1, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.NextArrayIndex(currentDirection, +1, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.NextArrayIndex(currentDirection, -2, m_RoamDirections.Length)],
                    m_RoamDirections[GameUtils.NextArrayIndex(currentDirection, +2, m_RoamDirections.Length)]
                });

                // backwards directions or continue in the direction we already are going down
                if (availableDirections.Count == 0)
                    AddValidDirections(new (int, int)[] {
                        m_RoamDirections[currentDirection],
                        m_RoamDirections[Helpers.NextArrayIndex(currentDirection, -3, m_RoamDirections.Length)],
                        m_RoamDirections[Helpers.NextArrayIndex(currentDirection, +3, m_RoamDirections.Length)]
                    });

                // only backtrack the way we came if there are no other options
                if (availableDirections.Count == 0)
                    AddValidDirections(new (int, int)[] { 
                        m_RoamDirections[Helpers.NextArrayIndex(currentDirection, +4, m_RoamDirections.Length)] 
                    });
            }

            // if we can't go anywhere, stay in place
            m_RoamDirection = availableDirections.Count > 0 ? availableDirections[new Random().Next(0, availableDirections.Count)] : (0, 0);
        }

        private MapPoint ChooseNextRoamTarget(MapPoint currentLocation)
        {
            (int x, int z) target = (currentLocation.GridX + m_RoamDirection.x, currentLocation.GridZ + m_RoamDirection.z);
            MapPoint targetLocation = new(target.x, target.z);

            if (!Terrain.Instance.IsPointInBounds(target) || !Terrain.Instance.CanCrossTile(currentLocation, targetLocation) ||
                Terrain.Instance.IsTileUnderwater(target))
            {
                m_StepsTakenInDirection = 0;
                ChooseRoamDirection(currentLocation);
                target = (currentLocation.GridX + m_RoamDirection.x, currentLocation.GridZ + m_RoamDirection.z);
                targetLocation = new(target.x, target.z);
            }

            return targetLocation;
        }

        #endregion
    }
}