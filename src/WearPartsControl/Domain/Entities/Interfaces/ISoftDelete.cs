using System;

namespace WearPartsControl.Domain.Entities.Interfaces;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }
}
