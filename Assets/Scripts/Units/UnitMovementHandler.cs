using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


public class UnitMovementHandler : NetworkBehaviour
{
    private Vector3 Position { get => transform.position; set => transform.position = value; }

    private const float MoveSpeed = 2f;
    private const float PositionError = 0.5f;


    // Following path
    private List<WorldLocation> path;
    private int targetIndex = 0;
    private bool hasNewPath;


    // Roaming
    private const int ViewDistance = 5;
    private const int RoamDistance = 10;
    private int stepsTaken;
    private (int x, int z) roamDirection;
    private Vector3 roamTarget;



    private void Start()
    {
        if (!IsOwner) return;

        roamDirection = (Random.Range(-1, 1), Random.Range(-1, 1));
        roamTarget = Position;
    }


    private void Update()
    {
        if (!IsOwner) return;

        if (hasNewPath)
            ResetPath(keepPath: true);

        if (path != null)
            MoveAlongPath();
        else
            Roam();
    }


    public void SetPath(List<WorldLocation> path)
    {
        this.path = path;
        hasNewPath = true;
    }



    private void Roam()
    {
        if (!MoveToTarget(roamTarget))
        {
            WorldLocation currentLocation = new(roamTarget.x, roamTarget.z);

            //if (!MoveToFlatSpace(currentLocation))
            //{
                if (stepsTaken <= RoamDistance)
                    stepsTaken++;
                else
                {
                    stepsTaken = 0;
                    roamDirection = (Random.Range(-1, 1), Random.Range(-1, 1));
                }

                ChooseNewRoamTarget(currentLocation);
            //}
        }
    }


    private bool MoveToFlatSpace(WorldLocation currentLocation)
    {
        for (int dist = 1; dist <= ViewDistance; ++dist)
        {
            for (int z = dist; z >= -dist; --z)
            {
                float targetZ = currentLocation.Z + z * Chunk.TileWidth;

                if (targetZ < 0 || targetZ >= WorldMap.Width - 1)  // we don't want to check the bottommost edge
                    continue;


                int[] xOffsets;
                // check the whole top row and bottom row, only the edge for the rest (the rest was checked in the last iteration)
                if (z == -dist || z == dist)
                    xOffsets = Enumerable.Range(0, dist).ToArray();
                else
                    xOffsets = new int[]{ dist };


                foreach (int x in xOffsets)
                {
                    float targetX = currentLocation.X + x * Chunk.TileWidth;

                    if (targetX < 0 && targetX >= WorldMap.Width - 1)   // we don't want to check the rightmost edge
                        continue;

                    WorldLocation targetLocation = new(targetX, targetZ);

                    if (IsSpaceFlat(targetLocation))
                    {
                        SetPath(Pathfinding.FindPath(currentLocation, targetLocation));
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool IsSpaceFlat(WorldLocation current)
    {
        for (int z = 0; z <= 1; ++z)
            for (int x = 0; x <= 1; ++x)
                if ((x, z) != (0, 0))
                    if (WorldMap.Instance.GetVertexHeight(current) != WorldMap.Instance.GetVertexHeight(new WorldLocation(current.X + x * Chunk.TileWidth, current.Z + z * Chunk.TileWidth)))
                        return false;
        return true;
    }

    private void ChooseNewRoamTarget(WorldLocation currentLocation)
    {
        // TODO fix bug at (0,0) stops and doesn't move
        // can't find a move

        (float x, float z) target = (currentLocation.X + roamDirection.x * Chunk.TileWidth, currentLocation.Z + roamDirection.z * Chunk.TileWidth);

        while (target.x < 0 || target.z < 0 || target.x >= WorldMap.Width || target.z >= WorldMap.Width || (target.x == currentLocation.X && target.z == currentLocation.Z))
        {
            stepsTaken = 0;
            roamDirection = (Random.Range(-1, 1), Random.Range(-1, 1));
            target = (currentLocation.X + roamDirection.x * Chunk.TileWidth, currentLocation.Z + roamDirection.z * Chunk.TileWidth);
        }

        WorldLocation targetLocation = new(target.x, target.z);

        Debug.Log(currentLocation.X + " " + currentLocation.Z + " " + targetLocation.X + " " + targetLocation.Z);

        roamTarget = new(
            targetLocation.X,
            WorldMap.Instance.GetVertexHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
            targetLocation.Z
        );
    }



    private void MoveAlongPath()
    {
        WorldLocation targetLocation = path[targetIndex];

        Vector3 targetCoordinates = new(
            targetLocation.X,
            WorldMap.Instance.GetVertexHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
            targetLocation.Z
        );

        if (!MoveToTarget(targetCoordinates))
        {
            targetIndex++;

            if (targetIndex >= path.Count)
                ResetPath(keepPath: false);
        }
    }


    private bool MoveToTarget(Vector3 target)
    {
        if (Mathf.Abs(Position.x - target.x) > PositionError ||
            Mathf.Abs(Position.y - target.y) > PositionError ||
            Mathf.Abs(Position.z - target.z) > PositionError)
        {
            Position = Vector3.Lerp(Position, target, MoveSpeed * Time.deltaTime);
            return true;
        }
        return false;
    }


    private void ResetPath(bool keepPath)
    {
        if (!keepPath) path = null;
        targetIndex = 0;
        hasNewPath = false;
    }
}
