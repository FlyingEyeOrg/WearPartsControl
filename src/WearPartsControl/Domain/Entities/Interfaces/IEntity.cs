namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IEntity
{
    Guid Id { get; set; }
}

public interface IEntity<TId>
    where TId : notnull
{
    TId Id { get; set; }
}
