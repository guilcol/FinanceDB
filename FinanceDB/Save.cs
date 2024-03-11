namespace FinanceDB;

public class Save : Command
{
    private BasicRs database;

    public Save(BasicRs db)
    {
        database = db;
    }

    public override void Execute()
    {
        throw new NotImplementedException();
    }
}