using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDB;

public class ParseCommand
{
    private string _cmd;
    private BasicRs database;
    public ParseCommand(string tokens, BasicRs db)
    {
        database = db; 
        string[] parsedInput = tokens.Split(" ", 2);
        Command cmd;
        switch (parsedInput[0])
        {
            case "insert":
                cmd = new Insert(ValidateCommand(parsedInput[1]), database); 
                break;
            case "update":
                cmd = new Update(ValidateCommand(parsedInput[1]), database);
                break;
            case "delete":
                cmd = new Delete(ValidateCommand(parsedInput[1]), database);
                break;
            default:
                throw new Exception("Unsupported command.");
        }
        cmd.Execute();
        
    }

    public Record ValidateCommand(string tokens)
    {
        string pattern = @"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z) \""(.+?)\"" (\d+\.\d+)";
        Regex regex = new Regex(pattern);
        Match match = regex.Match(tokens);
        if (match.Success)
        {
            // Extracting DateTime
            DateTime date = DateTime.Parse(match.Groups[1].Value, null, DateTimeStyles.AdjustToUniversal);
                
            // Extracting the string description (without quotes)
            string description = match.Groups[2].Value;
                
            // Extracting the decimal amount
            decimal amount = decimal.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            RecordKey key = new RecordKey("", date, 0);
            return new Record(key, description, amount);
        }
        throw new Exception("Invalid syntax");
    }
    
}