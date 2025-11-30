namespace CostVision.Interfaces.Entity
{
    public interface ICopyable<TEntity> where TEntity : class
    {
        public void CopyData(TEntity entity);
    }
}
