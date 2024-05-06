using System.Runtime.InteropServices.JavaScript;

namespace FinanceDB;

public sealed class BTreeNode
{
    public long Id { get; }
    public bool IsLeaf { get; }
    public Record[]? Records { get; }
    public BTreeNodeReference[] ChildrenRef { get; }

    public decimal Amount;

    public BTreeNode(long id, bool isLeaf, Record[]? records, BTreeNodeReference[]? childrenRef, decimal amount)
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
            return ChildrenRef.Length < BTreeRs.degree;
        return Records.Length < BTreeRs.degree; //TODO: Fix isFull() return true for any value under DEGREE
    }

    public bool isOverflowing()
    {
        if (Records == null)
            return ChildrenRef.Length > BTreeRs.degree;
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

        BTreeNodeReference[] childrenRef = ChildrenRef;
        childrenRef[childIndex] = newChildRef;

        return new BTreeNode(Id, false, null, childrenRef, Amount);
    }

    public int GetIndexFromKey(RecordKey recordKey)
    {
        Record dummyRecord = new Record(recordKey, "", 0);
        int index = Array.BinarySearch(Records, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    public int FindChildReference(RecordKey key)
    {
        int left = 0;

        int right = ChildrenRef.Length - 1;

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
        if (index == ChildrenRef.Length)
            return (ChildrenRef[index - 1], index - 1);

        if (rand.Next(2) == 0)
            return (ChildrenRef[index], index);
        return (ChildrenRef[index - 1], index - 1);
    }

    public BTreeNodeReference ClosestReferenceInNode(BTreeNodeReference targetRef)
    {
        RecordKey dummyKey = targetRef.FirstKey;

        if (ChildrenRef == null)
        {
            throw new Exception("For some reason, you managed to call this in a leaf node.");
        }

        int index = FindChildReference(dummyKey);

        if (index < 0)
            throw new Exception("What the fuck?");

        return ChildrenRef[index];
    }

    public BTreeNodeReference GetSelfReference()
    {
        if (IsLeaf)
            return new BTreeNodeReference(Records?[0].Key, Records?[Records.Length - 1].Key, Id, Amount);
        return new BTreeNodeReference(ChildrenRef?[0].FirstKey, ChildrenRef?[ChildrenRef.Length - 1].LastKey, Id,
            Amount);
    }

    public BTreeNode WithNewReferences(BTreeNodeReference oldRef, BTreeNodeReference[] newRefs)
    {
        if (IsLeaf)
            throw new InvalidOperationException(
                $"Operation is only allowed on internal node. Node {Id} is not internal.");

        // Convert the list of children references to a list to make modifications easier
        BTreeNodeReference[] newChildrenRef = ChildrenRef;

        // Find the index of oldRef in the list of children
        int index = Array.BinarySearch(newChildrenRef, oldRef);

        if (index < 0)
        {
            throw new ArgumentException("Reference not found in children", nameof(oldRef));
        }

        // Determine the size of the new array
        int newSize = newChildrenRef.Length - 1 + newRefs.Length; // Remove one, add newRefs.Length

        // Create a new array of the appropriate size
        BTreeNodeReference[] updatedChildrenRef = new BTreeNodeReference[newSize];

        // Copy elements from the old array to the new array up to the index of the old reference
        for (int i = 0; i < index; i++)
        {
            updatedChildrenRef[i] = newChildrenRef[i];
        }

        // Copy new references into the new array at the index
        for (int i = 0; i < newRefs.Length; i++)
        {
            updatedChildrenRef[index + i] = newRefs[i];
        }

        // Copy the remaining elements from the old array to the new array after the new references
        for (int i = index + 1; i < newChildrenRef.Length; i++)
        {
            updatedChildrenRef[i + newRefs.Length - 1] = newChildrenRef[i];
        }

        // Now, updatedChildrenRef contains all old references with the oldRef replaced by newRefs
        newChildrenRef = updatedChildrenRef; // Assuming newChildrenRef can be reassigned


        // Create a new BTreeNode with the updated list of children references
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

        for (int i = index + 1; i < ChildrenRef.Length; i++)
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