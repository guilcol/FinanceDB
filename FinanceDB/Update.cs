using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDB;

public class Update : Command
{
    private readonly Record _record;
    private readonly BasicRs _database;
    
    public Update(Record record, BasicRs db)
    {
        _database = db;
        _record = record;
    }
    public override void Execute()
    {
        if (_record != null)
        {
            _database.Delete(_record.Key);
            _database.Insert(_record);
        }
    }
    
}