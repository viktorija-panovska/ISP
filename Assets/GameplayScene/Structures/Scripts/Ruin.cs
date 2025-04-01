namespace Populous
{
    /// <summary>
    /// The <c>Ruin</c> class is a <c>Structure</c> that represents a ruined settlement.
    /// </summary>
    public class Ruin : Structure
    {
        private void Start() => m_DestroyMethod = DestroyMethod.DROWN;

        public override void Setup(Faction faction, TerrainTile occupiedTile)
        {
            base.Setup(faction, occupiedTile);
            GameUtils.ResizeGameObject(gameObject, Terrain.Instance.UnitsPerTileSide);
        }
    }
}