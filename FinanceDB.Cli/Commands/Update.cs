using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Cli.Commands;

public class Update : Command
{
    private readonly Record _record;
    private readonly IRecordStorage _database;

    public Update(Record record, IRecordStorage db)
    {
        _database = db;
        _record = record;
    }

    public override bool Execute()
    {
        if (_record == null)
            return false;

        return _database.Update(_record);
    }
}
