using System;

namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IHasModificationTime
{
    DateTime UpdatedAt { get; set; }
}
