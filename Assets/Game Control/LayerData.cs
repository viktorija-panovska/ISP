using UnityEngine;

namespace Populous
{
    public static class LayerData
    {
        public const string TERRAIN_LAYER_NAME = "Terrain";
        public const string RED_TEAM_LAYER_NAME = "Red Team"; 
        public const string BLUE_TEAM_LAYER_NAME = "Blue Team";
        public const string NONE_TEAM_LAYER_NAME = "None Team";

        public static int TerrainLayer = LayerMask.NameToLayer(TERRAIN_LAYER_NAME);
        public static int RedTeamLayer = LayerMask.NameToLayer(RED_TEAM_LAYER_NAME);
        public static int BlueTeamLayer = LayerMask.NameToLayer(BLUE_TEAM_LAYER_NAME);
        public static int NoneTeamLayer = LayerMask.NameToLayer(NONE_TEAM_LAYER_NAME);

        public static int[] TeamLayers = new int[] { RedTeamLayer, BlueTeamLayer, NoneTeamLayer };
    }
}