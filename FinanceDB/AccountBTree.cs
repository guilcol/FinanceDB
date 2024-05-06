
using System.ComponentModel.Design;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace FinanceDB;

public class AccountBTree
{
    private readonly string _accountId;
    private readonly Random _rand;
    public static int Degree;
    private readonly Dictionary<long, BTreeNode> _cache = new();
    private readonly string path;
    public AccountBTree(Random rand, int deg, string accountId)
    {
        _rand = rand;
        Degree = deg;
        _accountId = accountId;
        path = @"C:\\Users\\guilc\\RiderProjects\\FinanceDB\\FinanceDB\\Nodes\\" + accountId + @"\\";
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
        CreateNodeFile();
    }

    private void CreateNodeFile()
    {
        foreach (var item in _cache)
        {
            long nodeId = item.Key;
            BTreeNode node = item.Value;
            string fileName = nodeId + ".json";
            string filePath = path + fileName;
            if (!File.Exists(filePath))
            {
                Directory.CreateDirectory(path);
                string nodeToJson = JsonConvert.SerializeObject(node);
                File.WriteAllText(filePath, nodeToJson);
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
            root = new BTreeNode(0, true, new[] { record }, null, record.GetAmount());
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
                newId = _rand.NextInt64(long.MaxValue);
                while (_cache.ContainsKey(newId))
                    newId = _rand.NextInt64(long.MaxValue);
            }

            // Copy records into the new segment
            Record[] segment = new Record[segmentLength];
            Array.Copy(node.Records, start, segment, 0, segmentLength);

            // Calculate balance for the segment
            decimal segmentBalance = segment.Sum(record => record.GetAmount());

            // Store new node
            newNodes[segmentIndex] = new BTreeNode(newId, true, segment, null, segmentBalance);
            newReferences[segmentIndex] = newNodes[segmentIndex].GetSelfReference();
            PutOnCache(newNodes[segmentIndex]);
        }

        if (node.Id == 0)
        {
            BTreeNode newRoot = new BTreeNode(0, false, null, newReferences, node.GetAmount());
            PutOnCache(newRoot);
        }
        else
        {
            BTreeNode parentNode = FindParentNode(_cache[0], node.GetSelfReference());
            BTreeNode newParentNode = parentNode.WithNewReferences(node.GetSelfReference(), newReferences);
            PutOnCache(newParentNode);
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

            long newId = _rand.NextInt64(long.MaxValue);
            while (_cache.ContainsKey(newId))
                newId = _rand.NextInt64(long.MaxValue);
            
            // Copy references into the new segment
            BTreeNodeReference[] segment = new BTreeNodeReference[segmentLength];
            Array.Copy(node.ChildrenRef, start, segment, 0, segmentLength);

            // Calculate balance for the segment
            decimal segmentBalance = segment.Sum(reference => reference.GetAmount());

            // Store new node
            newNodes[segmentIndex] = new BTreeNode(newId, false, null, segment, segmentBalance);
            newReferences[segmentIndex] = newNodes[segmentIndex].GetSelfReference();
            PutOnCache(newNodes[segmentIndex]);
        }

        if (node.Id == 0)
        {
            BTreeNode newRoot = new BTreeNode(0, false, null, newReferences, node.GetAmount());
            PutOnCache(newRoot);
        }
        else
        {
            BTreeNode parentNode = FindParentNode(_cache[0], node.GetSelfReference());
            BTreeNode newParentNode = parentNode.WithNewReferences(node.GetSelfReference(), newReferences);
            PutOnCache(newParentNode);
        }
    }

    private BTreeNode FindParentNode(BTreeNode nodeTravel, BTreeNodeReference targetRef)
    {
        BTreeNodeReference reference = nodeTravel.ClosestReferenceInNode(targetRef);

        if (reference == targetRef)
            return nodeTravel;

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
    }

    public decimal GetBalance()
    {
        return ReadNode(0).GetAmount();
    }

    public decimal GetBalance(RecordKey key)
    {
        return CollectBalanceByAccountId(key, ReadNode(0), 0);
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
                    BTreeNode childNode = ReadNode(childRef.ChildId);
                    return CollectBalanceByAccountId(key, childNode, result);
                }
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
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            return null;
        string fileContent = File.ReadAllText(fullPath);
        BTreeNode nodeFromFile = JsonConvert.DeserializeObject<BTreeNode>(fileContent);
        PutOnCache(nodeFromFile);
        return nodeFromFile;
    }

    public RecordKey AdjustKey(RecordKey key)
    {
        throw new NotImplementedException();
    }
}