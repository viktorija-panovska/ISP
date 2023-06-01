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
    public int ManaGain => 10;
}

public struct HutUnit : IUnitType
{
    public int MaxHealth => 10;
    public int Strength => 2;
    public int Speed => 1;
    public int ManaGain => 10;
}



[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class Unit : NetworkBehaviour
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
    }


    // Health Bar //

    public void OnMouseEnter()
    {
        ToggleHealthBarServerRpc(show: true);
    }

    public void OnMouseExit()
    {
        ToggleHealthBarServerRpc(show: false);
    }


    [ServerRpc(RequireOwnership = false)]
    private void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
    {
        ToggleHealthBarClientRpc(show, UnitType.MaxHealth, Health, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { parameters.Receive.SenderClientId }
            }
        });        
    }

    [ClientRpc]
    private void ToggleHealthBarClientRpc(bool show, int maxHealth, int currentHealth, ClientRpcParams parameters = default)
    {
        if (!show)
        {
            HealthBar.gameObject.SetActive(false);
            return;
        }

        HealthBar.maxValue = maxHealth;
        HealthBar.value = currentHealth;
        HealthBar.gameObject.SetActive(true);
    }

    [ClientRpc]
    private void UpdateHealthBarClientRpc(int maxHealth, int currentHealth, ClientRpcParams parameters = default)
    {
        if (HealthBar.gameObject.activeSelf)
        {
            HealthBar.maxValue = maxHealth;
            HealthBar.value = currentHealth;
        }
    }



    // Movement //

    public void UpdateHeight()
    {
        // last location, next location (direction that the unit is going in, 

        /*Given 2 points, (x1,y1,z1) and (x2,y2,z2), you can take the difference between the two, so you end up with (x2-x1,y2-y1,z2-z1). 
         * Take the norm of this (i.e. take the distance between the original 2 points), and divide (x2-x1,y2-y1,z2-z1) by that value. 
         * You now have a vector with the same slope as the line between the first 2 points, but it has magnitude one, 
         * since you normalized it (by dividing by its magnitude). 
         * Then add/subtract that vector to one of the original points to get your final answer.*/

        Debug.Log("HI");

        //Position = new Vector3(Position.x, WorldMap.Instance.GetHeight(Location), Position.z);
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

        UpdateHealthBarClientRpc(UnitType.MaxHealth, Health);
    }
}
