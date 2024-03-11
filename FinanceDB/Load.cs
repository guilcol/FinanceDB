namespace FinanceDB;

public class Load : Command
{
    public List<Record> database;

    public Load(List<Record> db)
    {
        database = db;
    }
    public override void Execute()
    {
 
    }
}