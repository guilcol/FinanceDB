using FinanceDB.Core.Models;

namespace FinanceDB.Core.Storage;

/// <summary>
/// Immutable B-tree node structure representing either a leaf node (containing records)
/// or an internal node (containing child references).
/// </summary>
public sealed class BTreeNode
{
    /// <summary>
    /// Unique identifier for this node. Root node always has Id = 0.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// True if this is a leaf node (contains Records), false if internal (contains ChildrenRef).
    /// </summary>
    public bool IsLeaf { get; }

    /// <summary>
    /// Records stored in this leaf node. Null for internal nodes.
    /// Records are sorted by Key in ascending order.
    /// </summary>
    public Record[]? Records { get; }

    /// <summary>
    /// References to child nodes. Null for leaf nodes.
    /// References are sorted by FirstKey in ascending order.
    /// </summary>
    public BTreeNodeReference[] ChildrenRef { get; }

    /// <summary>
    /// Cached sum of all amounts in the subtree rooted at this node.
    /// </summary>
    public decimal Amount;

    /// <summary>
    /// Creates a new BTreeNode.
    /// </summary>
    public BTreeNode(long id, bool isLeaf, Record[]? records, BTreeNodeReference[]? childrenRef, decimal amount)
    {
        Id = id;
        IsLeaf = isLeaf;
        Records = records;
        ChildrenRef = childrenRef;
        Amount = amount;
    }

    /// <summary>
    /// Checks if node can accept more entries before reaching degree limit.
    /// </summary>
    public bool IsFull()
    {
        if (Records == null)
            return ChildrenRef.Length < BTreeRs.degree;
        return Records.Length < BTreeRs.degree;
    }

    /// <summary>
    /// Checks if node exceeds the degree limit and needs splitting.
    /// </summary>
    public bool isOverflowing()
    {
        if (Records == null)
            return ChildrenRef.Length > BTreeRs.degree;
        return Records.Length > BTreeRs.degree;
    }

    #region Immutable Transformation Methods

    /// <summary>
    /// Creates a new leaf node with the record inserted at the specified index.
    /// </summary>
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

    /// <summary>
    /// Creates a new leaf node with the record at the specified index removed.
    /// </summary>
    public BTreeNode WithDeletedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = new Record[Records.Length - 1];
        Array.Copy(Records, 0, records, 0, index);
        Array.Copy(Records, index + 1, records, index, Records.Length - (index + 1));

        return new BTreeNode(Id, true, records, null, Amount);
    }

    /// <summary>
    /// Creates a new leaf node with the record at the specified index replaced.
    /// </summary>
    public BTreeNode WithUpdatedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = (Record[])Records.Clone();
        records[index] = record;

        return new BTreeNode(Id, true, records, null, Amount);
    }

    /// <summary>
    /// Creates a new internal node with the child reference at the specified index replaced.
    /// </summary>
    public BTreeNode WithModifiedReference(int childIndex, BTreeNodeReference newChildRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on internal nodes. Node {Id} is a leaf.");

        BTreeNodeReference[] childrenRef = ChildrenRef;
        childrenRef[childIndex] = newChildRef;

        return new BTreeNode(Id, false, null, childrenRef, Amount);
    }

    #endregion

    #region Search Operations

    /// <summary>
    /// Binary search for record by key in this leaf node.
    /// </summary>
    public int GetIndexFromKey(RecordKey recordKey)
    {
        Record dummyRecord = new Record(recordKey, "", 0);
        int index = Array.BinarySearch(Records, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    /// <summary>
    /// Binary search for child reference containing the given key.
    /// </summary>
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

    /// <summary>
    /// Checks if key falls within child reference bounds.
    /// </summary>
    private int KeyInBounds(RecordKey key, BTreeNodeReference childRef)
    {
        if (key >= childRef.FirstKey && key <= childRef.LastKey)
            return 0;
        if (key >= childRef.FirstKey)
            return 1;
        return -1;
    }

    /// <summary>
    /// Selects a child reference for insertion when key falls between references.
    /// </summary>
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

    /// <summary>
    /// Finds the child reference closest to the target reference.
    /// </summary>
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

    #endregion

    #region Reference Generation

    /// <summary>
    /// Creates a BTreeNodeReference representing this node.
    /// </summary>
    public BTreeNodeReference GetSelfReference()
    {
        if (IsLeaf)
            return new BTreeNodeReference(Records?[0].Key, Records?[Records.Length - 1].Key, Id, Amount);
        return new BTreeNodeReference(ChildrenRef?[0].FirstKey, ChildrenRef?[ChildrenRef.Length - 1].LastKey, Id,
            Amount);
    }

    /// <summary>
    /// Creates a new internal node with one child reference replaced by multiple references.
    /// </summary>
    public BTreeNode WithNewReferences(BTreeNodeReference oldRef, BTreeNodeReference[] newRefs)
    {
        if (IsLeaf)
            throw new InvalidOperationException(
                $"Operation is only allowed on internal node. Node {Id} is not internal.");

        BTreeNodeReference[] newChildrenRef = ChildrenRef;

        int index = Array.BinarySearch(newChildrenRef, oldRef);

        if (index < 0)
        {
            throw new ArgumentException("Reference not found in children", nameof(oldRef));
        }

        int newSize = newChildrenRef.Length - 1 + newRefs.Length;

        BTreeNodeReference[] updatedChildrenRef = new BTreeNodeReference[newSize];

        for (int i = 0; i < index; i++)
        {
            updatedChildrenRef[i] = newChildrenRef[i];
        }

        for (int i = 0; i < newRefs.Length; i++)
        {
            updatedChildrenRef[index + i] = newRefs[i];
        }

        for (int i = index + 1; i < newChildrenRef.Length; i++)
        {
            updatedChildrenRef[i + newRefs.Length - 1] = newChildrenRef[i];
        }

        newChildrenRef = updatedChildrenRef;

        return new BTreeNode(Id, false, null, newChildrenRef, Amount);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Finds all child references that might contain records for the given key's account.
    /// </summary>
    public List<BTreeNodeReference> MatchingReferences(RecordKey key)
    {
        List<BTreeNodeReference> result = new List<BTreeNodeReference>();

        int index = FindChildReference(key);

        if (index < 0)
            index = ~index;

        result.Add(ChildrenRef[index]);

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

    /// <summary>
    /// Returns the cached Amount value for this subtree.
    /// </summary>
    public decimal GetAmount()
    {
        return Amount;
    }

    #endregion
}
