

namespace FinanceDB;

public class AccountBTree
{
    private readonly string _accountId;
    private readonly Random _rand;
    public static int Degree;
    private readonly INodeStorage _nodes;
    
    public AccountBTree(Random rand, int deg, string accountId)
    {
        _rand = rand;
        Degree = deg;
        _accountId = accountId;
        _nodes = new FileNodeStorage(rand, accountId);
    }

    public void Save()
    {
        bool resetLoop = true;

        while (resetLoop)
        {
            resetLoop = false;
            foreach (var item in _nodes.List())
            {
                if (item.isOverflowing()) // Check if current node is overflowing
                {
                    SplitNode(item); // Split the overflowing node
                    resetLoop = true;
                    break;
                }
            }
        }
        
        _nodes.Save();
    }

    public void Load()
    {
        // Loading is handled lazily by FileNodeStorage.Get()
        // which reads from disk if not in cache
    }

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

    public bool Delete(Record record)
    {
        var root = _nodes.Get(0);

        if (root == null)
            throw new Exception("Null root.");

        (bool Deleted, BTreeNode Node) result = DeleteOnSubtree(root, record);

        return result.Deleted;
    }

    public bool Update(Record record)
    {
        var root = _nodes.Get(0);

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
            _nodes.Put(newNode);
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
        BTreeNode childNode = _nodes.Get(selectedChild.ChildId);
        (bool Inserted, BTreeNode Node) subtreeResult = InsertOnSubtree(childNode, record);

        // If insertion worked, get reference for new node and pass to parent
        if (subtreeResult.Inserted)
        {
            BTreeNodeReference newChildRef = subtreeResult.Node.GetSelfReference();
            BTreeNode newNode = node.WithModifiedReference(childIndex, newChildRef);
            _nodes.Put(newNode);
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
            _nodes.Put(newNode);
            return (Deleted: true, Node: newNode);
        }

        int childIndex = node.FindChildReference(record.Key);
        BTreeNodeReference selectedChild;

        if (childIndex < 0) // If reference is not found, there is no record to delete
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

        if (childIndex < 0) // If reference is not found, there is no record to update
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

    public void SplitNode(BTreeNode node)
    {
        if (node.IsLeaf)
        {
            SplitLeafNode(node);
            return;
        }
        SplitInternalRoot(node);
    }

    private void SplitLeafNode(BTreeNode node)
    {
        // Obtain length of records
        int totalLength = node.Records.Length;
        
        // Calculate the number of segments needed
        int numSegments = totalLength / Degree;

        int capacity = numSegments * Degree;
        if (capacity < totalLength)
            ++numSegments;

        int correctLength = totalLength / numSegments;

        // Initialize arrays of new BTreeNodes
        BTreeNode[] newNodes = new BTreeNode[numSegments];
        BTreeNodeReference[] newReferences = new BTreeNodeReference[numSegments];

        // Loop through all records and split them into chunks of correctLength
        for (int start = 0, segmentIndex = 0; segmentIndex < numSegments; start += correctLength, segmentIndex++)
        {
            int segmentLength;
            
            // Calculate segment length for this iteration
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

            // Copy records into the new segment
            Record[] segment = new Record[segmentLength];
            Array.Copy(node.Records, start, segment, 0, segmentLength);

            // Calculate balance for the segment
            decimal segmentBalance = segment.Sum(record => record.GetAmount());

            // Store new node
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

    public void SplitInternalRoot(BTreeNode node)
    {
        // Obtain length of children references
        int totalLength = node.ChildrenRef.Length;

        // Calculate the number of segments needed
        int numSegments = totalLength / Degree;

        int capacity = numSegments * Degree;
        if (capacity < totalLength)
            ++numSegments;

        int correctLength = totalLength / numSegments;

        // Initialize arrays of new BTreeNodes
        BTreeNode[] newNodes = new BTreeNode[numSegments];
        BTreeNodeReference[] newReferences = new BTreeNodeReference[numSegments];

        // Loop through all references and split them into chunks of correctLength
        for (int start = 0, segmentIndex = 0; segmentIndex < numSegments; start += correctLength, segmentIndex++)
        {
            int segmentLength;

            // Calculate segment length for this iteration
            if (segmentIndex == numSegments - 1)
                segmentLength = totalLength - start;
            else
                segmentLength = correctLength;

            long newId = _nodes.GenerateNodeId();
            
            // Copy references into the new segment
            BTreeNodeReference[] segment = new BTreeNodeReference[segmentLength];
            Array.Copy(node.ChildrenRef, start, segment, 0, segmentLength);

            // Calculate balance for the segment
            decimal segmentBalance = segment.Sum(reference => reference.GetAmount());

            // Store new node
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

    private BTreeNode FindParentNode(BTreeNode nodeTravel, BTreeNodeReference targetRef)
    {
        BTreeNodeReference reference = nodeTravel.ClosestReferenceInNode(targetRef);

        if (reference == targetRef)
            return nodeTravel;

        BTreeNode childNode = _nodes.Get(reference.ChildId);

        return FindParentNode(childNode, targetRef);
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

    public decimal GetBalance()
    {
        return _nodes.Get(0).GetAmount();
    }

    public decimal GetBalance(RecordKey key)
    {
        return CollectBalanceByAccountId(key, _nodes.Get(0), 0);
    }


    public decimal CollectBalanceByAccountId(RecordKey key, BTreeNode node, decimal balance)
    {
        // Variable to store resulting balance
        decimal result = balance;

        if (node.IsLeaf)
        {
            // In case we reach a leaf, iterate through all records and add their balance to result
            for (int i = 0; i < node.Records.Length; i++)
            {
                Record currentRecord = node.Records[i];
                if (currentRecord.Key > key) // If current record is greater than key, we can stop reading them
                    return result;
                result += currentRecord.GetAmount();
            }
        }
        else // In case current node is internal
        {
            // Iterate all references 
            foreach (BTreeNodeReference childRef in node.ChildrenRef)
            {
                // Compare target key with each reference's last key
                if (key > childRef.LastKey)
                {
                    // Add reference's amount to result since key larger than any key inside this reference
                    result += childRef.GetAmount();
                }
                else
                {
                    // In case key is not larger than reference's last key, it must be inside this reference
                    // So we read the reference's child node and recursively call this method with the child node
                    BTreeNode childNode = _nodes.Get(childRef.ChildId);
                    return CollectBalanceByAccountId(key, childNode, result);
                }
            }
        }

        return result;
    }

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

        if (childIndex < 0) // If reference is not found, there is no record to read
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

    public int CacheLength()
    {
        return _nodes.CacheLength();
    }

    public RecordKey AdjustKey(RecordKey key)
    {
        var root = _nodes.Get(0);
        if (root == null)
            return key;

        // Find the highest sequence number for records with the same accountId and date
        uint maxSequence = FindMaxSequenceForDate(root, key.AccountId, key.Date);

        if (maxSequence == 0 && !ContainsKey(key))
            return key;

        return key.WithSequence(maxSequence + 1);
    }

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
}