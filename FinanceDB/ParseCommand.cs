using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDB;

public class ParseCommand
{
    private readonly IRecordStorage _database;

    public ParseCommand(IRecordStorage db)
    {
        _database = db;
    }

    public void Execute(string input)
    {
        string[] parsedInput = input.Split(" ", 2);
        string commandName = parsedInput[0].ToLower();

        switch (commandName)
        {
            case "insert":
                ExecuteInsert(parsedInput);
                break;
            case "update":
                ExecuteUpdate(parsedInput);
                break;
            case "delete":
                ExecuteDelete(parsedInput);
                break;
            case "delete_range":
                ExecuteDeleteRange(parsedInput);
                break;
            case "list":
                ListRecords(parsedInput.Length > 1 ? parsedInput[1].Trim() : null);
                break;
            case "list_range":
                ExecuteListRange(parsedInput);
                break;
            case "balance":
                ShowBalance(parsedInput.Length > 1 ? parsedInput[1].Trim() : null);
                break;
            case "save":
                _database.Save();
                Console.WriteLine("Database saved.");
                break;
            case "exit":
            case "quit":
                _database.Save();
                Console.WriteLine("Database saved. Goodbye!");
                Environment.Exit(0);
                break;
            case "help":
                ShowHelp();
                break;
            default:
                Console.WriteLine($"Unknown command: {commandName}. Type 'help' for available commands.");
                break;
        }
    }

