using System.Collections.Generic;
using UnityEngine;


public class UnitMovementHandler : MonoBehaviour
{
    public GameObject Player;
    public Vector3 PlayerCoordinates { get => Player.transform.position; private set => Player.transform.position = value; }
    private Vector3 startPosition;

    private const float moveSpeed = 1f;

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
        Vector3 target = LevelGenerator.Instance.WorldLocationToCoordinates(path[currentNode]);
        target = new Vector3(target.x + 0.5f, target.y + 1.5f, target.z + 0.5f);

        if (PlayerCoordinates != target)
            PlayerCoordinates = Vector3.Lerp(startPosition, target, moveSpeed);
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
        if (!keepPath) path = null;
        currentNode = 0;
        newPath = false;
    }





    public void Move(List<WorldLocation> path)
    {
        this.path = path;

        StartCoroutine(WaitAndMove(0.1f));
    }

    private IEnumerator<WaitForSeconds> WaitAndMove(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        Vector3 target = LevelGenerator.Instance.WorldLocationToCoordinates(path[currentNode]);
        target = new Vector3(target.x + 0.5f, target.y + 1.5f, target.z + 0.5f);

        float startTime = Time.time;
        while (Time.time - startTime <= 2)
        {
            PlayerCoordinates = Vector3.Lerp(PlayerCoordinates, target, Time.time - startTime);
            yield return null;
        }
    }
}
