namespace FinanceDB.Core.Dto;

public record ErrorResponse(
    string Error,
    string? Detail = null
);
