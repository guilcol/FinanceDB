using FinanceDB.Core.Models;

namespace FinanceDB.Core.Storage;

public class BTreeNodeReference : IComparable<BTreeNodeReference>, IEquatable<BTreeNodeReference>
{
    public readonly RecordKey FirstKey;
    public readonly RecordKey LastKey;
    public readonly long ChildId;
    public decimal Amount;

    public BTreeNodeReference(RecordKey firstKey, RecordKey lastKey, long childId, Decimal amount)
    {
        FirstKey = firstKey;
        LastKey = lastKey;
        ChildId = childId;
        Amount = amount;
    }

    public static bool operator ==(BTreeNodeReference? x, BTreeNodeReference? y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
            return false;

        return x.Equals(y);
    }

    public static bool operator !=(BTreeNodeReference? x, BTreeNodeReference? y)
    {
        return !(x == y);
    }

    public int CompareTo(BTreeNodeReference? other)
    {
        if (other == null)
            return 1;

        int result = FirstKey.CompareTo(other.FirstKey);
        if (result != 0)
            return result;

        result = LastKey.CompareTo(other.LastKey);
        if (result != 0)
            return result;

        if (ChildId < other.ChildId)
            return -1;

        if (ChildId > other.ChildId)
            return 1;

        return 0;
    }

    public bool Equals(BTreeNodeReference? other)
    {
        {
            if (other == null)
                return false;
            return (FirstKey == other.FirstKey && LastKey == other.LastKey && ChildId == other.ChildId);
        }
    }

    public decimal GetAmount()
    {
        return Amount;
    }
}
