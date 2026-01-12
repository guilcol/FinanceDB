using FinanceDB.Core.Models;

namespace FinanceDB.Core.Dto;

public record RecordDto(
    RecordKeyDto Key,
    string Description,
    decimal Amount
)
{
    public static RecordDto FromRecord(Record record)
    {
        return new RecordDto(
            RecordKeyDto.FromRecordKey(record.Key),
            record.Description,
            record.Amount
        );
    }

    public Record ToRecord()
    {
        return new Record(Key.ToRecordKey(), Description, Amount);
    }
}
