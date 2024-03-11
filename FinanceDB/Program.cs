using System.Diagnostics;
using System.Resources;
using System.Runtime.InteropServices.JavaScript;
using FinanceDB;

BasicRs database = new BasicRs("database.json");

while (true)
{
    ProcessUserInput(Console.ReadLine());
}

void ProcessUserInput(string input)
{
    ParseCommand cmd = new ParseCommand(input, database);
}