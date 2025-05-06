using System.Collections.Generic;
using System.Linq;
using System;
using Unity.Netcode;
using UnityEngine.EventSystems;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>IInspectableObject</c> interface defines methods necessary for classes representing objects that can be inspected when the player is in Query Mode.
    /// </summary>
    public interface IInspectableObject : IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>
        /// The GameObject associated with the class.
        /// </summary>
        public GameObject GameObject { get; }

        /// <summary>
        /// True if the object is currently being inspected by either player, false otherwise.
        /// </summary>
        public bool IsInspected { get; set; }

        /// <summary>
        /// Activates or deactivates the highlight of the object.
        /// </summary>
        /// <param name="shouldActivate">True if the highlight should be activated, false otherwise.</param>
        public void SetHighlight(bool shouldActivate);
    }


    /// <summary>
    /// The <c>QueryModeController</c> handles the inspecting of the objects when a player is in Query Mode.
    /// </summary>
    public class QueryModeController : NetworkBehaviour
    {
        private static QueryModeController m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static QueryModeController Instance { get => m_Instance; }

        /// <summary>
        /// Each cell represents one of the factions, and the object in the cell is the object that faction's player is inspecting.
        /// </summary>
        /// <remarks>The object at each index is the inspected object of the player of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly IInspectableObject[] m_InspectedObjects = new IInspectableObject[2];


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }


        /// <summary>
        /// Gets the object the player of the given faction is inspecting.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose inspected object should be returned.</param>
        /// <returns>The <c>IInspectableObject</c> that the player is inspecting, null if there is none.</returns>
        public IInspectableObject GetInspectedObject(Faction faction) => m_InspectedObjects[(int)faction];

        /// <summary>
        /// Gets the faction of the player that is inspecting the given object.
        /// </summary>
        /// <param name="inspectedObject">The object that is being checked.</param>
        /// <returns>The value of the faction whose player is inspecting the object in the <c>Faction</c> enum, -1 if the object is not being inspected.</returns>
        public int GetPlayerInspectingObject(IInspectableObject inspectedObject) => Array.IndexOf(m_InspectedObjects, inspectedObject);

        /// <summary>
        /// Sets the given object as the object being inspected by the player of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that's inspecting the object.</param>
        /// <param name="inspectedObject">A <c>NetworkObjectReference</c> of the object being inspected.</param>
        /// <param name="serverRpcParams">RPC parameters for the server RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetInspectedObject_ServerRpc(Faction faction, NetworkObjectReference inspectedObject, ServerRpcParams serverRpcParams = default)
        {
            if (!inspectedObject.TryGet(out NetworkObject networkObject) || networkObject.GetComponent<IInspectableObject>() == null)
                return;

            IInspectableObject inspectObject = networkObject.GetComponent<IInspectableObject>();
            IInspectableObject lastInspectedObject = m_InspectedObjects[(int)faction];

            // stop inspecting the last inspected object
            if (lastInspectedObject != null)
            {
                SetHighlight_ClientRpc(
                    isOn: false,
                    lastInspectedObject.GameObject.GetComponent<NetworkObject>(),
                    GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
                );

                m_InspectedObjects[(int)faction] = null;
                lastInspectedObject.IsInspected = false;
                HideInspectedObjectPanel_ClientRpc(GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId));
            }

            // this will make it so clicking again on an inspected object will just stop inspecting it.
            if (lastInspectedObject == inspectObject) return;

            ClientRpcParams clientParams = GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId);

            m_InspectedObjects[(int)faction] = inspectObject;
            inspectObject.IsInspected = true;

            SetHighlight_ClientRpc(
                isOn: true,
                inspectObject.GameObject.GetComponent<NetworkObject>(),
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            );

            if (inspectObject.GetType() == typeof(Unit))
            {
                Unit unit = (Unit)inspectObject;

                if (unit.IsInFight)
                {
                    (Unit red, Unit blue) = UnitManager.Instance.GetFightParticipants(unit.FightId);
                    ShowFightData_ClientRpc(red.Strength, blue.Strength, clientParams);
                    return;
                }

                ShowUnitData_ClientRpc(unit.Faction, unit.Type, unit.Strength, clientParams);
            }

            if (inspectObject.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)inspectObject;
                ShowSettlementData_ClientRpc(settlement.Faction, settlement.Type, settlement.FollowersInSettlement, settlement.Capacity, clientParams);
            }
        }

        /// <summary>
        /// Stops inspecting the given object, if any player is inspecting it.
        /// </summary>
        /// <remarks>Called when the object is despawned.</remarks>
        /// <param name="inspectedObject">The object that should be removed from being inspected.</param>
        public void RemoveInspectedObject(IInspectableObject inspectedObject)
        {
            int index = GetPlayerInspectingObject(inspectedObject);

            // nobody is inspecting the object
            if (index < 0) return;

            m_InspectedObjects[index] = null;
            inspectedObject.IsInspected = false;

            HideInspectedObjectPanel_ClientRpc(GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(index)));
        }

        /// <summary>
        /// Turns on/off the highlight indicating that the object is being inspected.
        /// </summary>
        /// <param name="isOn">True if the highlight should be turned on, false otherwise,</param>
        /// <param name="inspectedObject">A reference to the <c>IInspectedObject</c> that is inspected.</param>
        /// <param name="clientRpcParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void SetHighlight_ClientRpc(bool isOn, NetworkObjectReference inspectedObject, ClientRpcParams clientRpcParams)
        {
            if (!inspectedObject.TryGet(out NetworkObject networkObject) || networkObject.GetComponent<IInspectableObject>() == null)
                return;

            networkObject.GetComponent<IInspectableObject>().SetHighlight(isOn);
        }


        #region Show/Hide Inspected Object UI

        /// <summary>
        /// Tells the client to show the given data on the Inspected Unit panel.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit belongs to.</param>
        /// <param name="type">The type of the unit.</param>
        /// <param name="strength">The current strength of the unit.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowUnitData_ClientRpc(Faction faction, UnitType type, int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowUnitData(faction, type, strength);

        /// <summary>
        /// Tells the client to show the given data on the Inspected Settlement panel.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the settlement belongs to.</param>
        /// <param name="type">The type of the settlement.</param>
        /// <param name="unitsInSettlement">The number of units currently in the settlement.</param>
        /// <param name="maxUnitsInSettlement">The maximum number of units for the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowSettlementData_ClientRpc(Faction faction, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowSettlementData(faction, type, unitsInSettlement, maxUnitsInSettlement);

        /// <summary>
        /// Tells the client to show the given data on the Inspected Fight panel.
        /// </summary>
        /// <param name="redStrength">The current strength of the red unit in the fight.</param>
        /// <param name="blueStrength">The current strength of the blue unit in the fight.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowFightData_ClientRpc(int redStrength, int blueStrength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowFightData(redStrength, blueStrength);

        /// <summary>
        /// Tells the client to hide the inspected object panel.
        /// </summary>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void HideInspectedObjectPanel_ClientRpc(ClientRpcParams clientParams = default)
            => GameUI.Instance.HideInspectedObjectPanel();

        #endregion


        #region Update Inspected UI

        #region Unit

        /// <summary>
        /// Handles the update of the given unit data on the UI of the player focusing on the unit.
        /// </summary>
        /// <param name="unit">The unit whose UI data should be updated.</param>
        /// <param name="updateType">True if the unit's type should be updated, false otherwise.</param>
        /// <param name="updateStrength">True if the unit's strength should be updated, false otherwise.</param>
        public void UpdateInspectedUnit(Unit unit, bool updateType = false, bool updateStrength = false)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i] != null && m_InspectedObjects[i].GameObject == unit.GameObject);

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
            {
                ClientRpcParams clientParams = GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex));

                if (updateType)
                    UpdateInspectedUnitType_ClientRpc(unit.Type, clientParams);

                if (updateStrength)
                    UpdateInspectedUnitStrength_ClientRpc(unit.Strength, clientParams);
            }
        }

        /// <summary>
        /// Triggers the update of the inspected unit's type on the UI of the client.
        /// </summary>
        /// <param name="type">The new type that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitType_ClientRpc(UnitType type, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitType(type);

        /// <summary>
        /// Triggers the update of the inspected unit's strength on the UI of the client.
        /// </summary>
        /// <param name="strength">The new strength that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitStrength_ClientRpc(int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitStrength(strength);

        #endregion


        #region Settlement

        /// <summary>
        /// Handles the update of the given settlement data on the UI of the player focusing on the settlement.
        /// </summary>
        /// <param name="settlement">The settlement whose UI data should be updated.</param>
        /// <param name="updateFaction">True if the settlement's faction should be updated, false otherwise.</param>
        /// <param name="updateType">True if the settlement's type should be updated, false otherwise.</param>
        /// <param name="updateFollowers">True if the amount of followers in the settlement should be updated, false otherwise.</param>
        public void UpdateInspectedSettlement(Settlement settlement, bool updateFaction = false, bool updateType = false, bool updateFollowers = false)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i] != null && m_InspectedObjects[i].GameObject == settlement.GameObject);

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
            {
                ClientRpcParams clientParams = GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex));

                if (updateFaction)
                    UpdateInspectedSettlementFaction_ClientRpc(settlement.Faction, clientParams);

                if (updateType)
                    UpdateInspectedSettlementType_ClientRpc(settlement.Type, settlement.Capacity, clientParams);

                if (updateFollowers)
                    UpdateInspectedSettlementFollowers_ClientRpc(settlement.FollowersInSettlement, clientParams);
            }
        }

        /// <summary>
        /// Triggers the update of the inspected settlement's faction on the UI of the client.
        /// </summary>
        /// <param name="faction">The new <c>Faction</c> of the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementFaction_ClientRpc(Faction faction, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementFaction(faction);

        /// <summary>
        /// Triggers the update of the inspected settlement's type on the UI of the client.
        /// </summary>
        /// <param name="type">The new type of the settlement.</param>
        /// <param name="maxUnitsInSettlement">The new maximum amount of units in the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementType_ClientRpc(SettlementType type, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementType(type, maxUnitsInSettlement);

        /// <summary>
        /// Triggers the update of the number of followers in the inspected settlement on the UI of the client.
        /// </summary>
        /// <param name="followers">The new number of followers.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementFollowers_ClientRpc(int followers, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementFollowers(followers);

        #endregion


        #region Fight

        /// <summary>
        /// Handles the update of the strengths of the given units, which are involved in a fight.
        /// </summary>
        /// <param name="red">The <c>Unit</c> of the Red faction in the fight.</param>
        /// <param name="blue">The <c>Unit</c> of the Blue faction in the fight.</param>
        public void UpdateInspectedFight(Unit red, Unit blue)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i] != null && (m_InspectedObjects[i].GameObject == red.GameObject || m_InspectedObjects[i].GameObject == blue.GameObject));

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
                UpdateInspectedFight_ClientRpc(red.Strength, blue.Strength, GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex)));
        }

        /// <summary>
        /// Triggers the update of the strengths of the units in the inspected fight.
        /// </summary>
        /// <param name="redStrength">The strength of the Red unit in the fight.</param>
        /// <param name="blueStrength">The strength of the Blue unit in the fight.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedFight_ClientRpc(int redStrength, int blueStrength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFight(redStrength, blueStrength);

        #endregion

        #endregion
    }
}