namespace FinanceDB;

public class Save : Command
{
    private readonly IRecordStorage _database;

    public Save(IRecordStorage db)
    {
        _database = db;
    }

    public override bool Execute()
    {
        _database.Save();
        return true;
    }
}