    private void ExecuteInsert(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: insert <accountId> [<datetime>] \"<description>\" <amount>");
            return;
        }

        Record record = ParseRecord(parsedInput[1]);
        bool success = new Insert(record, _database).Execute();

        if (success)
            Console.WriteLine($"Record inserted into account '{record.Key.AccountId}'.");
        else
            Console.WriteLine("Failed to insert record. A record with this key may already exist.");
    }

    private void ExecuteUpdate(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: update <accountId> <datetime> <sequence> [description='<text>'] [amount=<number>]");
            return;
        }

        // Pattern: accountId datetime sequence [description='...'] [amount=...]
        string pattern = @"^(\S+)\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)(.*)$";
        Match match = Regex.Match(parsedInput[1], pattern);

        if (!match.Success)
        {
            Console.WriteLine("Invalid syntax. Usage: update <accountId> <datetime> <sequence> [description='<text>'] [amount=<number>]");
            return;
        }

        string accountId = match.Groups[1].Value;
        DateTime date = DateTime.Parse(match.Groups[2].Value, null, DateTimeStyles.AdjustToUniversal);
        uint sequence = uint.Parse(match.Groups[3].Value);
        string updatesPart = match.Groups[4].Value.Trim();

        RecordKey key = new RecordKey(accountId, date, sequence);
        Record? existingRecord = _database.Read(key);

        if (existingRecord == null)
        {
            Console.WriteLine($"Record not found with key: {accountId} {date:O} {sequence}");
            return;
        }

        // Parse optional updates
        string? newDescription = null;
        decimal? newAmount = null;

        // Match description='...'
        Match descMatch = Regex.Match(updatesPart, @"description='([^']*)'");
        if (descMatch.Success)
        {
            newDescription = descMatch.Groups[1].Value;
        }

        // Match amount=...
        Match amountMatch = Regex.Match(updatesPart, @"amount=(-?\d+(?:\.\d+)?)");
        if (amountMatch.Success)
        {
            newAmount = decimal.Parse(amountMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        if (newDescription == null && newAmount == null)
        {
            Console.WriteLine("No updates specified. Use description='<text>' and/or amount=<number>");
            return;
        }

        // Apply updates
        Record updatedRecord = new Record(
            key,
            newDescription ?? existingRecord.Description,
            newAmount ?? existingRecord.Amount
        );

        bool success = new Update(updatedRecord, _database).Execute();

        if (success)
            Console.WriteLine($"Record updated in account '{accountId}'.");
        else
            Console.WriteLine("Failed to update record.");
    }

    private void ExecuteDelete(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: delete <accountId> <datetime> <sequence>");
            return;
        }

        // Pattern: accountId datetime sequence
        string pattern = @"^(\S+)\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)$";
        Match match = Regex.Match(parsedInput[1], pattern);

        if (!match.Success)
        {
            Console.WriteLine("Invalid syntax. Usage: delete <accountId> <datetime> <sequence>");
            return;
        }

        string accountId = match.Groups[1].Value;
        DateTime date = DateTime.Parse(match.Groups[2].Value, null, DateTimeStyles.AdjustToUniversal);
        uint sequence = uint.Parse(match.Groups[3].Value);

        RecordKey key = new RecordKey(accountId, date, sequence);
        bool success = _database.Delete(key);

        if (success)
            Console.WriteLine($"Record deleted from account '{accountId}'.");
        else
            Console.WriteLine("Failed to delete record. Record not found.");
    }

    private void ExecuteDeleteRange(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: delete_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
            return;
        }

        // Pattern: accountId from datetime sequence to datetime sequence
        string pattern = @"^(\S+)\s+from\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)\s+to\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)$";
        Match match = Regex.Match(parsedInput[1], pattern);

        if (!match.Success)
        {
            Console.WriteLine("Invalid syntax. Usage: delete_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
            return;
        }

        string accountId = match.Groups[1].Value;
        DateTime startDate = DateTime.Parse(match.Groups[2].Value, null, DateTimeStyles.AdjustToUniversal);
        uint startSequence = uint.Parse(match.Groups[3].Value);
        DateTime endDate = DateTime.Parse(match.Groups[4].Value, null, DateTimeStyles.AdjustToUniversal);
        uint endSequence = uint.Parse(match.Groups[5].Value);

        RecordKey startKey = new RecordKey(accountId, startDate, startSequence);
        RecordKey endKey = new RecordKey(accountId, endDate, endSequence);

        var command = new DeleteRange(startKey, endKey, _database);
        command.Execute();

        if (command.DeletedCount > 0)
            Console.WriteLine($"{command.DeletedCount} record(s) deleted from account '{accountId}'.");
        else
            Console.WriteLine("No records found in the specified range.");
    }

    private void ExecuteListRange(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: list_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
            return;
        }

        // Pattern: accountId from datetime sequence to datetime sequence
        string pattern = @"^(\S+)\s+from\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)\s+to\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(\d+)$";
        Match match = Regex.Match(parsedInput[1], pattern);

        if (!match.Success)
        {
            Console.WriteLine("Invalid syntax. Usage: list_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
            return;
        }

        string accountId = match.Groups[1].Value;
        DateTime startDate = DateTime.Parse(match.Groups[2].Value, null, DateTimeStyles.AdjustToUniversal);
        uint startSequence = uint.Parse(match.Groups[3].Value);
        DateTime endDate = DateTime.Parse(match.Groups[4].Value, null, DateTimeStyles.AdjustToUniversal);
        uint endSequence = uint.Parse(match.Groups[5].Value);

        RecordKey startKey = new RecordKey(accountId, startDate, startSequence);
        RecordKey endKey = new RecordKey(accountId, endDate, endSequence);

        var records = _database.ListRange(startKey, endKey);

        if (records == null || records.Count == 0)
        {
            Console.WriteLine("No records found in the specified range.");
            return;
        }

        Console.WriteLine($"Records for account '{accountId}' in range:");
        foreach (var record in records)
        {
            Console.WriteLine($"  {record.Key.AccountId} {record.Key.Date:O} {record.Key.Sequence} | {record.GetDescription(),-30} | {record.GetAmount():C}");
        }
        Console.WriteLine($"Total: {records.Count} record(s)");
    }

    private Record ParseRecord(string tokens)
    {
        // Pattern with datetime: accountId datetime "description" amount
        // Example: checking 2024-01-15T10:30:00.000Z "Coffee purchase" 5.50
        string patternWithDate = @"^(\S+)\s+(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+""(.+?)""\s+(-?\d+(?:\.\d+)?)$";
        Match match = Regex.Match(tokens, patternWithDate);

        if (match.Success)
        {
            string accountId = match.Groups[1].Value;
            DateTime date = DateTime.Parse(match.Groups[2].Value, null, DateTimeStyles.AdjustToUniversal);
            string description = match.Groups[3].Value;
            decimal amount = decimal.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

            RecordKey key = new RecordKey(accountId, date, 0);
            return new Record(key, description, amount);
        }

        // Pattern without datetime: accountId "description" amount
        // Example: checking "Coffee purchase" 5.50
        string patternNoDate = @"^(\S+)\s+""(.+?)""\s+(-?\d+(?:\.\d+)?)$";
        match = Regex.Match(tokens, patternNoDate);

        if (match.Success)
        {
            string accountId = match.Groups[1].Value;
            DateTime date = DateTime.UtcNow;
            string description = match.Groups[2].Value;
            decimal amount = decimal.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            RecordKey key = new RecordKey(accountId, date, 0);
            return new Record(key, description, amount);
        }

        throw new ArgumentException("Invalid syntax. Expected: <command> <accountId> [<datetime>] \"<description>\" <amount>");
    }

    private void ListRecords(string? accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            Console.WriteLine("Usage: list <accountId>");
            return;
        }

        var records = _database.List(accountId);
        if (records == null || records.Count == 0)
        {
            Console.WriteLine($"No records found for account '{accountId}'.");
            return;
        }

        Console.WriteLine($"Records for account '{accountId}':");
        foreach (var record in records)
        {
            Console.WriteLine($"  {record.Key.AccountId} {record.Key.Date:O} {record.Key.Sequence} | {record.GetDescription(),-30} | {record.GetAmount():C}");
        }
        Console.WriteLine($"Total: {records.Count} record(s)");
    }

    private void ShowBalance(string? accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            Console.WriteLine("Usage: balance <accountId>");
            return;
        }

        try
        {
            decimal balance = _database.GetBalance(accountId, new RecordKey(accountId, DateTime.MaxValue, uint.MaxValue));
            Console.WriteLine($"Balance for account '{accountId}': {balance:C}");
        }
        catch
        {
            Console.WriteLine($"Account '{accountId}' not found.");
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  insert <accountId> [<datetime>] \"<description>\" <amount>");
        Console.WriteLine("  update <accountId> <datetime> <sequence> [description='<text>'] [amount=<number>]");
        Console.WriteLine("  delete <accountId> <datetime> <sequence>");
        Console.WriteLine("  delete_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
        Console.WriteLine("  list <accountId>");
        Console.WriteLine("  list_range <accountId> from <start_datetime> <start_sequence> to <end_datetime> <end_sequence>");
        Console.WriteLine("  balance <accountId>");
        Console.WriteLine("  save");
        Console.WriteLine("  exit");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  insert checking 2024-01-15T10:30:00Z \"Coffee purchase\" 5.50");
        Console.WriteLine("  insert checking \"Coffee purchase\" 5.50  (uses current time)");
        Console.WriteLine("  update checking 2024-01-15T10:30:00Z 0 description='Updated description'");
        Console.WriteLine("  update checking 2024-01-15T10:30:00Z 0 amount=10.50");
        Console.WriteLine("  update checking 2024-01-15T10:30:00Z 0 description='New desc' amount=15.00");
        Console.WriteLine("  delete_range checking from 2024-01-01T00:00:00Z 0 to 2024-01-31T23:59:59Z 999");
        Console.WriteLine("  list_range checking from 2024-01-01T00:00:00Z 0 to 2024-01-31T23:59:59Z 999");
    }
}