namespace FinanceDB;

public class Load : Command
{
    private readonly IRecordStorage _database;

    public Load(IRecordStorage db)
    {
        _database = db;
    }

    public override bool Execute()
    {
        _database.Load();
        return true;
    }
}