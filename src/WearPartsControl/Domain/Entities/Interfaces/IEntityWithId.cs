namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IEntityWithId<TId>
    where TId : notnull
{
    TId Id { get; set; }
}