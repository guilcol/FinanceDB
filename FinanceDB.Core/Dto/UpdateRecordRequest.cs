namespace FinanceDB.Core.Dto;

public record UpdateRecordRequest(
    string? Description,
    decimal? Amount
);
