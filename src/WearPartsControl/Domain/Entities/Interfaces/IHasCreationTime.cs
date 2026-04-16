using System;

namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IHasCreationTime
{
    DateTime CreatedAt { get; set; }
}
