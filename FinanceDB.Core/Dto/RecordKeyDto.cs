using FinanceDB.Core.Models;

namespace FinanceDB.Core.Dto;

public record RecordKeyDto(
    string AccountId,
    DateTime Date,
    uint Sequence
)
{
    public static RecordKeyDto FromRecordKey(RecordKey key)
    {
        return new RecordKeyDto(key.AccountId, key.Date, key.Sequence);
    }

    public RecordKey ToRecordKey()
    {
        return new RecordKey(AccountId, Date, Sequence);
    }
}
