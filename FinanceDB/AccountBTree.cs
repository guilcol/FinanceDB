namespace FinanceDB;

/// <summary>
/// Per-account B-tree implementation managing records for a single account.
///
/// <para><b>THREADING MODEL - CRITICAL:</b></para>
/// This class is designed for SINGLE-THREADED, EXCLUSIVE-ACCESS operation.
/// There are NO concurrency controls. The following assumptions MUST hold:
/// <list type="bullet">
///   <item>Only one thread calls methods on this instance at a time</item>
///   <item>No concurrent Save() operations</item>
///   <item>No shared instances across threads</item>
/// </list>
/// Violation of these assumptions results in UNDEFINED BEHAVIOR.
///
/// <para><b>MUTATION PATH AUDIT (v1):</b></para>
/// All mutations flow through these paths:
/// <list type="bullet">
///   <item>Insert: Insert() -> InsertOnSubtree() -> BTreeNode.WithNewRecord() -> _nodes.Put()</item>
///   <item>Update: Update() -> UpdateOnSubtree() -> BTreeNode.WithUpdatedRecord() -> _nodes.Put()</item>
///   <item>Delete: Delete() -> DeleteOnSubtree() -> BTreeNode.WithDeletedRecord() -> _nodes.Put()</item>
///   <item>Save: Save() -> SplitNode() (if needed) -> _nodes.Save()</item>
/// </list>
///
/// <para><b>FIXED LIMITATIONS:</b></para>
/// See OPERATION_SEMANTICS.md for complete list. Key issues:
/// <list type="bullet">
///   <item>FL-001: Delete does not update Amount field</item>
///   <item>FL-002: Update does not update Amount field</item>
///   <item>FL-007: No tree rebalancing after delete</item>
///   <item>FL-008: AdjustKey scans entire tree (O(n))</item>
/// </list>
/// </summary>
public class AccountBTree
{
    private readonly string _accountId;
    private readonly Random _rand;

    /// <summary>
    /// B-tree degree. WARNING: Static field shared across all instances.
    /// THREADING: Not thread-safe. Set once at startup before any operations.
    /// </summary>
    public static int Degree;

    private readonly INodeStorage _nodes;

    /// <summary>
    /// Creates a new AccountBTree for the specified account.
    /// THREADING: Constructor is not thread-safe. Create instances on single thread.
    /// </summary>
    public AccountBTree(Random rand, int deg, string accountId)
    {
        // THREADING ASSUMPTION: Called from single thread during initialization.
        // No concurrent access during construction.
        _rand = rand;
        Degree = deg;
        _accountId = accountId;
        _nodes = new FileNodeStorage(rand, accountId);
    }

    #region Mutation Entry Point: Save

    /// <summary>
    /// Persists the B-tree to disk after handling any overflowing nodes.
    ///
    /// <para><b>THREADING:</b> Must not be called concurrently with any other operation.</para>
    ///
    /// <para><b>ATOMICITY:</b> NOT ATOMIC. Split operations and file writes are sequential.
    /// Failure mid-operation leaves tree in inconsistent state.</para>
    ///
    /// <para><b>ALGORITHM:</b></para>
    /// <code>
    /// while (any node is overflowing):
    ///     split that node
    ///     restart scan (tree structure changed)
    /// write all cached nodes to disk
    /// </code>
    /// </summary>
    public void Save()
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent mutations or queries.
        // This method modifies tree structure (splits) and writes to disk.

        bool resetLoop = true;

        while (resetLoop)
        {
            resetLoop = false;
            foreach (var item in _nodes.List())
            {
                if (item.isOverflowing())
                {
                    // Split modifies tree structure. Must restart iteration.
                    SplitNode(item);
                    resetLoop = true;
                    break;
                }
            }
        }

