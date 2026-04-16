namespace WearPartsControl.Domain.Entities.Interfaces;

public interface IHasAuditUser
{
    string? CreatedBy { get; set; }

    string? UpdatedBy { get; set; }
}
