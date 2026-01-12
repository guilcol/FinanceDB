namespace FinanceDB.Core.Models;

public class Record
{
    public RecordKey Key;
    private string _description;
    private decimal _amount;

    public Record(RecordKey key, string description, decimal amount)
    {
        Key = key;
        _description = description;
        _amount = amount;
    }

    public string Description
    {
        get { return _description; }
        set { _description = value; }
    }

    public decimal Amount
    {
        get { return _amount; }
        set { _amount = value; }
    }

    public RecordKey GetKey()
    {
        return Key;
    }

    public DateTime GetDate()
    {
        return Key.Date;
    }

    public string GetDescription()
    {
        return _description;
    }

    public decimal GetAmount()
    {
        return _amount;
    }
    public string Serialize()
    {
        return $"{Key} {_description} {_amount}";
    }

    public Record Copy()
    {
        return new Record(Key, _description, _amount);
    }
}

public sealed class ByKeyRecordComparer : IComparer<Record>
{
    public static readonly IComparer<Record> Instance = new ByKeyRecordComparer();

    private ByKeyRecordComparer()
    {
        // Prevents instantiation.
    }

    public int Compare(Record? x, Record? y)
    {
        return Comparer<RecordKey>.Default.Compare(x?.Key, y?.Key);
    }
}
