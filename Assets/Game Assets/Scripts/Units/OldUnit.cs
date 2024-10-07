using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.UI;


public interface IUnitType
{
    public int MaxHealth { get; }
    public int Strength { get; }
    public int Speed { get; }
    public float ManaGain { get; }
}

public struct BaseUnit : IUnitType
{
    public readonly int MaxHealth => 5;
    public readonly int Strength => 2;
    public readonly int Speed => 1;
    public readonly float ManaGain => 0;
}

public struct TentUnit : IUnitType
{
    public readonly int MaxHealth => 10;
    public readonly int Strength => 4;
    public readonly int Speed => 2;
    public readonly float ManaGain => 0.5f;
}

public struct HutUnit : IUnitType
{
    public readonly int MaxHealth => 15;
    public readonly int Strength => 6;
    public readonly int Speed => 3;
    public readonly float ManaGain => 1;
}

public struct WoodHouseUnit : IUnitType
{
    public readonly int MaxHealth => 20;
    public readonly int Strength => 8;
    public readonly int Speed => 4;
    public readonly float ManaGain => 1.5f;
}

public struct StoneHouseUnit : IUnitType
{
    public readonly int MaxHealth => 25;
    public readonly int Strength => 10;
    public readonly int Speed => 5;
    public readonly float ManaGain => 2;
}

public struct FortressUnit : IUnitType
{
    public readonly int MaxHealth => 30;
    public readonly int Strength => 12;
    public readonly int Speed => 6;
    public readonly float ManaGain => 2.5f;
}

public struct CityUnit: IUnitType
{
    public readonly int MaxHealth => 35;
    public readonly int Strength => 14;
    public readonly int Speed => 7;
    public readonly float ManaGain => 3;
}


public enum UnitStates
{
    Settle,
    Battle
}


[RequireComponent(typeof(NetworkObject), typeof(UnitMovementHandler))]
public class OldUnit : NetworkBehaviour, IPlayerObject
{
    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public IUnitType UnitType { get; private set; }
    public UnitStates UnitState { get; private set; }

    public int Health { get; private set; }
    public bool IsLeader { get; private set; }
    public bool IsKnight { get; private set; }
    public bool IsFighting { get; private set; }
    public bool IsFollowed { get; set; }
    public House OriginHouse { get; private set; }
    public OldUnit ChasedBy { get; set; }

    public Vector3 Position { get => gameObject.transform.position; set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }
    public float Height { get => WorldMap.Instance.GetHeight(Location); }
    public WorldLocation NextLocation { get => MovementHandler.EndLocation; }

    public Team Team { get; private set; }
    public Slider HealthBar;
    public GameObject LeaderMarker;
    public GameObject BattleDetector;
    public GameObject KnightMarker;


    // Animation
    private Animator Animator { get => GetComponentInChildren<Animator>(); }
    private const string IDLE_STATE = "Idle";
    private const string WALK_STATE = "Walk";
    private const string ATTACK_STATE = "Attack";
    private const string SWORD_STATE = "SwordAttack";


    public void Initialize(IUnitType unitType, Team team, House originHouse, bool isLeader)
    {
        UnitType = unitType;
        Team = team;
        OriginHouse = originHouse;
        Health = UnitType.MaxHealth;
        IsLeader = isLeader;

        UnitState = UnitStates.Settle;

        MovementHandler.Initialize();
    }



    #region Change Unit Type

    public void MakeSettleUnit()
    {
        UnitState = UnitStates.Settle;
        BattleDetector.SetActive(false);
    }

    public void MakeBattleUnit()
    {
        UnitState = UnitStates.Battle;
        BattleDetector.SetActive(true);
    }


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
        IsLeader = false;
        IsKnight = true;
        MakeBattleUnit();
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

    public void Rotate(Vector3 lookPosition)
    {
        transform.rotation = Quaternion.LookRotation(lookPosition);
    }

    public void UpdateHeight()
    {
        float height;

        int startHeight = WorldMap.Instance.GetHeight(MovementHandler.StartLocation);
        int endHeight = WorldMap.Instance.GetHeight(MovementHandler.EndLocation);

        if (startHeight == endHeight)
            height = startHeight;
        else
        {
            float heightDifference = Mathf.Abs(endHeight - startHeight);
            float totalDistance = new Vector2(MovementHandler.EndLocation.X - MovementHandler.StartLocation.X, MovementHandler.EndLocation.Z - MovementHandler.StartLocation.Z).magnitude;

            float distance = startHeight < endHeight
                ? new Vector2(Position.x - MovementHandler.StartLocation.X, Position.z - MovementHandler.StartLocation.Z).magnitude
                : new Vector2(MovementHandler.EndLocation.X - Position.x, MovementHandler.EndLocation.Z - Position.z).magnitude;

            height = heightDifference * distance / totalDistance;
            height = startHeight < endHeight ? startHeight + height : endHeight + height;
        }

        if (height <= OldGameController.Instance.WaterLevel.Value)
            KillUnit();
        else
            Position = new Vector3(Position.x, height, Position.z);
    }

    public void MoveAlongPath(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path, isGuided: true);
    }

    public void FollowUnit(OldUnit unit)
    {
        MovementHandler.SetFollowingUnit(unit);
    }

    public void EndFollow()
    {
        MovementHandler.EndFollow();
    }

    public void ResumeMovement()
    {
        MovementHandler.Resume();
    }

    public void StopMovement()
    {
        MovementHandler.Stop();
    }

    private void LookAt(GameObject target)
    {
        var lookPos = target.transform.position - transform.position;
        lookPos.y = 0;
        Rotate(lookPos);
    }

    #endregion


    #region House interaction

    public virtual void EnterHouse()
    {
        OldGameController.Instance.EnterHouse(this, (House)WorldMap.Instance.GetHouseAtVertex(Location));
    }

    public virtual void AttackHouse()
    {
        House house = (House)WorldMap.Instance.GetHouseAtVertex(Location);
        LookAt(house.gameObject);
        OldGameController.Instance.AttackHouse(this, house);
    }

    #endregion


    #region Battle

    public void StartBattle(OldUnit otherUnit)
    {
        LookAt(otherUnit.gameObject);

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

    public void KillUnit()
    {
        OldGameController.Instance.DespawnUnit(this, true);
    }

    #endregion


    #region Animation

    public float GetCurrentAnimationLength() => Animator.GetCurrentAnimatorStateInfo(0).length;

    private void ChangeAnimationState(string state)
    {
        Animator.Play(state);
    }

    public void PlayIdleAnimation() => ChangeAnimationState(IDLE_STATE);

    public void PlayWalkAnimation() => ChangeAnimationState(WALK_STATE);

    public void PlayAttackAnimation() => ChangeAnimationState(ATTACK_STATE);

    #endregion

}
