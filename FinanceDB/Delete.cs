using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDB;

public class Delete : Command
{
    private Record _record;
    private BasicRs _database;

    public Delete(Record record, BasicRs db)
    {
        _record = record;
        _database = db;
    }

    public override void Execute()
    {
        throw new NotImplementedException();
    }

}