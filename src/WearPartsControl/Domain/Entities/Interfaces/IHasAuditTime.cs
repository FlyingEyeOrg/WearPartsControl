using System;

namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IHasAuditTime
{
    DateTime? CreatedAt { get; set; }

    DateTime? UpdatedAt { get; set; }
}
