using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>Structure</c> class represents any stationary object that is placed on the terrain.
    /// </summary>
    public class Structure : NetworkBehaviour
    {
        /// <summary>
        /// The method by which the structure can be destroyed.
        /// </summary>
        protected enum DestroyMethod
        {
            /// <summary>
            /// The structure cannot be destroyed.
            /// </summary>
            NONE,
            /// <summary>
            /// The structure is destroyed when the terrain under it changes.
            /// </summary>
            TERRAIN_CHANGE,
            /// <summary>
            /// The structure is destroyed when the terrain under it is lowered all the way to the water.
            /// </summary>
            DROWN
        }


        protected Team m_Team;
        /// <summary>
        /// Gets the team the structure belongs to.
        /// </summary>
        public virtual Team Team { get => m_Team; set => m_Team = value; }

        /// <summary>
        /// The method by which the structure can be destroyed.
        /// </summary>
        protected DestroyMethod m_DestroyMethod;

        protected TerrainPoint m_OccupiedTile;
        /// <summary>
        /// Gets the index of the tile on the terrain grid that the structure occupies.
        /// </summary>
        public TerrainPoint OccupiedTile { get => m_OccupiedTile; set { m_OccupiedTile = value; } }

        protected Dictionary<TerrainPoint, int> m_OccupiedPointHeights = new();
        /// <summary>
        /// Gets and sets the points the structure occupies and their heights at the time of the structure's creation.
        /// </summary>
        public Dictionary<TerrainPoint, int> OccupiedPointHeights { get => m_OccupiedPointHeights; set { m_OccupiedPointHeights = value; } }


        /// <summary>
        /// Cleans up references to other objects before the destruction of the structure.
        /// </summary>
        public virtual void Cleanup() {}


        #region Terrain Change

        /// <summary>
        /// Handles the settlement's response to a change in the height of the terrain under it.
        /// </summary>
        public virtual void ReactToTerrainChange()
        {
            (int lowestX, int lowestZ, int highestX, int highestZ) = Terrain.Instance.GetAffectedTileRange();

            if (m_OccupiedTile.GridX < lowestX || m_OccupiedTile.GridZ < lowestZ ||
                m_OccupiedTile.GridX > highestX || m_OccupiedTile.GridZ > highestZ)
                return;

            if (ShouldDestroyStructure())
            {
                StructureManager.Instance.DespawnStructure(gameObject);
                return;
            }

            // since it wasn't destroyed, it should be moved
            if (m_DestroyMethod == DestroyMethod.DROWN)
            {
                int height = Terrain.Instance.GetTileCenterHeight((m_OccupiedTile.GridX, m_OccupiedTile.GridZ));

                var corners = m_OccupiedPointHeights.Keys.ToArray();
                foreach (TerrainPoint point in corners)
                    m_OccupiedPointHeights[point] = height;

                SetHeight_ClientRpc/*ClientRpc*/(height);
            }
        }

        /// <summary>
        /// Decides whether the structure should be destroyed or lowered when the terrain under it has changed height.
        /// </summary>
        /// <returns>True if the structure should be destroyed, false otherwise.</returns>
        protected bool ShouldDestroyStructure()
        {
            if (m_DestroyMethod == DestroyMethod.NONE)
                return false;

            if (m_DestroyMethod == DestroyMethod.DROWN)
                return Terrain.Instance.IsTileUnderwater((m_OccupiedTile.GridX, m_OccupiedTile.GridZ));

            foreach ((TerrainPoint point, int height) in m_OccupiedPointHeights)
                if (point.Y != height)
                    return true;

            return false;
        }

        /// <summary>
        /// Sets the height the structure is sitting at to the given value.
        /// </summary>
        /// <param name="height">The value that the height the structure is sitting at should be set to.</param>
        [ClientRpc]
        protected void SetHeight_ClientRpc(float height)
            => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        #endregion
    }
}