namespace FinanceDB;

public sealed class BTreeNode
{
    public long Id { get; }
    public bool IsLeaf { get; }
    public List<Record>? Records { get; }
    public List<BTreeNodeReference>? ChildrenRef { get; }

    public BTreeNode(long id, bool isLeaf, List<Record>? records, List<BTreeNodeReference>? childrenRef)
    {
        Id = id;
        IsLeaf = isLeaf;
        Records = records;
        ChildrenRef = childrenRef;
    }

    public bool isFull()
    {
        if (Records == null)
            return ChildrenRef.Count < BTreeRs.degree;
        return Records.Count < BTreeRs.degree; //TODO: Fix isFull() return true for any value under DEGREE
    }

    public bool isOverflowing()
    {
        if (Records == null)
            return ChildrenRef.Count > BTreeRs.degree;
        return Records.Count > BTreeRs.degree;
    }

    
    public BTreeNode WithNewRecord(int index, Record record)
    {
        
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        List<Record> records = new List<Record>(Records);

        records.Insert(index, record);

        return new BTreeNode(Id, true, records, null);
    }
    
    public BTreeNode WithDeletedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        List<Record> records = new List<Record>(Records);

        records.RemoveAt(index);

        return new BTreeNode(Id, true, records, null);
    }
    
    public BTreeNode WithUpdatedRecord(int index, Record record)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on leaf node. Node {Id} is not a leaf.");

        List<Record> records = new List<Record>(Records);

        records[index] = record;

        return new BTreeNode(Id, true, records, null);
    }

    public BTreeNode WithModifiedReference(int childIndex, BTreeNodeReference newChildRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on internal nodes. Node {Id} is a leaf.");

        List<BTreeNodeReference> childrenRef = new List<BTreeNodeReference>(ChildrenRef);
        childrenRef[childIndex] = newChildRef;

        return new BTreeNode(Id, false, null, childrenRef);
    }
    
    public int GetIndexFromKey(RecordKey recordKey)
    {
        Record dummyRecord = new Record(recordKey, "", 0);
        int index = Records.BinarySearch(dummyRecord, ByKeyRecordComparer.Instance);
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
            return new BTreeNodeReference(Records[0].Key, Records[Records.Count - 1].Key, Id);
        return new BTreeNodeReference(ChildrenRef[0].FirstKey, ChildrenRef[ChildrenRef.Count - 1].LastKey, Id);
    }

    public BTreeNode WithSplitReference(BTreeNodeReference oldRef, BTreeNodeReference leftRef, BTreeNodeReference rightRef)
    {
        if (IsLeaf)
            throw new InvalidOperationException($"Operation is only allowed on internal node. Node {Id} is not internal.");
        
        List<BTreeNodeReference> newChildrenRef = new List<BTreeNodeReference>(ChildrenRef);

        int index = newChildrenRef.BinarySearch(oldRef);
        
        newChildrenRef.RemoveAt(index);
        
        newChildrenRef.Insert(index, leftRef);
        newChildrenRef.Insert(index + 1, rightRef);
        
        return new BTreeNode(Id, false, null, newChildrenRef);
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
}