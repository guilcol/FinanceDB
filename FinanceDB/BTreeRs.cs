using System.Data.Common;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace FinanceDB;
public class BTreeRs : IRecordStorage
{
    private readonly Dictionary<long, BTreeNode> cache = new Dictionary<long, BTreeNode>();
    
    public void Save()
    {
        throw new NotImplementedException();
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
            List<Record> newList = new List<Record>();
            newList.Add(record);
            root = new BTreeNode(0, true, newList, new List<BTreeNodeReference>());
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
            (selectedChild, childIndex) = node.SelectChildReference(~childIndex);
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

    public void SplitChild(BTreeNode node)
    {
        if (node.IsLeaf) // if node is leaf
        {
            // Find midpoint of Records
            int midPoint = node.Records.Count / 2;

            // Initialize Record lists for the new nodes
            List<Record> lowerRecords = new List<Record>();
            List<Record> upperRecords = new List<Record>();

            // Transfer first half to lowerRecords
            for (int i = 0; i < midPoint; i++)
                lowerRecords.Add(node.Records[i]);
            
            // Transfer second half to upperRecords
            for (int i = midPoint; i < node.Records.Count; i++)
                upperRecords.Add(node.Records[i]);

            // Variable to hold the new Id for the new nodes
            long leftNodeId = node.Id;
            long rightNodeId = cache.Keys.Max() + 1; //todo: random long ID and check if file exists
            
            // In case the node to be split is the root, we cannot repurpose the '0' Id,
            // because root is the only node that needs to have a specific Id.
            if (node.Id == 0)
            {
                leftNodeId = cache.Keys.Max() + 1; //todo: fix id
                rightNodeId = leftNodeId + 1;
            }
            
            // Create the new nodes.
            BTreeNode lowerHalf = new BTreeNode(leftNodeId, true, lowerRecords, null);
            BTreeNode upperHalf = new BTreeNode(rightNodeId, true, upperRecords, null);

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
            
            BTreeNode parentNode = FindParentNode(cache[0], node.GetSelfReference());

            BTreeNode newParentNode = parentNode.WithSplitReference(node.GetSelfReference(), lowerReference, upperReference);
        }
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
        cache.Add(node.Id, node);
    }

    public bool InsertOnList(Record record, List<Record> list)
    {
        int index = GetIndexFromKey(record.Key, list);
        if (index >= 0)
            return false;
        index = ~index;
        list.Insert(index, record);
        return true;
    }
    
    private int GetIndexFromKey(RecordKey key, List<Record> list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = list.BinarySearch(dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }
    bool IRecordStorage.Delete(Record record)
    {
        throw new NotImplementedException();
    }

    public bool Delete(RecordKey key)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<Record> List(string accountId)
    {
        throw new NotImplementedException();
    }

    public decimal GetBalance(string accountId)
    {
        throw new NotImplementedException();
    }

    public int RecordCount()
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(RecordKey key)
    {
        return false;
    }

    public bool ContainsKeyInRecords(RecordKey key, List<Record> list)
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

    public Record? ReadFromRecords(List<Record> currentNodeRecords, RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = currentNodeRecords.BinarySearch(dummyRecord, ByKeyRecordComparer.Instance);
        if (index > 0) 
            return currentNodeRecords[index];
        return null;
    }

    private BTreeNode ReadNode(long nodeId)
    {
        if (cache.ContainsKey(nodeId))
        {
            return cache[nodeId];
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