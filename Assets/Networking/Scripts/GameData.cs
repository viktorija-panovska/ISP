using Steamworks;
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    public enum Team
    {
        RED,
        BLUE,
        NONE
    }

    public struct LobbyInfo : INetworkSerializable, IEquatable<LobbyInfo>
    {
        private FixedString32Bytes m_LobbyName;
        public readonly string LobbyName { get => m_LobbyName.ToString(); }

        private FixedString32Bytes m_LobbyPassword;
        public readonly string LobbyPassword { get => m_LobbyPassword.ToString(); }

        public LobbyInfo(string lobbyName, string lobbyPassword)
        {
            m_LobbyName = lobbyName;
            m_LobbyPassword = lobbyPassword;
        }

        public readonly bool Equals(LobbyInfo other)
            => m_LobbyName == other.LobbyName && m_LobbyPassword == other.LobbyPassword;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_LobbyName);
            serializer.SerializeValue(ref m_LobbyPassword);
        }
    }


    public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
    {
        private ulong m_NetworkId;
        public readonly ulong NetworkId { get => m_NetworkId; }

        private ulong m_SteamId;
        public readonly ulong SteamId { get => m_SteamId; }

        private FixedString64Bytes m_SteamName;
        public readonly string SteamName { get => m_SteamName.ToString(); }

        private Team m_Team;
        public readonly Team Team { get => m_Team; }


        public PlayerInfo(ulong networkId, ulong steamId, Team team)
        {
            m_NetworkId = networkId;
            m_SteamId = steamId;
            m_SteamName = new Friend(steamId).Name;
            m_Team = team;
        }

        public readonly bool Equals(PlayerInfo other)
            => m_NetworkId == other.NetworkId && m_SteamId == other.SteamId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_NetworkId);
            serializer.SerializeValue(ref m_SteamId);
            serializer.SerializeValue(ref m_SteamName);
            serializer.SerializeValue(ref m_Team);
        }
    }


    [RequireComponent(typeof(NetworkObject))]
    public class GameData : NetworkBehaviour
    {
        private static GameData m_Instance;
        public static GameData Instance { get => m_Instance; }

        private NetworkVariable<LobbyInfo> m_CurrentLobbyInfo;
        public LobbyInfo CurrentLobbyInfo { get => m_CurrentLobbyInfo.Value; set { m_CurrentLobbyInfo.Value = value; } }

        private NetworkVariable<int> m_MapSeed;
        public int MapSeed { get => m_MapSeed.Value; set { m_MapSeed.Value = value; } }

        private NetworkList<PlayerInfo> m_PlayersInfo;

        private readonly ulong[] m_NetworkIdForTeam = new ulong[ConnectionManager.MAX_PLAYERS];
        private readonly Team[] m_TeamForNetworkId = new Team[ConnectionManager.MAX_PLAYERS];


        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetData();
        }


        #region Player Info Getters

        public int GetPlayersNumber() => m_PlayersInfo.Count;

        public PlayerInfo? GetPlayerInfoByIndex(int index)
            => index > m_PlayersInfo.Count ? null : m_PlayersInfo[index];

        public PlayerInfo? GetPlayerInfoByNetworkId(ulong networkId)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
                if (m_PlayersInfo[i].NetworkId == networkId)
                    return m_PlayersInfo[i];

            return null;
        }

        public PlayerInfo? GetPlayerInfoBySteamId(ulong steamId)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
                if (m_PlayersInfo[i].SteamId == steamId)
                    return m_PlayersInfo[i];

            return null;
        }

        public PlayerInfo? GetPlayerInfoByTeam(Team team)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
                if (m_PlayersInfo[i].Team == team)
                    return m_PlayersInfo[i];

            return null;
        }

        public ulong GetNetworkIdByTeam(Team team) => GetNetworkIdByTeam((int)team);
        public ulong GetNetworkIdByTeam(int team) => m_NetworkIdForTeam[team];

        public Team GetTeamByNetworkId(ulong networkId) => m_TeamForNetworkId[networkId];

        #endregion


        #region Modify Player Info List

        [ServerRpc(RequireOwnership = false)]
        public void AddPlayerInfoServerRpc(ulong networkId, ulong steamId, Team team)
            => AddPlayerInfo(new PlayerInfo(networkId, steamId, team));

        public bool AddCurrentPlayerInfo(Team team)
            => AddPlayerInfo(new PlayerInfo(NetworkManager.Singleton.LocalClientId, SteamClient.SteamId, team));

        public bool AddPlayerInfo(PlayerInfo playerInfo)
        {
            if (m_PlayersInfo.Count == ConnectionManager.MAX_PLAYERS)
                return false;

            m_PlayersInfo.Add(playerInfo);
            m_NetworkIdForTeam[(int)playerInfo.Team] = playerInfo.NetworkId;
            m_TeamForNetworkId[playerInfo.NetworkId] = playerInfo.Team;
            return true;
        }


        public bool RemovePlayerInfoByNetworkId(ulong networkId)
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

        public bool RemovePlayerInfoBySteamId(ulong steamId)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
            {
                if (m_PlayersInfo[i].SteamId == steamId)
                {
                    m_PlayersInfo.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool RemovePlayerInfoByTeam(Team team)
        {
            for (int i = 0; i < m_PlayersInfo.Count; ++i)
            {
                if (m_PlayersInfo[i].Team == team)
                {
                    m_PlayersInfo.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        #endregion


        public void SubscribeToPlayersInfoList(Action<NetworkListEvent<PlayerInfo>> method)
        {
            m_PlayersInfo.OnListChanged += new NetworkList<PlayerInfo>.OnListChangedDelegate(method);
        }

        public void UnsubscribeFromPlayersInfoList(Action<NetworkListEvent<PlayerInfo>> method)
        {
            m_PlayersInfo.OnListChanged -= new NetworkList<PlayerInfo>.OnListChangedDelegate(method);
        }

        public void ResetData()
        {
            m_CurrentLobbyInfo = new();
            m_MapSeed = new();
            m_PlayersInfo = new();
        }
    }
}