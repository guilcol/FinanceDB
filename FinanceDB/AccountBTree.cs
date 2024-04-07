using System.Data.Common;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace FinanceDB;

public class AccountBTree
{
    private readonly string _accountId;
    private readonly Random _rand;
    public static int Degree;
    private readonly Dictionary<long, BTreeNode> _cache = new Dictionary<long, BTreeNode>();

    public AccountBTree(Random rand, int deg, string accountId)
    {
        _rand = rand;
        Degree = deg;
        _accountId = accountId;
    }

    public void Save()
    {
        bool resetLoop = true;

        while (resetLoop)
        {
            resetLoop = false;
            foreach (var item in _cache)
            {
                if (item.Value.isOverflowing()) // Check if current node is overflowing
                {
                    SplitNode(item.Value); // Split the overflowing node
                    resetLoop = true;
                    break;
                }
            }
        }
    }

    public void Load()
    {
        throw new NotImplementedException();
    }

    public bool Insert(Record record)
    {
        var root = ReadNode(0);
        if (root == null)
        {
            root = new BTreeNode(0, true, new [] {record}, null);
            PutOnCache(root);
            return true;
        }

        (bool Inserted, BTreeNode Node) result = InsertOnSubtree(root, record);
        return result.Inserted;
    }

    public bool Delete(Record record)
    {
        var root = ReadNode(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Deleted, BTreeNode Node) result = DeleteOnSubtree(root, record);

        return result.Deleted;
    }

    public bool Update(Record record)
    {
        var root = ReadNode(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Updated, BTreeNode Node) result = UpdateOnSubtree(root, record);

        return result.Updated;
    }


    private (bool Inserted, BTreeNode Node) InsertOnSubtree(BTreeNode node, Record record)
    {
        if (node.IsLeaf) // If a leaf node is reached
        {
            // Find index to insert new record
            int index = node.GetIndexFromKey(record.Key);
            if (index >= 0) // In case record already exists
            {
                return (Inserted: false, Node: null);
            }

            // Return new node with inserted record
            BTreeNode newNode = node.WithNewRecord(~index, record);
            PutOnCache(newNode);
            return (Inserted: true, Node: newNode);
        }

        // If current node is internal, find correct reference
        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0) // If reference is not found, pick a neighboring reference
            (selectedChild, childIndex) = node.SelectChildReference(~childIndex, _rand);
        else
            selectedChild = node.ChildrenRef[childIndex];

        // Upon finding correct reference, recursively call child
        BTreeNode childNode = ReadNode(selectedChild.ChildId);
        (bool Inserted, BTreeNode Node) subtreeResult = InsertOnSubtree(childNode, record);

