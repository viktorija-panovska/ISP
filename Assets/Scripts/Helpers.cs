using UnityEngine;

public class Helpers
{
    public static int Modulo(int a, int b) => (a % b + b) % b;

    public static float Round(float value)
        => (value % 0.5) == 0 ? Mathf.Ceil(value) : Mathf.Round(value);
}
