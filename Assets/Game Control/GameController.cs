using Unity.Netcode;
using UnityEngine;


public enum Power
{
    MOLD_TERRAIN,
    GUIDE_FOLLOWERS,
    EARTHQUAKE,
    SWAMP,
    KNIGHT,
    VOLCANO,
    FLOOD,
    ARMAGHEDDON
}


/// <summary>
/// The <c>GameController</c> class 
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class GameController : NetworkBehaviour
{
    private static GameController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static GameController Instance { get => m_Instance; }

    public Team Winner;  // TODO: Remove - for testing only

    // Powers
    public const int MIN_MANA = 0;
    public const int MAX_MANA = 100;
    public readonly int[] PowerActivationThreshold = { 0, 13, 30, 45, 62, 78, 92, 100 };
    public readonly int[] PowerMannaCost = { 0, 10, 20, 30, 40, 50, 92, 100 };

    private int m_WaterLevel;
    public int WaterLevel { get => m_WaterLevel; }



    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    #endregion


    #region Powers


    #region MoldTerrain

    public void MoldTerrain(MapPoint point, bool lower)
    {
        UpdateMap(point, lower);
        //if (IsHost)
        //    UpdateMap(point, lower);
        //else
        //    UpdateMapServerRpc(point, lower);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateMapServerRpc(MapPoint point, bool lower)
    {
        UpdateMap(point, lower);
    }

    private void UpdateMap(MapPoint point, bool lower)
    {
        Terrain.Instance.ModifyTerrain(point, lower);
    }


    #endregion

    #endregion
}