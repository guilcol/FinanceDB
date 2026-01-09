namespace FinanceDB;

public class Insert : Command
{
    private Record _record;
    private readonly IRecordStorage _database;

    public Insert(Record record, IRecordStorage db)
    {
        _record = record;
        _database = db;
    }

    public override bool Execute()
    {
        RecordKey key = _database.AdjustKey(_record.Key);
        if (key != _record.Key)
        {
            _record = new Record(key, _record.GetDescription(), _record.GetAmount());
        }
        return _database.Insert(_record);
    }
}