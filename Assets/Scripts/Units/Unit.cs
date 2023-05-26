using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.UI;

public interface IUnitType
{
    public int MaxHealth { get; }
    public int Strength { get; }
    public int Speed { get; }
    public int ManaGain { get; }
}

public struct BaseUnit : IUnitType
{
    public int MaxHealth => 5;
    public int Strength => 2;
    public int Speed => 1;
    public int ManaGain => 0;
}

public struct HutUnit : IUnitType
{
    public int MaxHealth => 10;
    public int Strength => 2;
    public int Speed => 1;
    public int ManaGain => 2;
}



[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class Unit : MonoBehaviour
{
    public IUnitType UnitType { get; private set; }
    public int Health { get; private set; }
    public bool IsFighting { get; private set; }

    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team;
    public Slider HealthBar;

    public event NotifyEnterHouse EnterHouse;
    public event NotifyAttackUnit AttackUnit;
    public event NotifyAttackHouse AttackHouse;



    public void Initialize(IUnitType unitType)
    {
        UnitType = unitType;

        Health = UnitType.MaxHealth;
        HealthBar.maxValue = UnitType.MaxHealth;
        UpdateHealthBar();
    }


    // Health Bar //

    private void UpdateHealthBar()
    {
        HealthBar.value = Health;
    }

    public void OnMouseOver()
    {
        HealthBar.gameObject.SetActive(true);
    }

    public void OnMouseExit()
    {
        HealthBar.gameObject.SetActive(false);
    }


    // Movement //

    public void UpdateHeight()
    {
        // last location, next location (direction that the unit is going in, 

        Position = new Vector3(Position.x, WorldMap.Instance.GetHeight(Location), Position.z);
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path, isGuided: true);
    }


    // House Interaction //

    public virtual void OnEnterHouse()
    {
        EnterHouse?.Invoke(this, WorldMap.Instance.GetHouseAtVertex(Location));
    }

    public virtual void OnAttackHouse()
    {
        House house = WorldMap.Instance.GetHouseAtVertex(Location);
        AttackHouse?.Invoke(this, house);
    }


    // Unit Interaction //

    public void OnTriggerEnter(Collider other)
    {
        if (Team != Teams.Red || IsFighting) return;

        if (other.gameObject.layer == gameObject.layer)
        {
            var otherUnit = other.gameObject.GetComponent<Unit>();

            if (Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                StartBattle();
                otherUnit.StartBattle();
                AttackUnit?.Invoke(this, otherUnit);
            }
        }
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
        UpdateHealthBar();
    }
}
