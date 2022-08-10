using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UnitClass { Walker, Leader, Knight };

public enum Owner { Player1, Player2 };


public class Unit : MonoBehaviour
{
    public UnitClass UnitClass;

    public Owner Owner;

    // 100% being the most powerful
    public float RelativeStrength;

    public int TotalHealth;

    public int RemainingHealth { get; }
}