        // FIXED LIMITATION FL-004: Only writes NEW files. See OPERATION_SEMANTICS.md.
        _nodes.Save();
    }

    #endregion

    #region Mutation Entry Point: Load

    /// <summary>
    /// Placeholder for load operation. Actual loading is lazy via FileNodeStorage.Get().
    ///
    /// <para><b>THREADING:</b> Safe to call once during initialization.</para>
    /// </summary>
    public void Load()
    {
        // THREADING ASSUMPTION: Called once during initialization, before concurrent access.
        // Loading is handled lazily by FileNodeStorage.Get()
        // which reads from disk if not in cache
    }

    #endregion

    #region Mutation Entry Point: Insert

    /// <summary>
    /// Inserts a record into the B-tree.
    ///
    /// <para><b>THREADING:</b> Must not be called concurrently with any other operation.</para>
    ///
    /// <para><b>ATOMICITY:</b> ATOMIC within in-memory state. Either the record is inserted
    /// with all parent references updated, or no change occurs (duplicate key).</para>
    ///
    /// <para><b>MUTATION PATH:</b></para>
    /// <code>
    /// Insert() -> InsertOnSubtree(root, record)
    ///          -> [recursive descent to leaf]
    ///          -> BTreeNode.WithNewRecord() [creates new node]
    ///          -> _nodes.Put() [updates cache]
    ///          -> [propagate reference updates to root]
    /// </code>
    /// </summary>
    /// <param name="record">Record to insert. Key must not already exist.</param>
    /// <returns>True if inserted; false if key already exists.</returns>
    public bool Insert(Record record)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent reads or writes.

        var root = _nodes.Get(0);
        if (root == null)
        {
            // First record in tree - create root leaf node
            root = new BTreeNode(0, true, new[] { record }, null, record.GetAmount());
            _nodes.Put(root);
            return true;
        }

        (bool Inserted, BTreeNode Node) result = InsertOnSubtree(root, record);
        return result.Inserted;
    }

    /// <summary>
    /// Recursive insertion into subtree rooted at node.
    /// THREADING: Internal method. Caller must hold exclusive access.
    /// </summary>
    private (bool Inserted, BTreeNode Node) InsertOnSubtree(BTreeNode node, Record record)
    {
        // THREADING ASSUMPTION: Exclusive access held by caller.

        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index >= 0)
            {
                // Duplicate key - no change
                return (Inserted: false, Node: null);
            }

            // INVARIANT: WithNewRecord correctly updates Amount field.
            BTreeNode newNode = node.WithNewRecord(~index, record);
            _nodes.Put(newNode);
            return (Inserted: true, Node: newNode);
        }

        // Internal node - find correct child
        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0)
            (selectedChild, childIndex) = node.SelectChildReference(~childIndex, _rand);
        else
            selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = _nodes.Get(selectedChild.ChildId);
        (bool Inserted, BTreeNode Node) subtreeResult = InsertOnSubtree(childNode, record);

        if (subtreeResult.Inserted)
        {
            // Update parent reference to reflect child's new bounds/amount
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            // FIXED LIMITATION FL-003: WithModifiedReference mutates original array.
            // See OPERATION_SEMANTICS.md.
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            _nodes.Put(newNode);
            return (Inserted: true, Node: newNode);
        }

        return (Inserted: false, Node: null);
    }

    #endregion

    #region Mutation Entry Point: Delete

    /// <summary>
    /// Deletes a record from the B-tree.
    ///
    /// <para><b>THREADING:</b> Must not be called concurrently with any other operation.</para>
    ///
    /// <para><b>ATOMICITY:</b> ATOMIC within in-memory state. Either the record is deleted
    /// with all parent references updated, or no change occurs (not found).</para>
    ///
    /// <para><b>FIXED LIMITATIONS:</b></para>
    /// <list type="bullet">
    ///   <item>FL-001: Amount field NOT updated on delete. GetBalance() will be incorrect.</item>
    ///   <item>FL-007: No tree rebalancing. Tree may become unbalanced.</item>
    /// </list>
    ///
    /// <para><b>MUTATION PATH:</b></para>
    /// <code>
    /// Delete() -> DeleteOnSubtree(root, record)
    ///          -> [recursive descent to leaf]
    ///          -> BTreeNode.WithDeletedRecord() [creates new node, Amount NOT updated]
    ///          -> _nodes.Put() [updates cache]
    ///          -> [propagate reference updates to root]
    /// </code>
    /// </summary>
    /// <param name="record">Record to delete. Matched by Key.</param>
    /// <returns>True if deleted; false if not found.</returns>
    public bool Delete(Record record)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent reads or writes.

        var root = _nodes.Get(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Deleted, BTreeNode Node) result = DeleteOnSubtree(root, record);

        return result.Deleted;
    }

    /// <summary>
    /// Recursive deletion from subtree rooted at node.
    /// THREADING: Internal method. Caller must hold exclusive access.
    ///
    /// FIXED LIMITATION FL-001: Amount field is NOT updated when record is deleted.
    /// The deleted record's amount remains in the tree's balance calculation.
    /// </summary>
    public (bool Deleted, BTreeNode Node) DeleteOnSubtree(BTreeNode node, Record record)
    {
        // THREADING ASSUMPTION: Exclusive access held by caller.

        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index < 0)
            {
                // Record not found - no change
                return (Deleted: false, Node: null);
            }

            // FIXED LIMITATION FL-001: WithDeletedRecord does NOT subtract amount.
            // The Amount field in the new node equals the old node's Amount.
            // This causes GetBalance() to return incorrect values after delete.
            BTreeNode newNode = node.WithDeletedRecord(index, record);
            _nodes.Put(newNode);
            return (Deleted: true, Node: newNode);
        }

        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0)
            return (Deleted: false, Node: null);
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = _nodes.Get(selectedChild.ChildId);
        (bool Deleted, BTreeNode Node) subtreeResult = DeleteOnSubtree(childNode, record);

        if (subtreeResult.Deleted)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            // FIXED LIMITATION FL-003: WithModifiedReference mutates original array.
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            _nodes.Put(newNode);
            return (Deleted: true, Node: newNode);
        }

        return (Deleted: false, Node: null);
    }

    #endregion

    #region Mutation Entry Point: Update

    /// <summary>
    /// Updates a record in the B-tree.
    ///
    /// <para><b>THREADING:</b> Must not be called concurrently with any other operation.</para>
    ///
    /// <para><b>ATOMICITY:</b> ATOMIC within in-memory state. Either the record is updated,
    /// or no change occurs (not found).</para>
    ///
    /// <para><b>FIXED LIMITATION FL-002:</b> Amount field NOT updated on update.
    /// If the record's amount changes, GetBalance() will return incorrect values.</para>
    ///
    /// <para><b>MUTATION PATH:</b></para>
    /// <code>
    /// Update() -> UpdateOnSubtree(root, record)
    ///          -> [recursive descent to leaf]
    ///          -> BTreeNode.WithUpdatedRecord() [creates new node, Amount NOT updated]
    ///          -> _nodes.Put() [updates cache]
    ///          -> [propagate reference updates to root]
    /// </code>
    /// </summary>
    /// <param name="record">Record with new values. Key must exist.</param>
    /// <returns>True if updated; false if key not found.</returns>
    public bool Update(Record record)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent reads or writes.

        var root = _nodes.Get(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Updated, BTreeNode Node) result = UpdateOnSubtree(root, record);

        return result.Updated;
    }

    /// <summary>
    /// Recursive update in subtree rooted at node.
    /// THREADING: Internal method. Caller must hold exclusive access.
    ///
    /// FIXED LIMITATION FL-002: Amount field is NOT updated when record amount changes.
    /// </summary>
    public (bool Updated, BTreeNode Node) UpdateOnSubtree(BTreeNode node, Record record)
    {
        // THREADING ASSUMPTION: Exclusive access held by caller.

        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index < 0)
            {
                return (Updated: false, Node: null);
            }

            // FIXED LIMITATION FL-002: WithUpdatedRecord does NOT adjust amount.
            // If record.Amount differs from old amount, the delta is lost.
            // This causes GetBalance() to return incorrect values after update.
            BTreeNode newNode = node.WithUpdatedRecord(index, record);
            _nodes.Put(newNode);
            return (Updated: true, Node: newNode);
        }

        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0)
            return (Updated: false, Node: null);
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = _nodes.Get(selectedChild.ChildId);
        (bool Updated, BTreeNode Node) subtreeResult = UpdateOnSubtree(childNode, record);

        if (subtreeResult.Updated)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            // FIXED LIMITATION FL-003: WithModifiedReference mutates original array.
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            _nodes.Put(newNode);
            return (Updated: true, Node: newNode);
        }

        return (Updated: false, Node: null);
    }

    #endregion

    #region Split Operations (Called During Save)

    /// <summary>
    /// Splits an overflowing node into multiple nodes.
    /// THREADING: Called only during Save(). Exclusive access required.
    /// </summary>
    public void SplitNode(BTreeNode node)
    {
        // THREADING ASSUMPTION: Called during Save() which has exclusive access.
        if (node.IsLeaf)
        {
            SplitLeafNode(node);
            return;
        }
        SplitInternalRoot(node);
    }

    /// <summary>
    /// Splits an overflowing leaf node.
    /// THREADING: Internal method. Caller (Save) must hold exclusive access.
    /// </summary>
    private void SplitLeafNode(BTreeNode node)
    {
        // THREADING ASSUMPTION: Exclusive access held by Save().

        int totalLength = node.Records.Length;
        int numSegments = totalLength / Degree;

        int capacity = numSegments * Degree;
        if (capacity < totalLength)
            ++numSegments;

        int correctLength = totalLength / numSegments;

        BTreeNode[] newNodes = new BTreeNode[numSegments];
        BTreeNodeReference[] newReferences = new BTreeNodeReference[numSegments];

        for (int start = 0, segmentIndex = 0; segmentIndex < numSegments; start += correctLength, segmentIndex++)
        {
            int segmentLength;

            if (segmentIndex == numSegments - 1)
                segmentLength = totalLength - start;
            else
                segmentLength = correctLength;

            long newId;

            if (segmentIndex == 0 && node.Id != 0)
            {
                // Repurpose ID from this node
                newId = node.Id;
            }
            else
            {
                newId = _nodes.GenerateNodeId();
            }

            Record[] segment = new Record[segmentLength];
            Array.Copy(node.Records, start, segment, 0, segmentLength);

            decimal segmentBalance = segment.Sum(record => record.GetAmount());

            newNodes[segmentIndex] = new BTreeNode(newId, true, segment, null, segmentBalance);
            newReferences[segmentIndex] = newNodes[segmentIndex].GetSelfReference();
            _nodes.Put(newNodes[segmentIndex]);
        }

        if (node.Id == 0)
        {
            BTreeNode newRoot = new BTreeNode(0, false, null, newReferences, node.GetAmount());
            _nodes.Put(newRoot);
        }
        else
        {
            BTreeNode parentNode = FindParentNode(_nodes.Get(0), node.GetSelfReference());
            BTreeNode newParentNode = parentNode.WithNewReferences(node.GetSelfReference(), newReferences);
            _nodes.Put(newParentNode);
        }
    }

    /// <summary>
    /// Splits an overflowing internal node.
    /// THREADING: Internal method. Caller (Save) must hold exclusive access.
    /// </summary>
    public void SplitInternalRoot(BTreeNode node)
    {
        // THREADING ASSUMPTION: Exclusive access held by Save().

        int totalLength = node.ChildrenRef.Length;
        int numSegments = totalLength / Degree;

        int capacity = numSegments * Degree;
        if (capacity < totalLength)
            ++numSegments;

        int correctLength = totalLength / numSegments;

        BTreeNode[] newNodes = new BTreeNode[numSegments];
        BTreeNodeReference[] newReferences = new BTreeNodeReference[numSegments];

        for (int start = 0, segmentIndex = 0; segmentIndex < numSegments; start += correctLength, segmentIndex++)
        {
            int segmentLength;

            if (segmentIndex == numSegments - 1)
                segmentLength = totalLength - start;
            else
                segmentLength = correctLength;

            long newId = _nodes.GenerateNodeId();

            BTreeNodeReference[] segment = new BTreeNodeReference[segmentLength];
            Array.Copy(node.ChildrenRef, start, segment, 0, segmentLength);

            decimal segmentBalance = segment.Sum(reference => reference.GetAmount());

            newNodes[segmentIndex] = new BTreeNode(newId, false, null, segment, segmentBalance);
            newReferences[segmentIndex] = newNodes[segmentIndex].GetSelfReference();
            _nodes.Put(newNodes[segmentIndex]);
        }

        if (node.Id == 0)
        {
            BTreeNode newRoot = new BTreeNode(0, false, null, newReferences, node.GetAmount());
            _nodes.Put(newRoot);
        }
        else
        {
            BTreeNode parentNode = FindParentNode(_nodes.Get(0), node.GetSelfReference());
            BTreeNode newParentNode = parentNode.WithNewReferences(node.GetSelfReference(), newReferences);
            _nodes.Put(newParentNode);
        }
    }

    /// <summary>
    /// Finds the parent node of a given node reference.
    /// THREADING: Internal method. Caller must hold exclusive access.
    /// </summary>
    private BTreeNode FindParentNode(BTreeNode nodeTravel, BTreeNodeReference targetRef)
    {
        // THREADING ASSUMPTION: Exclusive access held by caller.

        BTreeNodeReference reference = nodeTravel.ClosestReferenceInNode(targetRef);

        if (reference == targetRef)
            return nodeTravel;

        BTreeNode childNode = _nodes.Get(reference.ChildId);

        return FindParentNode(childNode, targetRef);
    }

    #endregion

    #region Query Operations (Non-Mutating)

    // THREADING NOTE: Query operations are safe to call IF no concurrent mutations.
    // In single-threaded model, this is guaranteed. Do not rely on query safety
    // in multi-threaded scenarios.

    private int GetIndexFromKey(RecordKey key, Record[] list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(list, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    /// <summary>
    /// Lists all records in this account's tree.
    /// THREADING: Safe only if no concurrent mutations.
    /// </summary>
    public IReadOnlyList<Record>? List()
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        RecordKey dummyKey = new RecordKey(_accountId, DateTime.MinValue, 0);
        return CollectRecordsByAccountId(dummyKey, _nodes.Get(0), new List<Record>());
    }

    private List<Record> CollectRecordsByAccountId(RecordKey key, BTreeNode node, List<Record> list)
    {
        List<Record> result = list;

        if (node.IsLeaf)
            result.AddRange(node.Records);
        else
        {
            foreach (BTreeNodeReference childRef in node.ChildrenRef)
            {
                BTreeNode childNode = _nodes.Get(childRef.ChildId);
                result = CollectRecordsByAccountId(key, childNode, list);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets total balance for account.
    /// THREADING: Safe only if no concurrent mutations.
    ///
    /// WARNING: Due to FL-001 and FL-002, balance may be incorrect after deletes/updates.
    /// </summary>
    public decimal GetBalance()
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        return _nodes.Get(0).GetAmount();
    }

    /// <summary>
    /// Gets cumulative balance up to specified key.
    /// THREADING: Safe only if no concurrent mutations.
    ///
    /// WARNING: Due to FL-001 and FL-002, balance may be incorrect after deletes/updates.
    /// </summary>
    public decimal GetBalance(RecordKey key)
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        return CollectBalanceByAccountId(key, _nodes.Get(0), 0);
    }

    public decimal CollectBalanceByAccountId(RecordKey key, BTreeNode node, decimal balance)
    {
        decimal result = balance;

        if (node.IsLeaf)
        {
            for (int i = 0; i < node.Records.Length; i++)
            {
                Record currentRecord = node.Records[i];
                if (currentRecord.Key > key)
                    return result;
                result += currentRecord.GetAmount();
            }
        }
        else
        {
            foreach (BTreeNodeReference childRef in node.ChildrenRef)
            {
                if (key > childRef.LastKey)
                {
                    result += childRef.GetAmount();
                }
                else
                {
                    BTreeNode childNode = _nodes.Get(childRef.ChildId);
                    return CollectBalanceByAccountId(key, childNode, result);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Counts total records in tree.
    /// THREADING: Safe only if no concurrent mutations.
    /// </summary>
    public int RecordCount()
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        var root = _nodes.Get(0);
        if (root == null)
            return 0;
        return CountRecordsInSubtree(root);
    }

    private int CountRecordsInSubtree(BTreeNode node)
    {
        if (node.IsLeaf)
            return node.Records?.Length ?? 0;

        int count = 0;
        foreach (var childRef in node.ChildrenRef)
        {
            var childNode = _nodes.Get(childRef.ChildId);
            count += CountRecordsInSubtree(childNode);
        }
        return count;
    }

    public bool ContainsKey(RecordKey key, BTreeNode node)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks if key exists in tree.
    /// THREADING: Safe only if no concurrent mutations.
    /// </summary>
    public bool ContainsKey(RecordKey key)
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        var root = _nodes.Get(0);
        if (root == null)
            return false;

        return ContainsKeyInternal(key, _nodes.Get(0));
    }

    private bool ContainsKeyInternal(RecordKey key, BTreeNode node)
    {
        if (node.IsLeaf)
            return ContainsKeyInRecords(key, node.Records);

        int childRefIndex = node.FindChildReference(key);

        if (childRefIndex < 0)
            return false;

        BTreeNodeReference childRef = node.ChildrenRef[childRefIndex];

        BTreeNode childNode = _nodes.Get(childRef.ChildId);

        return ContainsKeyInternal(key, childNode);
    }

    public bool ContainsKeyInRecords(RecordKey key, Record[] list)
    {
        int index = GetIndexFromKey(key, list);
        if (index >= 0)
            return true;
        return false;
    }

    /// <summary>
    /// Reads a record by key.
    /// THREADING: Safe only if no concurrent mutations.
    /// </summary>
    public Record Read(RecordKey key)
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        var root = _nodes.Get(0);
        if (root == null)
            return null;
        Record record = ReadFromSubtree(key, root);
        return record;
    }

    private Record ReadFromSubtree(RecordKey key, BTreeNode node)
    {
        Record record;
        if (node.IsLeaf)
        {
            record = ReadFromRecords(node.Records, key);
            if (record != null)
                return record;
            throw new Exception("Record does not exist.");
        }

        int childIndex = node.FindChildReference(key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0)
            throw new Exception("Record does not exist.");
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = _nodes.Get(selectedChild.ChildId);

        record = ReadFromSubtree(key, childNode);

        if (record != null)
            return record;

        throw new Exception("Record is null");
    }

    public Record? ReadFromRecords(Record[] currentNodeRecords, RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(currentNodeRecords, dummyRecord, ByKeyRecordComparer.Instance);
        if (index >= 0)
            return currentNodeRecords[index];
        return null;
    }

    /// <summary>
    /// Returns node cache size.
    /// THREADING: Safe only if no concurrent mutations.
    /// </summary>
    public int CacheLength()
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.
        return _nodes.CacheLength();
    }

    /// <summary>
    /// Computes adjusted key with next available sequence number.
    /// THREADING: Safe only if no concurrent mutations.
    ///
    /// FIXED LIMITATION FL-008: This method scans the entire tree to find max sequence.
    /// Complexity is O(n) instead of O(log n).
    /// </summary>
    public RecordKey AdjustKey(RecordKey key)
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.

        var root = _nodes.Get(0);
        if (root == null)
            return key;

        // FIXED LIMITATION FL-008: Full tree scan. See OPERATION_SEMANTICS.md.
        uint maxSequence = FindMaxSequenceForDate(root, key.AccountId, key.Date);

        if (maxSequence == 0 && !ContainsKey(key))
            return key;

        return key.WithSequence(maxSequence + 1);
    }

    /// <summary>
    /// FIXED LIMITATION FL-008: Scans entire tree to find max sequence for date.
    /// This is O(n) complexity. A proper implementation would use an index.
    /// </summary>
    private uint FindMaxSequenceForDate(BTreeNode node, string accountId, DateTime date)
    {
        // THREADING ASSUMPTION: No concurrent mutations during query.

        uint maxSeq = 0;

        if (node.IsLeaf)
        {
            foreach (var record in node.Records)
            {
                if (record.Key.AccountId == accountId && record.Key.Date == date)
                {
                    if (record.Key.Sequence > maxSeq)
                        maxSeq = record.Key.Sequence;
                }
            }
        }
        else
        {
            foreach (var childRef in node.ChildrenRef)
            {
                var childNode = _nodes.Get(childRef.ChildId);
                uint childMax = FindMaxSequenceForDate(childNode, accountId, date);
                if (childMax > maxSeq)
                    maxSeq = childMax;
            }
        }

        return maxSeq;
    }

    #endregion
}
