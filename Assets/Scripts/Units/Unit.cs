using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class Unit : MonoBehaviour
{
    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team;

    public int Health { get; private set; }
    public int Strength { get; private set; }
    public int Speed { get; private set; }

    public bool IsFighting { get; private set; }

    public event NotifyEnterHouse EnterHouse;
    public event NotifyAttackUnit BattleBegin;
    public event NotifyAttackHouse AttackHouse;


    public void Awake()
    {
        Health = 10;
        Strength = 2;
        Speed = 1;
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path);
    }

    public void StartBattle()
    {
        IsFighting = true;
        MovementHandler.Stop();
    }

    public void EndBattle()
    {
        IsFighting = false;
        MovementHandler.Resume();
    }

    public void TakeDamage(int damage)
    {
        Health -= damage;
    }



    public virtual void OnEnterHouse()
    {
        EnterHouse?.Invoke(this, WorldMap.Instance.GetHouseAtVertex(Location));
    }

    public virtual void OnAttackHouse()
    {
        AttackHouse?.Invoke(this, WorldMap.Instance.GetHouseAtVertex(Location));
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

            if (Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                StartBattle();
                otherUnit.StartBattle();
                BattleBegin?.Invoke(this, otherUnit);
            }
        }
    }
}
