namespace CostVision.Abstractions.Entity
{
    public interface IEntity<TId> where TId : notnull, IEquatable<TId>
    {
        TId Id { get; set; }
    }
}
