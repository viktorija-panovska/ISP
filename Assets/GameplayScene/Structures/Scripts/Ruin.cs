namespace Populous
{
    /// <summary>
    /// The <c>Ruin</c> class is a <c>Structure</c> that represents a ruined settlement.
    /// </summary>
    public class Ruin : Structure
    {
        private void Start()
        {
            GameUtils.ResizeGameObject(gameObject, Terrain.Instance.UnitsPerTileSide * 20);
            m_DestroyMethod = DestroyMethod.DROWN;
        }

    }
}