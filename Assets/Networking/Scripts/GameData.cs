using Steamworks;
using Steamworks.Data;
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The names of the Factions in the game.
    /// </summary>
    public enum Faction
    {
        /// <summary>
        /// The Red faction.
        /// </summary>
        RED,
        /// <summary>
        /// The Blue faction.
        /// </summary>
        BLUE,
        /// <summary>
        /// An empty faction, used as a placeholder.
        /// </summary>
        NONE
    }

    /// <summary>
    /// The <c>PlayerInfo</c> struct stores the data of a player in the game.
    /// </summary>
    public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
    {
        private ulong m_NetworkId;
        /// <summary>
        /// Gets the network ID of this player.
        /// </summary>
        public readonly ulong NetworkId { get => m_NetworkId; }

        private ulong m_SteamId;
        /// <summary>
        /// Gets teh Steam ID of this player.
        /// </summary>
        public readonly ulong SteamId { get => m_SteamId; }

        private FixedString64Bytes m_SteamName;
        /// <summary>
        /// Gets this player's Steam display name.
        /// </summary>
        public readonly string SteamName { get => m_SteamName.ToString(); }

        private Faction m_Faction;
        /// <summary>
        /// Gets the faction this player is in control of in the game.
        /// </summary>
        public readonly Faction Faction { get => m_Faction; }


        /// <summary>
        /// The constructor of the <c>PlayerInfo</c> struct
        /// </summary>
        /// <param name="networkId">The network ID of the player.</param>
        /// <param name="steamId">The Steam ID of the player.</param>
        /// <param name="faction">The faction that the player will control in the game.</param>
        public PlayerInfo(ulong networkId, ulong steamId, Faction faction)
        {
            m_NetworkId = networkId;
            m_SteamId = steamId;
            m_SteamName = m_SteamId != 0 ? new Friend(m_SteamId).Name : "";
            m_Faction = faction;
        }

        /// <summary>
        /// Tests whether this <c>PlayerInfo</c> struct is equal to the given <c>PlayerInfo</c> struct.
        /// </summary>
        /// <param name="other">The <c>PlayerInfo</c> struct that this struct should be compared against.</param>
        /// <returns>True if the structs are equal, false otherwise.</returns>
        public readonly bool Equals(PlayerInfo other)
            => m_NetworkId == other.NetworkId && m_SteamId == other.SteamId;

        /// <summary>
        /// Serializes the data in this struct so that they can be transferred over the network.
        /// </summary>
        /// <typeparam name="T">The type of the serializer.</typeparam>
        /// <param name="serializer">The serializer that should be used in the serialization.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_NetworkId);
            serializer.SerializeValue(ref m_SteamId);
            serializer.SerializeValue(ref m_SteamName);
            serializer.SerializeValue(ref m_Faction);
        }
    }


    /// <summary>
    /// The <c>GameData</c> class stores the crucial data of the game, namely the information about the lobby it is running out of,
    /// and the players in the game.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameData : NetworkBehaviour
    {
        private static GameData m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static GameData Instance { get => m_Instance; }

        /// <summary>
        /// The lobby this game is running out of.
        /// </summary>
        private Lobby m_Lobby;

        private readonly NetworkVariable<FixedString64Bytes> m_LobbyName = new();
        /// <summary>
        /// Gets the name of the lobby.
        /// </summary>
        public string LobbyName { get => m_LobbyName.Value.ToString(); }

        private readonly NetworkVariable<int> m_GameSeed = new();
        /// <summary>
        /// Gets the seed for generating the terrain and other game elements.
        /// </summary>
        public int GameSeed { get => m_GameSeed.Value; }

        /// <summary>
        /// A list of the <c>PlayerInfo</c> instances for all the connected players.
        /// </summary>
        private NetworkList<PlayerInfo> m_PlayersInfo;

        /// <summary>
        /// The index of the host's info in the player info list.
        /// </summary>
        /// <remarks>As the host will always be the first to have their player info added to the list, their player info
        /// will be at index 0.</remarks>
        private const int HOST_PLAYER_INDEX = 0;
        /// <summary>
        /// The index of the client's info in the player info list.
        /// </summary>
        /// <remarks>As the host will always be the first to have their player info added to the list, the index of the 
        /// client info in the list is 1.</remarks>
        private const int CLIENT_PLAYER_INDEX = 1;

        /// <summary>
        /// An array of the network IDs of the players controlling each faction.
        /// </summary>
        /// <remarks>Index 0 is the ID for the player controlling the Red faction, and index 1 for the blue faction.</remarks>
        private readonly ulong[] m_NetworkIdForFaction = new ulong[2] { ulong.MaxValue, ulong.MaxValue };


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
                Destroy(m_Instance.gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);

            m_PlayersInfo = new();
        }


        /// <summary>
        /// Sets the lobby and the seed for the game.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> that the game will be ran out of.</param>
        /// <param name="gameSeed">The seed that will be used to generate the terrain and other game elements.</param>
        public void Setup(Lobby lobby, int gameSeed)
        {
            m_Lobby = lobby;
            m_LobbyName.Value = m_Lobby.GetData(ConnectionManager.LOBBY_NAME_KEY);
            m_GameSeed.Value = gameSeed;
        }


        #region Player Info List

        /// <summary>
        /// Calls the server to add a player with the given data to the player list.
        /// </summary>
        /// <param name="networkId">The player's network ID.</param>
        /// <param name="steamId">The player's Steam ID.</param>
        /// <param name="faction">The faction the player controls.</param>
        [ServerRpc(RequireOwnership = false)]
        public void AddPlayerInfo_ServerRpc(ulong networkId, ulong steamId, Faction faction)
            => AddPlayerInfo(new(networkId, steamId, faction));

        /// <summary>
        /// Adds a player with the given data to the player info list.
        /// </summary>
        /// <param name="playerInfo">The <c>PlayerInfo</c> of the added player.</param>
        private void AddPlayerInfo(PlayerInfo playerInfo) 
        { 
            m_PlayersInfo.Add(playerInfo);
            m_NetworkIdForFaction[(int)playerInfo.Faction] = playerInfo.NetworkId;
        }

        /// <summary>
        /// Removes the player info of the client.
        /// </summary>
        public void RemoveClientInfo()
        {
            if (m_PlayersInfo.Count <= CLIENT_PLAYER_INDEX) return;
            m_NetworkIdForFaction[CLIENT_PLAYER_INDEX] = ulong.MaxValue;
            m_PlayersInfo.RemoveAt(CLIENT_PLAYER_INDEX);
        }

        /// <summary>
        /// Subscribes the given method to the <c>OnListChanged</c> event of the player info list.
        /// </summary>
        /// <param name="method">A <c>OnLIstChangedDelegate</c> of the method that is to be subscribed.</param>
        public void SubscribeToPlayersInfoList(NetworkList<PlayerInfo>.OnListChangedDelegate method)
            => m_PlayersInfo.OnListChanged += method;

        /// <summary>
        /// Unsubscribes the given method from the <c>OnListChanged</c> event of the player info list.
        /// </summary>
        /// <param name="method">A <c>OnLIstChangedDelegate</c> of the method that is to be unsubscribed.</param>
        public void UnsubscribeFromPlayersInfoList(NetworkList<PlayerInfo>.OnListChangedDelegate method)
            => m_PlayersInfo.OnListChanged -= method;

        #endregion


        #region Player Info Getters

        /// <summary>
        /// Gets the player info of the host, if it exists.
        /// </summary>
        /// <returns>The <c>PlayerInfo</c> of the host, null if it doesn't exist.</returns>
        public PlayerInfo? GetHostPlayerInfo() 
            => m_PlayersInfo.Count == HOST_PLAYER_INDEX ? null : m_PlayersInfo[HOST_PLAYER_INDEX];

        /// <summary>
        /// Gets the player info of the client, if it exists.
        /// </summary>
        /// <returns>The <c>PlayerInfo</c> of the client, null if it doesn't exist.</returns>
        public PlayerInfo? GetClientPlayerInfo()
            => m_PlayersInfo.Count <= CLIENT_PLAYER_INDEX ? null : m_PlayersInfo[CLIENT_PLAYER_INDEX];

        /// <summary>
        /// Gets the player info of the player controlling the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player.</param>
        /// <returns>The <c>PlayerInfo</c> of the player, null if it doesn't exist.</returns>
        public PlayerInfo? GetPlayerInfoByFaction(Faction faction)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
                if (m_PlayersInfo[i].Faction == faction)
                    return m_PlayersInfo[i];

            return null;
        }

        /// <summary>
        /// Returns the network ID of the player controlling the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player.</param>
        /// <returns>The network ID of the player.</returns>
        public ulong GetNetworkIdByFaction(Faction faction) => GetNetworkIdByFaction((int)faction);

        /// <summary>
        /// Returns the network ID of the player controlling the given faction.
        /// </summary>
        /// <param name="factionIndex">The "faction index" corresponds to the value of the faction in the <c>Faction</c> enum:
        /// 0 for the Red faction and 1 for the Blue faction.</param>
        /// <returns>The network ID of the player.</returns>
        public ulong GetNetworkIdByFaction(int factionIndex) => m_NetworkIdForFaction[factionIndex];

        #endregion
    }
}