        // If insertion worked, get reference for new node and pass to parent
        if (subtreeResult.Inserted)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            PutOnCache(newNode);
            return (Inserted: true, Node: newNode);
        }

        // In case insertion did not work
        return (Inserted: false, Node: null);
    }


    public (bool Deleted, BTreeNode Node) DeleteOnSubtree(BTreeNode node, Record record)
    {
        if (node.IsLeaf) // If a leaf node is reached
        {
            // Find index to delete given record
            int index = node.GetIndexFromKey(record.Key);
            if (index < 0) // In case record does not exist
            {
                return (Deleted: false, Node: null);
            }

            // Return new node with deleted record` 
            BTreeNode newNode = node.WithDeletedRecord(index, record);
            PutOnCache(newNode);
            return (Deleted: true, Node: newNode);
        }

        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0) // If reference is not found, there is no record to delete
            return (Deleted: false, Node: null);
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = ReadNode(selectedChild.ChildId);
        (bool Deleted, BTreeNode Node) subtreeResult = DeleteOnSubtree(childNode, record);

        if (subtreeResult.Deleted)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            PutOnCache(newNode);
            return (Deleted: true, Node: newNode);
        }

        return (Deleted: false, Node: null);
    }

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
            PutOnCache(newNode);
            return (Updated: true, Node: newNode);
        }

        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0) // If reference is not found, there is no record to update
            return (Updated: false, Node: null);
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = ReadNode(selectedChild.ChildId);
        (bool Updated, BTreeNode Node) subtreeResult = UpdateOnSubtree(childNode, record);

        if (subtreeResult.Updated)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            PutOnCache(newNode);
            return (Updated: true, Node: newNode);
        }

        return (Updated: false, Node: null);
    }

    public void SplitNode(BTreeNode node)
    {
        if (node.IsLeaf)
        {
            SplitLeafNode(node);
            return;
        }

        if (node.Id == 0)
        {
            SplitInternalRoot(node);
            return;
        }

        SplitInternalNonRoot(node);

    }

    public void SplitLeafNode(BTreeNode node)
    {
        // Find midpoint of Records
        int midPoint = node.Records.Length / 2;

        // Initialize Record lists for the new nodes
        // TODO: Use arrays for this.
        List<Record> lowerRecords = new List<Record>();
        List<Record> upperRecords = new List<Record>();

        // Transfer first half to lowerRecords
        for (int i = 0; i < midPoint; i++)
            lowerRecords.Add(node.Records[i]);

        // Transfer second half to upperRecords
        for (int i = midPoint; i < node.Records.Length; i++)
            upperRecords.Add(node.Records[i]);

        // Create random IDs for left and right nodes
        long newLeftId = _rand.NextInt64(long.MaxValue);
        long newRightId = _rand.NextInt64(long.MaxValue);

        if (node.Id != 0)
            newLeftId = node.Id;
        else
            while (File.Exists(newLeftId.ToString())) // Verify left ID is unique
                newLeftId = _rand.NextInt64(long.MaxValue);

        // Verify right ID is unique
        while (File.Exists(newRightId.ToString()))
            newRightId = _rand.NextInt64(long.MaxValue);

        // Create the new nodes.
        BTreeNode lowerHalf = new BTreeNode(newLeftId, true, lowerRecords.ToArray(), null);
        BTreeNode upperHalf = new BTreeNode(newRightId, true, upperRecords.ToArray(), null);

        // Create the references to be placed on the parent node
        BTreeNodeReference lowerReference = lowerHalf.GetSelfReference();
        BTreeNodeReference upperReference = upperHalf.GetSelfReference();

        if (node.Id == 0)
        {
            List<BTreeNodeReference> childrenRef = new List<BTreeNodeReference>();

            childrenRef.Add(lowerReference);
            childrenRef.Add(upperReference);

            BTreeNode newRoot = new BTreeNode(0, false, null, childrenRef);

            PutOnCache(newRoot);
            PutOnCache(lowerHalf);
            PutOnCache(upperHalf);

            return;
        }

        BTreeNode parentNode = FindParentNode(_cache[0], node.GetSelfReference());

        BTreeNode newParentNode =
            parentNode.WithSplitReference(node.GetSelfReference(), lowerReference, upperReference);

        PutOnCache(newParentNode);
        PutOnCache(lowerHalf);
        PutOnCache(upperHalf);
    }

    public void SplitInternalRoot(BTreeNode node)
    {
        // Find midpoint of Children References
        int midPoint = node.ChildrenRef.Count / 2;

        // Initialize BTreeNodeReference lists for the new nodes
        List<BTreeNodeReference> lowerChildrenReferences = new List<BTreeNodeReference>();
        List<BTreeNodeReference> upperChildrenReferences = new List<BTreeNodeReference>();

        // Transfer first half to lowerChildrenReferences
        for (int i = 0; i < midPoint; i++)
            lowerChildrenReferences.Add(node.ChildrenRef[i]);

        // Transfer second half to upperChildrenReferences
        for (int i = midPoint; i < node.ChildrenRef.Count; i++)
            upperChildrenReferences.Add(node.ChildrenRef[i]);

        // Create random IDs for left and right nodes
        long newLeftId = _rand.NextInt64(long.MaxValue);
        long newRightId = _rand.NextInt64(long.MaxValue);

        // Verify IDs are unique
        while (File.Exists(newLeftId.ToString())) // Verify left ID is unique
            newLeftId = _rand.NextInt64(long.MaxValue);
        while (File.Exists(newRightId.ToString()))
            newRightId = _rand.NextInt64(long.MaxValue);

        // Create the new nodes.
        BTreeNode lowerHalf = new BTreeNode(newLeftId, false, null, lowerChildrenReferences);
        BTreeNode upperHalf = new BTreeNode(newRightId, false, null, upperChildrenReferences);

        // Create the references to be placed on the parent node
        BTreeNodeReference lowerReference = lowerHalf.GetSelfReference();
        BTreeNodeReference upperReference = upperHalf.GetSelfReference();

        List<BTreeNodeReference> rootRefs = new List<BTreeNodeReference>();
        rootRefs.Add(lowerReference);
        rootRefs.Add(upperReference);

        BTreeNode newRoot = new BTreeNode(0, false, null, rootRefs);

        PutOnCache(newRoot);
        PutOnCache(lowerHalf);
        PutOnCache(upperHalf);
    }

    public void SplitInternalNonRoot(BTreeNode node)
    {
        // Find midpoint of Children References
        int midPoint = node.ChildrenRef.Count / 2;

        // Initialize BTreeNodeReference lists for the new nodes
        List<BTreeNodeReference> lowerChildrenReferences = new List<BTreeNodeReference>();
        List<BTreeNodeReference> upperChildrenReferences = new List<BTreeNodeReference>();

        // Transfer first half to lowerChildrenReferences
        for (int i = 0; i < midPoint; i++)
            lowerChildrenReferences.Add(node.ChildrenRef[i]);

        // Transfer second half to upperChildrenReferences
        for (int i = midPoint; i < node.ChildrenRef.Count; i++)
            upperChildrenReferences.Add(node.ChildrenRef[i]);

        // Create random IDs for left and right nodes
        long newLeftId = node.Id;
        long newRightId = _rand.NextInt64(long.MaxValue);

        // Verify ID is unique
        while (File.Exists(newRightId.ToString()))
            newRightId = _rand.NextInt64(long.MaxValue);

        // Create the new nodes.
        BTreeNode lowerHalf = new BTreeNode(newLeftId, false, null, lowerChildrenReferences);
        BTreeNode upperHalf = new BTreeNode(newRightId, false, null, upperChildrenReferences);

        // Create the references to be placed on the parent node
        BTreeNodeReference lowerReference = lowerHalf.GetSelfReference();
        BTreeNodeReference upperReference = upperHalf.GetSelfReference();

        BTreeNode parentNode = FindParentNode(_cache[0], node.GetSelfReference());

        BTreeNode newParentNode =
            parentNode.WithSplitReference(node.GetSelfReference(), lowerReference, upperReference);

        PutOnCache(newParentNode);
        PutOnCache(lowerHalf);
        PutOnCache(upperHalf);
    }

    private BTreeNode FindParentNode(BTreeNode node, BTreeNodeReference targetRef)
    {
        BTreeNodeReference reference = node.ClosestReferenceInNode(targetRef);

        if (reference == targetRef)
            return node;

        BTreeNode childNode = ReadNode(reference.ChildId);

        return FindParentNode(childNode, targetRef);
    }

    private void PutOnCache(BTreeNode node)
    {
        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);
        _cache.Add(node.Id, node);
    }

    private int GetIndexFromKey(RecordKey key, Record[] list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(list, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    public IReadOnlyList<Record>? List()
    {
        RecordKey dummyKey = new RecordKey(_accountId, DateTime.MinValue, 0);
        return CollectRecordsByAccountId(dummyKey, ReadNode(0), new List<Record>());
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
                BTreeNode childNode = ReadNode(childRef.ChildId);
                result = CollectRecordsByAccountId(key, childNode, list);
            }
        }

        return result;

        /*

        List<Record> result = list;

        if (node.IsLeaf)
        {
            Record dummyRecord = new Record(key, "", 0);
            int index = Array.BinarySearch(node.Records, 0, node.Records.Length, dummyRecord, ByKeyRecordComparer.Instance);

            if (index < 0)
                index = ~index;

            for (int i = index; i < node.Records.Length; i++)
            {
                Record currentRecord = node.Records[i];
                if (currentRecord.Key.AccountId == key.AccountId)
                    result.Add(currentRecord);
                else
                    break;
            }
        }
        else
        {
            List<BTreeNodeReference> viableReferences = node.MatchingReferences(key);

            if (viableReferences == null)
                return result;

            foreach (BTreeNodeReference reference in viableReferences)
            {
                BTreeNode childNode = ReadNode(reference.ChildId);
                result = CollectRecordsByAccountId(key, childNode, result);

            }
        }

        return result;
         */
    }


    public decimal GetBalance(RecordKey key)
    {
        return CollectBalanceByAccountId(key, ReadNode(0), 0);
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
                if (key >= childRef.FirstKey)
                {
                    BTreeNode childNode = ReadNode(childRef.ChildId);
                    result = CollectBalanceByAccountId(key, childNode, result);
                }
                else
                    return result;
            }
        }
        return result;
    }

    public int RecordCount()
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(RecordKey key, BTreeNode node)
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(RecordKey key)
    {
        
        if (_cache.Count == 0)
            return false;
        
        return ContainsKeyInternal(key, ReadNode(0));
    }

    private bool ContainsKeyInternal(RecordKey key, BTreeNode node)
    {
        if (node.IsLeaf)
            return ContainsKeyInRecords(key, node.Records);

        int childRefIndex = node.FindChildReference(key);

        if (childRefIndex < 0)
            return false;

        BTreeNodeReference childRef = node.ChildrenRef[childRefIndex];

        BTreeNode childNode = ReadNode(childRef.ChildId);

        return ContainsKeyInternal(key, childNode);
    }

    public bool ContainsKeyInRecords(RecordKey key, Record[] list)
    {
        int index = GetIndexFromKey(key, list);
        if (index >= 0)
            return true;
        return false;
    }

    public Record Read(RecordKey key)
    {
        var root = ReadNode(0);
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

        if (childIndex < 0) // If reference is not found, there is no record to read
            throw new Exception("Record does not exist.");
        selectedChild = node.ChildrenRef[childIndex];

        BTreeNode childNode = ReadNode(selectedChild.ChildId);

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

    public int CacheLength()
    {
        return _cache.Count;
    }

    private BTreeNode ReadNode(long nodeId)
    {
        if (_cache.ContainsKey(nodeId))
        {
            return _cache[nodeId];
        }

        string fileName = $"{nodeId}.json";

        if (!File.Exists(fileName))
            return null;
        string fileContent = File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<BTreeNode>(fileContent);
    }

    public RecordKey AdjustKey(RecordKey key)
    {
        throw new NotImplementedException();
    }
    
}