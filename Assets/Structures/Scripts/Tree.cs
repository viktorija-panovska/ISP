namespace Populous
{
    /// <summary>
    /// The <c>Tree</c> class is a <c>Structure</c> representing one of the trees on the terrain.
    /// </summary>
    public class Tree : Structure 
    {
        private void Start() => m_DestroyMethod = DestroyMethod.DROWN;
    }
}