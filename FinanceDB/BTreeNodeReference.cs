namespace FinanceDB;

public class BTreeNodeReference
{
    public readonly  RecordKey FirstKey;
    public readonly  RecordKey LastKey;
    public readonly long ChildId;

    public BTreeNodeReference(RecordKey firstKey, RecordKey lastKey, long childId)
    {
        FirstKey = firstKey;
        LastKey = lastKey;
        ChildId = childId;
    }
    
    
}