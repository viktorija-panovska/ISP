namespace Populous
{
    public static class TagData
    {
        public const string SWORD_TAG = "Sword";

        public const string RED_FACTION_TAG = "RedTeam";
        public const string BLUE_FACTION_TAG = "BlueTeam";
        public const string NONE_FACTION_TAG = "NoneTeam";

        public const string RED_LEADER_TAG = "RedLeader";
        public const string BLUE_LEADER_TAG = "BlueLeader";

        public const string TENT_TAG = "Tent";
        public const string MUD_HUT_TAG = "MudHut";
        public const string STRAW_HUT_TAG = "StrawHut";
        public const string ROCK_HUT_TAG = "RockHut";
        public const string WOOD_HOUSE_TAG = "WoodHouse";
        public const string STONE_HOUSE_TAG = "StoneHouse";
        public const string NICE_HOUSE_TAG = "NiceHouse";
        public const string ROUND_TOWER_TAG = "RoundTower";
        public const string SQUARE_TOWER_TAG = "SquareTower";
        public const string CITY_TAG = "City";

        public static string[] FactionTags = { RED_FACTION_TAG, BLUE_FACTION_TAG, NONE_FACTION_TAG };
        public static string[] LeaderTags = { RED_LEADER_TAG, BLUE_LEADER_TAG };
        public static string[] SettlementTags = {
            TENT_TAG, MUD_HUT_TAG, STRAW_HUT_TAG, ROCK_HUT_TAG, WOOD_HOUSE_TAG, STONE_HOUSE_TAG, NICE_HOUSE_TAG, ROUND_TOWER_TAG, SQUARE_TOWER_TAG, CITY_TAG
        };
    }
}