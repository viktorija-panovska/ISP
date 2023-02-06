using System.Collections.Generic;
using UnityEngine;


public class UnitMovementHandler : MonoBehaviour
{
    public GameObject Player;
    public Vector3 PlayerCoordinates { get => Player.transform.position; private set => Player.transform.position = value; }

    private const float moveSpeed = 2f;
    private const float error = 2f;

    private List<WorldLocation> path;
    private int currentNodeIndex = 0;
    private bool hasNewPath;


    private void Update()
    {
        // TODO: Canceling trajectory and switching to a different one
        if (hasNewPath)
            Reset(true);

        if (path != null)
            MoveUnit();
    }

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
            WorldMap.GetVertexHeight(current),  // TODO: height offset
            current.Z
        );

        if (Mathf.Abs(PlayerCoordinates.x - target.x) > error ||
            Mathf.Abs(PlayerCoordinates.y - target.y) > error ||
            Mathf.Abs(PlayerCoordinates.z - target.z) > error)
        {
            PlayerCoordinates = Vector3.Lerp(PlayerCoordinates, target, moveSpeed * Time.deltaTime);
        }
        else
        {
            currentNodeIndex++;

            if (currentNodeIndex >= path.Count)
                Reset(false);
        }
    }

    private void Reset(bool keepPath)
    {
        if (!keepPath) path = null;
        currentNodeIndex = 0;
        hasNewPath = false;
    }
}
