using System.Collections.Generic;
using UnityEngine;

public class UnitMovementHandler : MonoBehaviour
{
    public GameObject Player;
    public Vector3 PlayerCoordinates { get => Player.transform.position; private set => Player.transform.position = value; }
    private Vector3 startPosition;

    private const float moveSpeed = 1f;
    private float timer = 0;

    private List<WorldLocation> path;
    private int currentNode;
    private bool newPath;


    private void Start()
    {
        startPosition = PlayerCoordinates;
    }

    private void Update()
    {
        if (newPath)
            Reset(true);

        if (path != null)
            MoveUnit();
    }


    public void SetPath(List<WorldLocation> path)
    {
        this.path = path;
        newPath = true;
    }

    private void MoveUnit()
    {
        timer += Time.deltaTime * moveSpeed;

        Vector3 target = LevelGenerator.Instance.WorldLocationToCoordinates(path[currentNode]);
        target = new Vector3(target.x + 0.5f, target.y + 1.5f, target.z + 0.5f);

        if (PlayerCoordinates != target)
            PlayerCoordinates = Vector3.Lerp(startPosition, target, timer);
        else
        {
            currentNode++;

            if (currentNode >= path.Count)
                Reset(false);
        }
    }

    private void Reset(bool keepPath)
    {
        startPosition = PlayerCoordinates;
        timer = 0;
        if (!keepPath) path = null;
        currentNode = 0;
        newPath = false;
    }
}
