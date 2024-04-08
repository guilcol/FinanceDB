namespace FinanceDB;

public interface IRecordStorage
{
    public void Save();

    public void Load();

    public bool Insert(Record record);

    public bool Update(Record record);
    
    public bool Delete(Record record);
    
    public bool Delete(RecordKey key);

    public IReadOnlyList<Record>? List(string accountId);

    public decimal GetBalance(string accountId, RecordKey key);

    public int RecordCount();

    public bool ContainsKey(RecordKey key);

    public Record? Read(RecordKey key);

    public RecordKey AdjustKey(RecordKey key);
    
}