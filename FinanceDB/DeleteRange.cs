namespace FinanceDB;

public class DeleteRange : Command
{
    private readonly RecordKey _startKey;
    private readonly RecordKey _endKey;
    private readonly IRecordStorage _database;
    private int _deletedCount;

    public DeleteRange(RecordKey startKey, RecordKey endKey, IRecordStorage db)
    {
        _startKey = startKey;
        _endKey = endKey;
        _database = db;
    }

    public override bool Execute()
    {
        _deletedCount = _database.DeleteRange(_startKey, _endKey);
        return _deletedCount > 0;
    }

    public int DeletedCount => _deletedCount;
}
