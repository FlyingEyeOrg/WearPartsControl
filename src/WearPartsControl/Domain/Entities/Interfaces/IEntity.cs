namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IEntity : IEntity<Guid>
{

}

public interface IEntity<TId>
    where TId : notnull
{
    TId Id { get; set; }
}
    