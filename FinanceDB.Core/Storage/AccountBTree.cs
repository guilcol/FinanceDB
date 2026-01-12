using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Core.Storage;

/// <summary>
/// Per-account B-tree implementation managing records for a single account.
/// </summary>
public class AccountBTree
{
    private readonly string _accountId;
    private readonly Random _rand;

    /// <summary>
    /// B-tree degree. WARNING: Static field shared across all instances.
    /// </summary>
    public static int Degree;

    private readonly INodeStorage _nodes;

    /// <summary>
    /// Creates a new AccountBTree for the specified account.
    /// </summary>
    public AccountBTree(Random rand, int deg, string accountId)
    {
        _rand = rand;
        Degree = deg;
        _accountId = accountId;
        _nodes = new FileNodeStorage(rand, accountId);
    }

    #region Mutation Entry Point: Save

    /// <summary>
    /// Persists the B-tree to disk after handling any overflowing nodes.
    /// </summary>
    public void Save()
    {
        bool resetLoop = true;

        while (resetLoop)
        {
            resetLoop = false;
            foreach (var item in _nodes.List())
            {
                if (item.isOverflowing())
                {
                    SplitNode(item);
                    resetLoop = true;
                    break;
                }
            }
        }

        _nodes.Save();
    }

    #endregion

    #region Mutation Entry Point: Load

    /// <summary>
    /// Placeholder for load operation. Actual loading is lazy via FileNodeStorage.Get().
    /// </summary>
    public void Load()
    {
    }

    #endregion

    #region Mutation Entry Point: Insert

    /// <summary>
    /// Inserts a record into the B-tree.
    /// </summary>
    public bool Insert(Record record)
    {
        var root = _nodes.Get(0);
        if (root == null)
        {
            root = new BTreeNode(0, true, new[] { record }, null, record.GetAmount());
            _nodes.Put(root);
            return true;
        }

        (bool Inserted, BTreeNode Node) result = InsertOnSubtree(root, record);
        return result.Inserted;
    }

    /// <summary>
    /// Recursive insertion into subtree rooted at node.
    /// </summary>
    private (bool Inserted, BTreeNode Node) InsertOnSubtree(BTreeNode node, Record record)
    {
        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index >= 0)
            {
                return (Inserted: false, Node: null);
            }

            BTreeNode newNode = node.WithNewRecord(~index, record);
            _nodes.Put(newNode);
            return (Inserted: true, Node: newNode);
        }

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
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
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
    /// </summary>
    public bool Delete(Record record)
    {
        var root = _nodes.Get(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Deleted, BTreeNode Node) result = DeleteOnSubtree(root, record);

        return result.Deleted;
    }

    /// <summary>
    /// Recursive deletion from subtree rooted at node.
    /// </summary>
    public (bool Deleted, BTreeNode Node) DeleteOnSubtree(BTreeNode node, Record record)
    {
        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index < 0)
            {
                return (Deleted: false, Node: null);
            }

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
    /// </summary>
    public bool Update(Record record)
    {
        var root = _nodes.Get(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Updated, BTreeNode Node) result = UpdateOnSubtree(root, record);

        return result.Updated;
    }

    /// <summary>
    /// Recursive update in subtree rooted at node.
    /// </summary>
    public (bool Updated, BTreeNode Node) UpdateOnSubtree(BTreeNode node, Record record)
    {
        if (node.IsLeaf)
        {
            int index = node.GetIndexFromKey(record.Key);
            if (index < 0)
            {
                return (Updated: false, Node: null);
            }

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
    /// </summary>
    public void SplitNode(BTreeNode node)
    {
        if (node.IsLeaf)
        {
            SplitLeafNode(node);
            return;
        }
        SplitInternalRoot(node);
    }

    /// <summary>
    /// Splits an overflowing leaf node.
    /// </summary>
    private void SplitLeafNode(BTreeNode node)
    {
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
    /// </summary>
    public void SplitInternalRoot(BTreeNode node)
    {
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
    /// </summary>
    private BTreeNode FindParentNode(BTreeNode nodeTravel, BTreeNodeReference targetRef)
    {
        BTreeNodeReference reference = nodeTravel.ClosestReferenceInNode(targetRef);

        if (reference == targetRef)
            return nodeTravel;

        BTreeNode childNode = _nodes.Get(reference.ChildId);

        return FindParentNode(childNode, targetRef);
    }

    #endregion

    #region Query Operations (Non-Mutating)

    private int GetIndexFromKey(RecordKey key, Record[] list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(list, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    /// <summary>
    /// Lists all records in this account's tree.
    /// </summary>
    public IReadOnlyList<Record>? List()
    {
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
    /// </summary>
    public decimal GetBalance()
    {
        return _nodes.Get(0).GetAmount();
    }

    /// <summary>
    /// Gets cumulative balance up to specified key.
    /// </summary>
    public decimal GetBalance(RecordKey key)
    {
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
    /// </summary>
    public int RecordCount()
    {
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
    /// </summary>
    public bool ContainsKey(RecordKey key)
    {
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
    /// </summary>
    public Record Read(RecordKey key)
    {
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
    /// </summary>
    public int CacheLength()
    {
        return _nodes.CacheLength();
    }

    /// <summary>
    /// Computes adjusted key with next available sequence number.
    /// </summary>
    public RecordKey AdjustKey(RecordKey key)
    {
        var root = _nodes.Get(0);
        if (root == null)
            return key;

        uint maxSequence = FindMaxSequenceForDate(root, key.AccountId, key.Date);

        if (maxSequence == 0 && !ContainsKey(key))
            return key;

        return key.WithSequence(maxSequence + 1);
    }

    /// <summary>
    /// Scans entire tree to find max sequence for date.
    /// </summary>
    private uint FindMaxSequenceForDate(BTreeNode node, string accountId, DateTime date)
    {
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
