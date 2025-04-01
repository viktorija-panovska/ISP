using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The "Snap To" actions available in the game.
    /// </summary>
    public enum SnapTo
    {
        /// <summary>
        /// Snap the camera to the currently inspected object.
        /// </summary>
        INSPECTED_OBJECT,
        /// <summary>
        /// Snap the camera to the faction's unit magnet.
        /// </summary>
        UNIT_MAGNET,
        /// <summary>
        /// Snap the camera to the faction's leader.
        /// </summary>
        LEADER,
        /// <summary>
        /// Snap the camera to a settlement belonging to the faction.
        /// </summary>
        SETTLEMENT,
        /// <summary>
        /// Snap the camera to an active fight.
        /// </summary>
        FIGHT,
        /// <summary>
        /// Snap the camera to a knight belonging to the faction.
        /// </summary>
        KNIGHT
    }

    /// <summary>
    /// The <c>SnapToObjectController</c> class handles the "Snap Camera to Object" actions (also known as the "Zoom" actions in the original "Populous")
    /// </summary>
    public class SnapToObjectController : NetworkBehaviour
    {
        private static SnapToObjectController m_Instance;
        /// <summary>
        /// Gets a signleton instance of this class;
        /// </summary>
        public static SnapToObjectController Instance { get => m_Instance; }

        /// <summary>
        /// An array containing the index of the next fight the player's camera will snap to if the Zoom to Fight action is performed.
        /// </summary>
        /// <remarks>The fight index at each array index is the fight index of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_FightIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next knight the player's camera will snap to if the Zoom to Knight action is performed.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_KnightsIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next settlement the player's camera will snap to if the Zoom to Settlement action is performed.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_SettlementIndex = new int[2];


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }


        #region Snap Methods

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of the inspected object.
        /// </summary>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToInspectedObject_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            IInspectableObject inspected = QueryModeController.Instance.GetInspectedObject(faction);

            if (inspected == null)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.INSPECTED_OBJECT);
                return;
            }

            GameController.Instance.SetCameraLookPosition_ClientRpc(
                inspected.GameObject.transform.position,
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of the unit magnet.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToUnitMagnet_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            GameController.Instance.SetCameraLookPosition_ClientRpc(
                GameController.Instance.GetUnitMagnetLocation(faction).ToScenePosition(),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of the faction's leader.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToLeader_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            ILeader leader = GameController.Instance.GetLeader(faction);

            if (leader == null)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.LEADER);
                return;
            }

            GameController.Instance.SetCameraLookPosition_ClientRpc(
                leader.GameObject.transform.position,
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of one of the faction's settlements, cycling through them on repeated calls.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToSettlements_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            int factionIndex = (int)faction;

            Vector3? position = StructureManager.Instance.GetSettlementPosition(faction, m_SettlementIndex[factionIndex]);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.SETTLEMENT);
                return;
            }

            GameController.Instance.SetCameraLookPosition_ClientRpc(
                new(position.Value.x, Terrain.Instance.WaterLevel, position.Value.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_SettlementIndex[factionIndex] = GameUtils.GetNextArrayIndex(m_SettlementIndex[factionIndex], 1, StructureManager.Instance.GetSettlementsNumber(faction));
        }

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of one of the ongoing fights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToFights_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            int factionIndex = (int)faction;

            Vector3? position = UnitManager.Instance.GetFightLocation(m_FightIndex[(int)faction]);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.FIGHT);
                return;
            }

            GameController.Instance.SetCameraLookPosition_ClientRpc(
                new(position.Value.x, Terrain.Instance.WaterLevel, position.Value.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_FightIndex[factionIndex] = GameUtils.GetNextArrayIndex(m_FightIndex[factionIndex], 1, UnitManager.Instance.GetFightsNumber());
        }

        /// <summary>
        /// Sends the camera of the player of the given faction to the location of one of the faction's knights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToKnights_ServerRpc(Faction faction, ServerRpcParams serverRpcParams = default)
        {
            int factionIndex = (int)faction;

            Unit knight = UnitManager.Instance.GetKnight(faction, m_KnightsIndex[factionIndex]);
            if (!knight)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.KNIGHT);
                return;
            }

            Vector3 position = knight.transform.position;

            GameController.Instance.SetCameraLookPosition_ClientRpc(
                new(position.x, Terrain.Instance.WaterLevel, position.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_KnightsIndex[factionIndex] = GameUtils.GetNextArrayIndex(m_KnightsIndex[factionIndex], 1, UnitManager.Instance.GetKnightsNumber(faction));
        }

        #endregion


        /// <summary>
        /// Triggers the client's UI to show that snapping the camera to the given object was impossible.
        /// </summary>
        /// <param name="snapOption">The camera snapping option that was attempted.</param>
        [ClientRpc]
        private void NotifyCannotSnap_ClientRpc(SnapTo snapOption) => GameUI.Instance.NotifyCannotSnapTo(snapOption);
    }
}