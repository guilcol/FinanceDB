namespace FinanceDB.Core.Dto;

public record InsertRecordRequest(
    DateTime? Date,
    uint? Sequence,
    string Description,
    decimal Amount
);
