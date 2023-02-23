using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public class UnitMovementHandler : NetworkBehaviour
{
    private Vector3 Position { get => transform.position; set => transform.position = value; }

    private const float moveSpeed = 2f;
    private const float error = 0.5f;

    private List<WorldLocation> path;
    private int currentNodeIndex = 0;
    private bool hasNewPath;

    private Vector3 randomTarget;



    private void Start()
    {
        randomTarget = Position;
    }


    private void Update()
    {
        if (!IsOwner) return;

        if (!hasNewPath)
        {
            if (!MoveToTarget(randomTarget))
                SetRandomTarget();
        }
        //else
        //{
        //    ResetPath(true);
        //}

        //if (path != null)
        //    MoveUnit();
    }

    private bool MoveToTarget(Vector3 target)
    {
        if (Mathf.Abs(Position.x - target.x) > error ||
            Mathf.Abs(Position.y - target.y) > error ||
            Mathf.Abs(Position.z - target.z) > error)
        {
            Position = Vector3.Lerp(Position, target, moveSpeed * Time.deltaTime);
            return true;
        }

        return false;
    }

    private void SetRandomTarget()
    {
        WorldLocation currentLocation = new WorldLocation(Position.x, Position.z);
        List<WorldLocation> neighbors = currentLocation.GetNeighboringLocations();

        WorldLocation targetLocation = neighbors[Random.Range(0, neighbors.Count - 1)];

        randomTarget = new Vector3(
            targetLocation.X, 
            WorldMap.GetVertexHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.size.y / 2, 
            targetLocation.Z
        );
    }



    #region
    public void SetPath(List<WorldLocation> path)
    {
        this.path = path;
        hasNewPath = true;
    }


    private void MoveUnit()
    {
        WorldLocation current = path[currentNodeIndex];

        Vector3 target = new(
            current.X,
            WorldMap.GetVertexHeight(current) + GetComponent<MeshRenderer>().bounds.size.y / 2,
            current.Z
        );

        if (Mathf.Abs(Position.x - target.x) > error ||
            Mathf.Abs(Position.y - target.y) > error ||
            Mathf.Abs(Position.z - target.z) > error)
        {
            Position = Vector3.Lerp(Position, target, moveSpeed * Time.deltaTime);
        }
        else
        {
            currentNodeIndex++;

            if (currentNodeIndex >= path.Count)
                ResetPath(false);
        }
    }

    private void ResetPath(bool keepPath)
    {
        if (!keepPath) path = null;
        currentNodeIndex = 0;
        hasNewPath = false;
    }
    #endregion
}
