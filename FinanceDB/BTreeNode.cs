using System.Runtime.InteropServices.JavaScript;

namespace FinanceDB;

/// <summary>
/// Immutable B-tree node structure representing either a leaf node (containing records)
/// or an internal node (containing child references).
///
/// <para><b>THREADING MODEL:</b></para>
/// BTreeNode instances are intended to be IMMUTABLE. All "With*" methods return NEW instances.
/// However, see FL-003 for a violation of this principle.
///
/// <para><b>INVARIANTS:</b></para>
/// <list type="bullet">
///   <item>IsLeaf == true implies Records != null AND ChildrenRef == null</item>
///   <item>IsLeaf == false implies Records == null AND ChildrenRef != null</item>
///   <item>Amount should equal sum of all amounts in subtree (VIOLATED by FL-001, FL-002)</item>
/// </list>
///
/// <para><b>FIXED LIMITATIONS:</b></para>
/// <list type="bullet">
///   <item>FL-001: WithDeletedRecord does NOT update Amount</item>
///   <item>FL-002: WithUpdatedRecord does NOT update Amount</item>
///   <item>FL-003: WithModifiedReference mutates original ChildrenRef array</item>
/// </list>
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
    ///
    /// <para><b>INVARIANT VIOLATION WARNING:</b></para>
    /// Due to FL-001 and FL-002, this value may be INCORRECT after Delete or Update operations.
    /// Only Insert and Split operations correctly maintain this field.
    /// </summary>
    public decimal Amount;

    /// <summary>
    /// Creates a new BTreeNode.
    /// THREADING: Construction is not thread-safe. Caller must ensure exclusive access.
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
    /// Note: Method name is misleading - returns true when NOT full.
    /// </summary>
    public bool IsFull()
    {
        if (Records == null)
            return ChildrenRef.Length < BTreeRs.degree;
        return Records.Length < BTreeRs.degree; //TODO: Fix isFull() return true for any value under DEGREE
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
    ///
    /// <para><b>INVARIANT:</b> Correctly updates Amount field (Amount + record.Amount).</para>
    ///
    /// THREADING: Returns new instance. Original node unchanged.
    /// </summary>
    /// <param name="index">Position to insert (from binary search complement).</param>
    /// <param name="record">Record to insert.</param>
    /// <returns>New BTreeNode with record inserted.</returns>
    /// <exception cref="InvalidOperationException">If called on internal node.</exception>
    public BTreeNode WithNewRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = new Record[Records.Length + 1];
        Array.Copy(Records, 0, records, 0, index);
        records[index] = record;
        Array.Copy(Records, index, records, index + 1, Records.Length - index);

        // CORRECT: Amount is updated with new record's amount
        return new BTreeNode(Id, true, records, null, Amount + record.GetAmount());
    }

    /// <summary>
    /// Creates a new leaf node with the record at the specified index removed.
    ///
    /// <para><b>FIXED LIMITATION FL-001:</b></para>
    /// Amount field is NOT updated. The deleted record's amount remains in the total.
    /// This causes GetBalance() to return incorrect values after deletions.
    ///
    /// THREADING: Returns new instance. Original node unchanged.
    /// </summary>
    /// <param name="index">Position of record to delete.</param>
    /// <param name="record">Record being deleted (used for documentation, not functionally).</param>
    /// <returns>New BTreeNode with record removed but INCORRECT Amount.</returns>
    /// <exception cref="InvalidOperationException">If called on internal node.</exception>
    public BTreeNode WithDeletedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = new Record[Records.Length - 1];
        Array.Copy(Records, 0, records, 0, index);
        Array.Copy(Records, index + 1, records, index, Records.Length - (index + 1));

        // FIXED LIMITATION FL-001: Amount is NOT decremented.
        // Should be: Amount - record.GetAmount()
        // Current: Amount (unchanged)
        // IMPACT: GetBalance() returns incorrect values after delete.
        return new BTreeNode(Id, true, records, null, Amount);
    }

    /// <summary>
    /// Creates a new leaf node with the record at the specified index replaced.
    ///
    /// <para><b>FIXED LIMITATION FL-002:</b></para>
    /// Amount field is NOT updated. If the new record has a different amount,
    /// the delta is lost. This causes GetBalance() to return incorrect values.
    ///
    /// THREADING: Returns new instance. Original node unchanged.
    /// </summary>
    /// <param name="index">Position of record to update.</param>
    /// <param name="record">New record value.</param>
    /// <returns>New BTreeNode with record replaced but INCORRECT Amount if amounts differ.</returns>
    /// <exception cref="InvalidOperationException">If called on internal node.</exception>
    public BTreeNode WithUpdatedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        Record[] records = (Record[])Records.Clone();
        records[index] = record;

        // FIXED LIMITATION FL-002: Amount is NOT adjusted for amount change.
        // Should be: Amount - oldRecord.Amount + record.Amount
        // Current: Amount (unchanged)
        // IMPACT: GetBalance() returns incorrect values after update with different amount.
        return new BTreeNode(Id, true, records, null, Amount);
    }

    /// <summary>
    /// Creates a new internal node with the child reference at the specified index replaced.
    ///
    /// <para><b>FIXED LIMITATION FL-003 - CRITICAL:</b></para>
    /// This method MUTATES the original ChildrenRef array instead of cloning it.
    /// The assignment `BTreeNodeReference[] childrenRef = ChildrenRef;` creates a reference
    /// to the SAME array, not a copy. Modifying childrenRef[childIndex] modifies the original.
    ///
    /// <para><b>IMPACT:</b></para>
    /// <list type="bullet">
    ///   <item>Violates immutability contract</item>
    ///   <item>Old node references see unexpected mutations</item>
    ///   <item>May cause issues if old nodes are cached or referenced elsewhere</item>
    /// </list>
    ///
    /// THREADING: UNSAFE - mutates original array. Single-threaded access required.
    /// </summary>
    /// <param name="childIndex">Index of child reference to replace.</param>
    /// <param name="newChildRef">New child reference value.</param>
    /// <returns>New BTreeNode, but original's ChildrenRef array is ALSO modified.</returns>
    /// <exception cref="InvalidOperationException">If called on leaf node.</exception>
    public BTreeNode WithModifiedReference(int childIndex, BTreeNodeReference newChildRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on internal nodes. Node {Id} is a leaf.");

        // FIXED LIMITATION FL-003: This does NOT clone the array.
        // This creates a reference to the SAME array object.
        // The modification below affects the ORIGINAL node's ChildrenRef.
        BTreeNodeReference[] childrenRef = ChildrenRef;
        childrenRef[childIndex] = newChildRef;

        // Both the old node and this new node share the same ChildrenRef array.
        // The old node's array has been mutated.
        return new BTreeNode(Id, false, null, childrenRef, Amount);
    }

    #endregion

    #region Search Operations

    /// <summary>
    /// Binary search for record by key in this leaf node.
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
    /// </summary>
    /// <param name="recordKey">Key to search for.</param>
    /// <returns>Non-negative index if found; bitwise complement of insertion point if not found.</returns>
    public int GetIndexFromKey(RecordKey recordKey)
    {
        Record dummyRecord = new Record(recordKey, "", 0);
        int index = Array.BinarySearch(Records, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    /// <summary>
    /// Binary search for child reference containing the given key.
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
    /// </summary>
    /// <param name="key">Key to locate.</param>
    /// <returns>Non-negative index if key is within a child's bounds; bitwise complement of insertion point otherwise.</returns>
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
    /// <returns>0 if in bounds, 1 if key > bounds, -1 if key < bounds.</returns>
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
    /// Uses random selection between adjacent references.
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
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
    /// Used during split operations to locate parent nodes.
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
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
    /// Used to update parent node references after mutations.
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
    /// </summary>
    /// <returns>Reference with this node's key bounds, Id, and Amount.</returns>
    public BTreeNodeReference GetSelfReference()
    {
        if (IsLeaf)
            return new BTreeNodeReference(Records?[0].Key, Records?[Records.Length - 1].Key, Id, Amount);
        return new BTreeNodeReference(ChildrenRef?[0].FirstKey, ChildrenRef?[ChildrenRef.Length - 1].LastKey, Id,
            Amount);
    }

    /// <summary>
    /// Creates a new internal node with one child reference replaced by multiple references.
    /// Used during node splitting.
    /// THREADING: Returns new instance. Original node unchanged.
    /// </summary>
    /// <param name="oldRef">Reference to replace.</param>
    /// <param name="newRefs">Array of new references to insert.</param>
    /// <returns>New BTreeNode with references replaced.</returns>
    /// <exception cref="InvalidOperationException">If called on leaf node.</exception>
    /// <exception cref="ArgumentException">If oldRef not found.</exception>
    public BTreeNode WithNewReferences(BTreeNodeReference oldRef, BTreeNodeReference[] newRefs)
    {
        if (IsLeaf)
            throw new InvalidOperationException(
                $"Operation is only allowed on internal node. Node {Id} is not internal.");

        // Note: Unlike WithModifiedReference, this method creates a new array.
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
    /// THREADING: Read-only operation. Safe if no concurrent mutations.
    /// </summary>
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

    /// <summary>
    /// Returns the cached Amount value for this subtree.
    /// WARNING: May be incorrect after Delete/Update operations (FL-001, FL-002).
    /// </summary>
    public decimal GetAmount()
    {
        return Amount;
    }

    #endregion
}
