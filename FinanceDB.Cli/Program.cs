using System.Net.Http.Headers;
using FinanceDB.Cli;
using FinanceDB.Core.Interfaces;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables("FINANCEDB_")
    .Build();

var baseUrl = config["Server:BaseUrl"] ?? "http://localhost:5000";

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(30)
};
httpClient.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json"));

IRecordStorage database = new HttpRecordStorage(httpClient);

Console.WriteLine($"FinanceDB CLI - Connected to server at {baseUrl}");
Console.WriteLine("Type 'help' for available commands.");
Console.WriteLine();

ParseCommand parser = new ParseCommand(database);

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
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"Server error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
