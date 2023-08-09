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
    public int ManaGain => 2;
}



[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class Unit : NetworkBehaviour, IPlayerObject
{
    public IUnitType UnitType { get; private set; }
    public int Health { get; private set; }
    public bool IsFighting { get; private set; }
    public bool IsLeader { get; private set; }
    public bool IsKnight { get; private set; }

    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team;
    public Slider HealthBar;
    public GameObject LeaderMarker;
    public GameObject BattleDetector;
    public GameObject KnightMarker;



    public void Initialize(IUnitType unitType, bool isLeader)
    {
        UnitType = unitType;
        Health = UnitType.MaxHealth;
        IsLeader = isLeader;
    }


    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Swamp"))
            KillUnit();

        if (Team != Teams.Red || IsFighting) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            var otherUnit = other.gameObject.GetComponent<Unit>();

            if (Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                StartBattle();
                otherUnit.StartBattle();
                GameController.Instance.AttackUnit(this, otherUnit);
            }
        }
    }


    public void KillUnit()
    {
        GameController.Instance.DespawnUnit(this, true);
    }



    #region Change Unit Type

    public void MakeLeader()
    {
        IsLeader = true;
        SetLeaderMarkerClientRpc(true);
    }

    [ClientRpc]
    public void SetLeaderMarkerClientRpc(bool active)
    {
        LeaderMarker.SetActive(active);
    }


    public void MakeKnight()
    {
        IsKnight = true;
        BattleDetector.SetActive(true);
        SetKnightMarkerClientRpc(true);
    }

    [ClientRpc]
    public void SetKnightMarkerClientRpc(bool active)
    {
        KnightMarker.SetActive(active);
    }

    #endregion


    #region Health Bar
    public void OnMouseEnter()
    {
        ToggleHealthBarServerRpc(show: true);
    }

    public void OnMouseExit()
    {
        ToggleHealthBarServerRpc(show: false);
    }


    [ServerRpc(RequireOwnership = false)]
    public void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
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
    #endregion


    #region Movement

    public void UpdateHeight()
    {
        int startHeight = WorldMap.Instance.GetHeight(MovementHandler.StartLocation);
        int endHeight = WorldMap.Instance.GetHeight(MovementHandler.EndLocation);

        if (startHeight == endHeight)
            Position = new Vector3(Position.x, startHeight, Position.z);
        else
        {
            float heightDifference = Mathf.Abs(endHeight - startHeight);
            float totalDistance = new Vector2(MovementHandler.EndLocation.X - MovementHandler.StartLocation.X, MovementHandler.EndLocation.Z - MovementHandler.StartLocation.Z).magnitude;

            float distance = startHeight < endHeight 
                ? new Vector2(Position.x - MovementHandler.StartLocation.X, Position.z - MovementHandler.StartLocation.Z).magnitude
                : new Vector2(MovementHandler.EndLocation.X - Position.x, MovementHandler.EndLocation.Z - Position.z).magnitude;

            float height = heightDifference * distance / totalDistance;

            Position = new Vector3(Position.x, startHeight < endHeight ? startHeight + height + GetComponent<MeshRenderer>().bounds.extents.y : endHeight + height + GetComponent<MeshRenderer>().bounds.extents.y, Position.z);
        }
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path, isGuided: true);
    }

    public void EndFollow()
    {
        MovementHandler.EndPath();
    }

    #endregion


    #region House interaction

    public virtual void OnEnterHouse()
    {
        GameController.Instance.EnterHouse(this, WorldMap.Instance.GetHouseAtVertex(Location));
    }

    public virtual void OnAttackHouse()
    {
        House house = WorldMap.Instance.GetHouseAtVertex(Location);
        GameController.Instance.AttackHouse(this, house);
    }

    #endregion


    #region Battle

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

    #endregion
}
