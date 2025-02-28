using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>Structure</c> class represents any stationary object that exists on the terrain.
    /// </summary>
    public class Structure : NetworkBehaviour
    {
        /// <summary>
        /// The method by which the structure can be destroyed.
        /// </summary>
        protected enum DestroyMethod
        {
            /// <summary>
            /// The structure is destroyed when the terrain under it changes.
            /// </summary>
            TERRAIN_CHANGE,
            /// <summary>
            /// The structure is destroyed when the terrain under it is lowered all the way to the water.
            /// </summary>
            DROWN
        }


        protected Faction m_Faction;
        /// <summary>
        /// Gets the faction the structure belongs to.
        /// </summary>
        public virtual Faction Faction { get => m_Faction; }

        protected TerrainTile m_OccupiedTile;
        /// <summary>
        /// Gets the terrain tile that the structure sits on.
        /// </summary>
        public TerrainTile OccupiedTile { get => m_OccupiedTile; }

        /// <summary>
        /// The points at the corners of the occupied tile and their heights at the time of the structure's creation.
        /// </summary>
        /// <remarks>Only relevant for structures that are destroyed by any change in the terrain.</remarks>
        protected Dictionary<TerrainPoint, int> m_OccupiedTileCornerHeights;

        /// <summary>
        /// The method by which the structure can be destroyed.
        /// </summary>
        protected DestroyMethod m_DestroyMethod;


        /// <summary>
        /// Initializes the structure.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the structure belongs to.</param>
        /// <param name="occupiedTile">The <c>TerrainTile</c> the structure occupies.</param>
        public virtual void Setup(Faction faction, TerrainTile occupiedTile)
        {
            m_Faction = faction;
            m_OccupiedTile = occupiedTile;
            GameController.Instance.OnFlood += ReactToTerrainChange;

            if (m_DestroyMethod == DestroyMethod.TERRAIN_CHANGE)
            {
                m_OccupiedTileCornerHeights = new();
                foreach (TerrainPoint corner in m_OccupiedTile.GetCorners())
                    m_OccupiedTileCornerHeights.Add(corner, corner.GetHeight());
            }
        }

        /// <summary>
        /// Cleans up references to other objects before the destruction of the structure.
        /// </summary>
        public virtual void Cleanup() 
        {
            if (GameController.Instance.OnFlood != null)
                GameController.Instance.OnFlood -= ReactToTerrainChange;
        }


        #region Terrain Change

        /// <summary>
        /// Handles the settlement's response to a change in the height of the terrain under it.
        /// </summary>
        public virtual void ReactToTerrainChange()
        {
            // either the structure is underwater or the heights of the corners have been changed
            if (ShouldDestroyStructure())
            {
                StructureManager.Instance.DespawnStructure(gameObject);
                return;
            }

            // for terrain change, if the structure wasn't destroyed, there has been no change
            // in the corners of the tile and the structure doesn't need to be moved.
            // for drown, if the structure wasn't destroyed, there could have been a change
            if (m_DestroyMethod == DestroyMethod.DROWN)
                SetHeight/*_ClientRpc*/((int)m_OccupiedTile.GetCenterHeight());
        }

        /// <summary>
        /// Decides whether the structure should be destroyed or not when the terrain under it has changed height.
        /// </summary>
        /// <returns>True if the structure should be destroyed, false otherwise.</returns>
        protected bool ShouldDestroyStructure()
        {
            if (m_DestroyMethod == DestroyMethod.DROWN)
                return m_OccupiedTile.IsUnderwater();

            if (m_DestroyMethod == DestroyMethod.TERRAIN_CHANGE)
            {
                // check if the tile corners have been changed
                foreach ((TerrainPoint point, int height) in m_OccupiedTileCornerHeights)
                    if (point.GetHeight() != height)
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the height the structure is sitting at to the given value.
        /// </summary>
        /// <param name="height">The value that the height the structure is sitting at should be set to.</param>
        //[ClientRpc]
        protected void SetHeight/*_ClientRpc*/(float height)
            => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        #endregion
    }
}