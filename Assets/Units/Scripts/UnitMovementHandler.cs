using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Populous
{
    public class UnitMovementHandler : MonoBehaviour
    {
        [SerializeField] private float m_MoveSpeed = 40f;
        [SerializeField] private float m_PositionError = 0.5f;

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


        public void InitializeMovement()
        {
            m_Unit = GetComponent<Unit>();
            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;
        }

        private void Start()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (m_TargetPoint == null) return;

            Vector3 currentPosition = transform.position;

            if (Vector3.Distance(currentPosition, m_TargetPoint.Value) > m_PositionError)
                m_Rigidbody.MovePosition(currentPosition + (m_MoveSpeed * Time.deltaTime * (m_TargetPoint.Value - currentPosition).normalized));
            else if (m_Path != null)
                ChooseNextPathTarget();
            else
                m_TargetPoint = null;
        }


        public void SetPath(Vector3 end)
            => SetPath(Pathfinding.FindPath(m_Unit.ClosestMapPoint, new(end.x, end.z)));

        private void SetPath(List<MapPoint> path)
        {
            m_Path = path;
            m_TargetPointIndex = 0;
            m_TargetPoint = m_Path[m_TargetPointIndex].ToWorldPosition();
        }

        public void FollowLeader()
        {
            m_UnitToFollow = UnitManager.Instance.GetLeader(m_Unit.Team);
            GetNextStepToLeader();
        }

        private void GetNextStepToLeader()
        {
            MapPoint? step = Pathfinding.FollowUnit(m_Unit.ClosestMapPoint, m_UnitToFollow.ClosestMapPoint);
            if (step == null) return;
            SetPath(new List<MapPoint>() { step.Value });
        }

        private void ChooseNextPathTarget()
        {
            if (m_TargetPointIndex >= m_Path.Count)
            {
                if (m_Unit.CurrentState == UnitState.GO_TO_FLAG && !m_Unit.IsLeader && m_UnitToFollow)
                    GetNextStepToLeader();
                else if (m_FlagReached && m_Unit.CurrentState == UnitState.GO_TO_FLAG)
                    WanderAroundPoint();
                else
                    EndPath();

                return;
            }

            m_StartPosition = m_Unit.ClosestMapPoint.ToWorldPosition();
            StartLocation = m_Unit.ClosestMapPoint;

            // pick next target
            Vector3 target = m_Path[m_TargetPointIndex].ToWorldPosition();

            // rotate unit to face the next target
            m_Unit.RotateClientRpc((target - Vector3.up * target.y) - (m_StartPosition - Vector3.up * m_StartPosition.y));

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

        private void WanderAroundPoint()
        {
            MapPoint lastPoint = m_Unit.ClosestMapPoint;
            SetPath(new List<MapPoint> { GetNeighboringPoint(lastPoint), lastPoint });
        }

        private void EndPath()
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
                if (!Terrain.Instance.IsCrossingStructure(point, choice))
                    neighbor = choice;
            }

            return neighbor.Value;
        }

    }
}