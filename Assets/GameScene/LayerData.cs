using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>LayerData</c> static class provides easy access to the identifiers of the physics layers used in the Gameplay Scene.
    /// </summary>
    public static class LayerData
    {
        /// <summary>
        /// The name of the layers containing the terrain chunks.
        /// </summary>
        public const string TERRAIN_LAYER_NAME = "Terrain";
        /// <summary>
        /// The name of the layer containing the units and settlements of the Red faction.
        /// </summary>
        public const string RED_FACTION_LAYER_NAME = "Red Team";
        /// <summary>
        /// The name of the layer containing the units and settlements of the Blue faction.
        /// </summary>
        public const string BLUE_FACTION_LAYER_NAME = "Blue Team";
        /// <summary>
        /// The name of the layer containing the units and structures that don't belong to any faction.
        /// </summary>
        public const string NONE_FACTION_LAYER_NAME = "None Team";

        /// <summary>
        /// The layer mask including the layer containing the terrain.
        /// </summary>
        public static int TerrainLayer = LayerMask.NameToLayer(TERRAIN_LAYER_NAME);
        /// <summary>
        /// The layer mask including the layer containing the units and settlements of the Red faction.
        /// </summary>
        public static int RedFactionLayer = LayerMask.NameToLayer(RED_FACTION_LAYER_NAME);
        /// <summary>
        /// The layer mask including the layer containing the units and settlements of the Blue faction.
        /// </summary>
        public static int BlueFactionLayer = LayerMask.NameToLayer(BLUE_FACTION_LAYER_NAME);
        /// <summary>
        /// The layer mask including the layer containing the units and structures that don't belong to any faction.
        /// </summary>
        public static int NoneFactionLayer = LayerMask.NameToLayer(NONE_FACTION_LAYER_NAME);

        /// <summary>
        /// An array containing the layer masks including each faction, where index 0 is the layer mask for the Red faction, 
        /// index 1 is the layer mask for the Blue faction, and index 2 is the layer mask for no faction.
        /// </summary>
        public static int[] FactionLayers = new int[] { RedFactionLayer, BlueFactionLayer, NoneFactionLayer };
    }
}