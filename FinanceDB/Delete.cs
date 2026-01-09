namespace FinanceDB;

public class Delete : Command
{
    private readonly Record _record;
    private readonly IRecordStorage _database;

    public Delete(Record record, IRecordStorage db)
    {
        _record = record;
        _database = db;
    }
    
    public override bool Execute()
    {
        if (_record == null)
            return false;

        return _database.Delete(_record);
    }
}