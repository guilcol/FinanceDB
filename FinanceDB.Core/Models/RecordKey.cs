namespace FinanceDB.Core.Models;

public sealed class RecordKey : IComparable<RecordKey>, IEquatable<RecordKey>
{
    public static bool operator ==(RecordKey? x, RecordKey? y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
            return false;

        return x.Equals(y);
    }

    public static bool operator !=(RecordKey? x, RecordKey? y)
    {
        return !(x == y);
    }

    public static bool operator >(RecordKey? x, RecordKey? y)
    {
        return Comparer<RecordKey>.Default.Compare(x, y) > 0;
    }

    public static bool operator <(RecordKey? x, RecordKey? y)
    {
        return Comparer<RecordKey>.Default.Compare(x, y) < 0;
    }

    public static bool operator >=(RecordKey? x, RecordKey? y)
    {
        return Comparer<RecordKey>.Default.Compare(x, y) >= 0;
    }

    public static bool operator <=(RecordKey? x, RecordKey? y)
    {
        return Comparer<RecordKey>.Default.Compare(x, y) <= 0;
    }

    public readonly string AccountId;
    public readonly DateTime Date;
    public readonly uint Sequence;

    public RecordKey(string accountId, DateTime date, uint sequence)
    {
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        Date = date;
        Sequence = sequence;
    }

    public int CompareTo(RecordKey? other)
    {
        if (other == null)
            return 1;

        int result = StringComparer.Ordinal.Compare(AccountId, other.AccountId);
        if (result != 0)
            return result;

        result = Date.CompareTo(other.Date);
        if (result != 0)
            return result;

        return Sequence.CompareTo(other.Sequence);
    }

    public string GetAccountId()
    {
        return AccountId;
    }

    public DateTime GetDate()
    {
        return Date;
    }

    public uint GetSequence()
    {
        return Sequence;
    }


    public bool Equals(RecordKey? other)
    {
        if (other == null)
            return false;
        return (AccountId == other.AccountId && Date == other.Date && Sequence == other.Sequence);
    }

    public override string ToString()
    {
        return $"{AccountId} {Date} {Sequence}";
    }

    public RecordKey WithSequence(uint sequence)
    {
        return new RecordKey(AccountId, Date, sequence);
    }
}
