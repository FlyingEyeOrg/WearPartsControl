using WearPartsControl.Domain.Entities.Interfaces;

namespace WearPartsControl.Domain.Entities;

public abstract class Entity : IEntity, IEntity<Guid>
{
    public Guid Id { get; set; }
}
