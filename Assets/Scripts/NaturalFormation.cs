using UnityEngine;


public enum FormationTypes
{
    Tree,
    Rock,
    Swamp
}

public interface IFormationType
{
    public bool DestroyOnlyAtBottom { get; }
}

public struct Tree : IFormationType
{
    public bool DestroyOnlyAtBottom => false;
}

public struct Rock : IFormationType
{
    public bool DestroyOnlyAtBottom => true;
}

public struct Swamp : IFormationType
{
    public bool DestroyOnlyAtBottom => true;
}



public class NaturalFormation : MonoBehaviour
{
    public FormationTypes Type;
    public IFormationType FormationType { get; private set; }

    private Vector3 Position { get => gameObject.transform.position; }
    private int height;

    public void Awake()
    {
        switch (Type)
        {
            case FormationTypes.Tree: FormationType = new Tree(); break;
            case FormationTypes.Rock: FormationType = new Rock(); break;
            case FormationTypes.Swamp: FormationType = new Swamp(); break;
        }

        height = WorldMap.Instance.GetHeight(new(Position.x, Position.z));
    }

    public bool ShouldDestroy()
    {
        int newHeight = WorldMap.Instance.GetHeight(new(Position.x, Position.z));

        if (newHeight == GameController.Instance.WaterLevel.Value)
            return true;

        if (newHeight != height && !FormationType.DestroyOnlyAtBottom)
            return true;

        return false;
    }
}
