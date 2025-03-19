using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Field</c> class is a <c>Structure</c> that represents one tile of fields that exists on the terrain.
    /// </summary>
    public class Field : Structure
    {
        [Tooltip("The GameObjects corresponding to each faction's field, where index 0 is the red faction, index 1 is the blue faction, and index 2 is no faction.")]
        [SerializeField] private GameObject[] m_Fields;

        private HashSet<Settlement> m_SettlementsServed = new();
        /// <summary>
        /// Gets a list of all the settlements that this field belongs to.
        /// </summary>
        public HashSet<Settlement> SettlementsServed { get => m_SettlementsServed; }


        private void Start()
        {
            m_DestroyMethod = DestroyMethod.TERRAIN_CHANGE;

            foreach (Renderer child in GetComponentsInChildren<Renderer>())
                GameUtils.ResizeGameObject(child.gameObject, Terrain.Instance.UnitsPerTileSide);
        }


        #region Settlement Override

        /// <inheritdoc />
        public override void Setup(Faction faction, TerrainTile occupiedTile)
        {
            base.Setup(faction, occupiedTile);
            ToggleField_ClientRpc(m_Faction, true);
        }

        /// <inheritdoc />
        public override void Cleanup()
        {
            base.Cleanup();

            foreach (Settlement settlement in m_SettlementsServed)
            {
                if (settlement.OnSettlementDestroyed != null)
                    settlement.OnSettlementDestroyed -= RemoveSettlementServed;

                if (settlement.OnSettlementFactionChanged != null)
                    settlement.OnSettlementFactionChanged -= SwitchFaction;
            }

            m_SettlementsServed = new();
        }

        #endregion


        #region Field Changes

        /// <summary>
        /// Changes the faction this field belongs to.
        /// </summary>
        /// <param name="faction">The new <c>Faction</c> this field should belong to.</param>
        public void SwitchFaction(Faction faction)
        {
            if (faction == m_Faction) return;

            ToggleField_ClientRpc(m_Faction, false);
            m_Faction = faction;
            ToggleField_ClientRpc(m_Faction, true);

            if (m_Faction == Faction.NONE)
                Cleanup();
        }

        /// <summary>
        /// Activates or deactivates the field object of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose field should be activated.</param>
        /// <param name="isOn">True if the field should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleField_ClientRpc(Faction faction, bool isOn)
        {
            GameObject field = GameUtils.GetChildWithTag(gameObject, TagData.FactionTags[(int)faction]);
            if (!field) return;
            field.SetActive(isOn);
        }

        #endregion


        #region Settlements Served

        /// <summary>
        /// Adds the given settlement to the list of settlements this field belongs to.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be added.</param>
        public void AddSettlementServed(Settlement settlement) 
        { 
            m_SettlementsServed.Add(settlement);
            settlement.OnSettlementDestroyed += RemoveSettlementServed;
            settlement.OnSettlementFactionChanged += SwitchFaction;
        }

        /// <summary>
        /// Removes the given settlement from the list of settlements this field belongs 
        /// to and removes the field if there are no more settlements that own it.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be removed.</param>
        public void RemoveSettlementServed(Settlement settlement)
        {
            m_SettlementsServed.Remove(settlement);
            settlement.OnSettlementDestroyed -= RemoveSettlementServed;
            settlement.OnSettlementFactionChanged -= SwitchFaction;

            if (m_SettlementsServed.Count == 0)
                StructureManager.Instance.DespawnStructure(gameObject);
        }

        #endregion
    }
}