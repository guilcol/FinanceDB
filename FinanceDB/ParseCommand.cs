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
            case "list":
                ListRecords(parsedInput.Length > 1 ? parsedInput[1].Trim() : null);
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
            Console.WriteLine("Usage: update <accountId> <datetime> \"<description>\" <amount>");
            return;
        }

        Record record = ParseRecord(parsedInput[1]);
        bool success = new Update(record, _database).Execute();

        if (success)
            Console.WriteLine($"Record updated in account '{record.Key.AccountId}'.");
        else
            Console.WriteLine("Failed to update record. Record not found.");
    }

    private void ExecuteDelete(string[] parsedInput)
    {
        if (parsedInput.Length < 2)
        {
            Console.WriteLine("Usage: delete <accountId> <datetime> \"<description>\" <amount>");
            return;
        }

        Record record = ParseRecord(parsedInput[1]);
        bool success = new Delete(record, _database).Execute();

        if (success)
            Console.WriteLine($"Record deleted from account '{record.Key.AccountId}'.");
        else
            Console.WriteLine("Failed to delete record. Record not found.");
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
            Console.WriteLine($"  {record.Key.Date:O} | {record.GetDescription(),-30} | {record.GetAmount():C}");
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
        Console.WriteLine("  update <accountId> <datetime> \"<description>\" <amount>");
        Console.WriteLine("  delete <accountId> <datetime> \"<description>\" <amount>");
        Console.WriteLine("  list <accountId>");
        Console.WriteLine("  balance <accountId>");
        Console.WriteLine("  save");
        Console.WriteLine("  exit");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  insert checking 2024-01-15T10:30:00Z \"Coffee purchase\" 5.50");
        Console.WriteLine("  insert checking \"Coffee purchase\" 5.50  (uses current time)");
    }
}