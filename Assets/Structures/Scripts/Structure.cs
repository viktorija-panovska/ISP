using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    public class Structure : NetworkBehaviour
    {
        protected enum DestroyState
        {
            NONE,
            LOWER,
            WATER
        }


        [SerializeField] protected DestroyState m_DestroyState;

        protected Dictionary<MapPoint, int> m_OccupiedPointHeights = new();
        /// <summary>
        /// Gets and sets the occupied points and their heights at the time of the structure's creation.
        /// </summary>
        public Dictionary<MapPoint, int> OccupiedPointHeights { get => m_OccupiedPointHeights; set { m_OccupiedPointHeights = value; } }

        protected (int x, int z) m_OccupiedTile;
        /// <summary>
        /// Gets the index of the tile on the grid that the structure occupies.
        /// </summary>
        public (int x, int z) OccupiedTile { get => m_OccupiedTile; set { m_OccupiedTile = value; } }

        protected Team m_Team;
        public Team Team { get => m_Team; set => m_Team = value; }


        public virtual void Cleanup()
        {}

        /// <summary>
        /// Change the height or destroy the structure depending on the change of the height of the terrain beneath it.
        /// </summary>
        public virtual void ReactToTerrainChange()
        {
            if (ShouldDestroyStructure())
                StructureManager.Instance.DespawnStructure(gameObject);
        }

        protected bool ShouldDestroyStructure()
        {
            bool isTileUnderwater = true;
            bool hasHeightChanged = false;

            foreach ((MapPoint point, int height) in m_OccupiedPointHeights)
            {
                if (m_DestroyState == DestroyState.WATER && point.Y > Terrain.Instance.WaterLevel)
                    isTileUnderwater = false;

                if (point.Y != height || point.Y <= Terrain.Instance.WaterLevel)
                {
                    if (m_DestroyState == DestroyState.LOWER)
                        return true;
                    else
                        hasHeightChanged = true;
                }
            }

            if (m_DestroyState == DestroyState.LOWER)
                return false;

            if (m_DestroyState == DestroyState.WATER && isTileUnderwater)
                return true;

            if (hasHeightChanged)
                SetHeightClientRpc(GetType() == typeof(Flag) ? Terrain.Instance.GetPointHeight(m_OccupiedTile) : Terrain.Instance.GetTileCenterHeight(m_OccupiedTile));

            return false;
        }


        [ClientRpc]
        private void SetHeightClientRpc(float height) => transform.position = new Vector3(transform.position.x, height, transform.position.z);

    }
}