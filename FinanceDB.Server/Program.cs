using System.Collections.Concurrent;
using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Storage;
using FinanceDB.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var bTreeDegree = builder.Configuration.GetValue<int>("BTree:Degree", 100);

// Register BTreeRs as singleton
builder.Services.AddSingleton<Random>();
builder.Services.AddSingleton<IRecordStorage>(sp =>
{
    var rand = sp.GetRequiredService<Random>();
    var db = new BTreeRs(rand, bTreeDegree);
    db.Load();
    return db;
});

// Per-accountId locking - ASP.NET thread pool handles queuing
builder.Services.AddSingleton(new ConcurrentDictionary<string, object>());

// Global lock for system operations (Save/Load)
builder.Services.AddSingleton(new GlobalLock());

var app = builder.Build();

// Map endpoints
app.MapRecordEndpoints();
app.MapAccountEndpoints();
app.MapSystemEndpoints();

app.Run();

// Class definitions must come after all top-level statements
public class GlobalLock
{
    public object Lock { get; } = new object();
}

// Make Program class accessible for integration tests
public partial class Program { }
