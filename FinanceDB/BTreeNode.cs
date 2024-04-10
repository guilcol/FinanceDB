using System.Runtime.InteropServices.JavaScript;

namespace FinanceDB;

public sealed class BTreeNode
{
    public long Id { get; }
    public bool IsLeaf { get; }
    public Record[]? Records { get; }
    public List<BTreeNodeReference>? ChildrenRef { get; }
    
    public decimal Amount;

    public BTreeNode(long id, bool isLeaf, Record[]? records, List<BTreeNodeReference>? childrenRef, decimal amount)
    {
        Id = id;
        IsLeaf = isLeaf;
        Records = records;
        ChildrenRef = childrenRef;
        Amount = amount;
    }

    public bool isFull()
    {
        if (Records == null)
            return ChildrenRef.Count < BTreeRs.degree;
        return Records.Length < BTreeRs.degree; //TODO: Fix isFull() return true for any value under DEGREE
    }
    public bool isOverflowing()
    {
        if (Records == null)
            return ChildrenRef.Count > BTreeRs.degree;
        return Records.Length > BTreeRs.degree;
    }


    public BTreeNode WithNewRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = new Record[Records.Length + 1];
        Array.Copy(Records, 0, records, 0, index);
        records[index] = record;
        Array.Copy(Records, index, records, index + 1, Records.Length - index);

        return new BTreeNode(Id, true, records, null, Amount + record.GetAmount());
    }

    public BTreeNode WithDeletedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = new Record[Records.Length - 1];
        Array.Copy(Records, 0, records, 0, index);
        Array.Copy(Records, index + 1, records, index, Records.Length - (index + 1));

        return new BTreeNode(Id, true, records, null, Amount);
    }

    public BTreeNode WithUpdatedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = (Record[])Records.Clone();
        records[index] = record;

        return new BTreeNode(Id, true, records, null, Amount);
    }

    public BTreeNode WithModifiedReference(int childIndex, BTreeNodeReference newChildRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on internal nodes. Node {Id} is a leaf.");

        List<BTreeNodeReference> childrenRef = new List<BTreeNodeReference>(ChildrenRef);
        childrenRef[childIndex] = newChildRef;

        return new BTreeNode(Id, false, null, childrenRef, Amount);
    }

    public int GetIndexFromKey(RecordKey recordKey)
    {
        Record dummyRecord = new Record(recordKey, "", 0);
        int index = Array.BinarySearch((Record[])Records, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    public int FindChildReference(RecordKey key)
    {
        int left = 0;
        int right = ChildrenRef.Count - 1;

        while (left <= right)
        {
            int middle = (left + right) / 2;

            switch (KeyInBounds(key, ChildrenRef[middle]))
            {
                case 0:
                    return middle;
                case 1:
                    left = middle + 1;
                    break;
                case -1:
                    right = middle - 1;
                    break;
            }
        }

        return ~left;
    }

    private int KeyInBounds(RecordKey key, BTreeNodeReference childRef)
    {
        if (key >= childRef.FirstKey && key <= childRef.LastKey)
            return 0;
        if (key >= childRef.FirstKey)
            return 1;
        return -1;
    }

    public (BTreeNodeReference, int) SelectChildReference(int index, Random rand)
    {
        if (index == 0)
            return (ChildrenRef[0], 0);
        if (index == ChildrenRef.Count)
            return (ChildrenRef[index - 1], index - 1);

        if (rand.Next(2) == 0)
            return (ChildrenRef[index], index);
        return (ChildrenRef[index - 1], index - 1);
    }

    public BTreeNodeReference ClosestReferenceInNode(BTreeNodeReference targetRef)
    {
        RecordKey dummyKey = targetRef.FirstKey;

        int index = FindChildReference(dummyKey);

        if (index < 0)
            throw new Exception("What the fuck?");

        return ChildrenRef[index];
    }

    public BTreeNodeReference GetSelfReference()
    {
        if (IsLeaf)
            return new BTreeNodeReference(Records?[0].Key, Records?[Records.Length - 1].Key, Id, Amount);
        return new BTreeNodeReference(ChildrenRef?[0].FirstKey, ChildrenRef?[ChildrenRef.Count - 1].LastKey, Id, Amount);
    }

    public BTreeNode WithSplitReference(BTreeNodeReference oldRef, BTreeNodeReference leftRef, BTreeNodeReference rightRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException(
                $"Operation is only allowed on internal node. Node {Id} is not internal.");

        List<BTreeNodeReference> newChildrenRef = new List<BTreeNodeReference>(ChildrenRef);

        int index = newChildrenRef.BinarySearch(oldRef);

        newChildrenRef.RemoveAt(index);

        newChildrenRef.Insert(index, leftRef);
        newChildrenRef.Insert(index + 1, rightRef);

        return new BTreeNode(Id, false, null, newChildrenRef, Amount);
    }

    public List<BTreeNodeReference> MatchingReferences(RecordKey key)
    {
        List<BTreeNodeReference> result = new List<BTreeNodeReference>();

        int index = FindChildReference(key);

        if (index < 0)
            index = ~index;

        result.Add(ChildrenRef[index]);

        // todo: don't depend on accountId

        for (int i = index + 1; i < ChildrenRef.Count; i++)
        {
            BTreeNodeReference childRef = ChildrenRef[i];

            if (childRef.FirstKey <= key && childRef.LastKey >= key)
            {
                result.Add(childRef);
            }
            else if (childRef.FirstKey.AccountId == key.AccountId)
            {
                result.Add(childRef);
            }
            else
            {
                return result;
            }
        }

        return result;
    }

    public decimal GetAmount()
    {
        return Amount;
    }
}