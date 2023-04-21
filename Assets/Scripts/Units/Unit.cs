using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class Unit : MonoBehaviour
{
    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team { get; private set; }
    public int Health { get; private set; }
    public int Strength { get; private set; }
    public int Speed { get; private set; }

    public event NotifyEnterHouse EnterHouse;
    public event NotifyBattleBegin BattleBegin;


    public void Awake()
    {
        Health = 10;
        Strength = 2;
        Speed = 1;
    }


    public void SetTeam(ulong ownerId)
    {
        if (ownerId == 0)
            Team = Teams.Red;
        else
            Team = Teams.Blue;
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path);
    }

    public void TakeDamage(int damage)
    {
        Health -= damage;
    }

    public virtual void OnEnterHouse()
    {
        EnterHouse?.Invoke(gameObject, WorldMap.Instance.GetHouseAtVertex(Location));
    }


    public void UpdateHeight()
    {
        Position = new Vector3(Position.x, WorldMap.Instance.GetHeight(Location));
    }


    public void OnTriggerEnter(Collider other)
    {
        if (Team != Teams.Red) return;

        if (other.gameObject.layer == gameObject.layer)
        {
            var otherUnit = other.gameObject.GetComponent<Unit>();

            if (Team != otherUnit.Team)
            {
                MovementHandler.Stop();
                other.GetComponent<UnitMovementHandler>().Stop();
                BattleBegin?.Invoke(gameObject, other.gameObject);
            }
        }
    }
}
