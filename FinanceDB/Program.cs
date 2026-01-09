using FinanceDB;

const int BTreeDegree = 100;
Random rand = new Random();

BTreeRs database = new BTreeRs(rand, BTreeDegree);
database.Load();

ParseCommand parser = new ParseCommand(database);

Console.WriteLine("FinanceDB - Financial Transaction Database");
Console.WriteLine("Type 'help' for available commands.");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    try
    {
        parser.Execute(input);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}