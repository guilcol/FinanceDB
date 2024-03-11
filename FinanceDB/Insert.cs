using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDB;

public class Insert : Command
{
    private Record _record;
    private readonly BasicRs _database;

    public Insert(Record record, BasicRs db)
    {
        _record = record;
        _database = db;
    }
    
    public override void Execute()
    {
        RecordKey key = _database.AdjustKey(_record.Key);
        if (key != _record.Key)
        {
            _record = new Record(key, _record.GetDescription(), _record.GetAmount());
        }
        _database.Insert(_record);
    }
    
    
}