using Steamworks;
using Steamworks.Data;
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    public enum Faction
    {
        RED,
        BLUE,
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
            m_SteamName = steamId != 0 ? new Friend(steamId).Name : "";
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
        /// <summary>
        /// Gets the name of the lobby.
        /// </summary>
        public string LobbyName { get => m_Lobby.GetData("name"); }
        /// <summary>
        /// Gets the password of the lobby.
        /// </summary>
        public string LobbyPassword { get => m_Lobby.GetData("password"); }


        private int m_GameSeed;
        private NetworkVariable<int> m_GameSeed_Network = new();
        public int GameSeed { get => m_GameSeed_Network.Value; }

        private NetworkList<PlayerInfo> m_PlayersInfo;

        private readonly ulong[] m_NetworkIdForFaction = new ulong[2];
        private readonly Faction[] m_FactionForNetworkId = new Faction[2];


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
            DontDestroyOnLoad(gameObject);

            m_PlayersInfo = new();
        }

        public void Setup(Lobby lobby, int gameSeed)
        {
            m_Lobby = lobby;
            m_GameSeed = gameSeed;
        }


        #region Player Info Getters

        public PlayerInfo? GetHostPlayerInfo() => m_PlayersInfo.Count == 0 ? null : m_PlayersInfo[0];

        public PlayerInfo? GetClientPlayerInfo() => m_PlayersInfo.Count <= 1 ? null : m_PlayersInfo[1];

        public PlayerInfo? GetPlayerInfoByFaction(Faction faction)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
                if (m_PlayersInfo[i].Faction == faction)
                    return m_PlayersInfo[i];

            return null;
        }


        public ulong GetNetworkIdByFaction(Faction team) => GetNetworkIdByFaction((int)team);
        public ulong GetNetworkIdByFaction(int factionIndex) => m_NetworkIdForFaction[factionIndex];
        public Faction GetFactionByNetworkId(ulong networkId) => m_FactionForNetworkId[networkId];

        #endregion


        #region Modify Player Info List

        [ServerRpc(RequireOwnership = false)]
        public void AddPlayerInfo_ServerRpc(ulong networkId, ulong steamId, Faction team)
            => AddPlayerInfo(new PlayerInfo(networkId, steamId, team));

        private void AddPlayerInfo(PlayerInfo playerInfo)
        {
            Debug.Log("Add Player Info: " + playerInfo.NetworkId);

            m_PlayersInfo.Add(playerInfo);
            m_NetworkIdForFaction[(int)playerInfo.Faction] = playerInfo.NetworkId;
            m_FactionForNetworkId[playerInfo.NetworkId] = playerInfo.Faction;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemovePlayerInfo_ServerRpc(ulong networkId)
            => RemovePlayerInfo(networkId);

        private bool RemovePlayerInfo(ulong networkId)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
            {
                if (m_PlayersInfo[i].NetworkId == networkId)
                {
                    m_PlayersInfo.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void SubscribeToPlayersInfoList(Action<NetworkListEvent<PlayerInfo>> method)
            => m_PlayersInfo.OnListChanged += new NetworkList<PlayerInfo>.OnListChangedDelegate(method);

        public void UnsubscribeFromPlayersInfoList(Action<NetworkListEvent<PlayerInfo>> method)
            => m_PlayersInfo.OnListChanged -= new NetworkList<PlayerInfo>.OnListChangedDelegate(method);

        #endregion
    }
}