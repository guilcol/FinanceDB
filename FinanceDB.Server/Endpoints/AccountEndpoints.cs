using System.Collections.Concurrent;
using System.Web;
using FinanceDB.Core.Dto;
using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Server.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        // GET /accounts/{accountId}/balance?asOf={date},{sequence}
        app.MapGet("/accounts/{accountId}/balance", (
            string accountId,
            string? asOf,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                RecordKey key;
                if (asOf != null)
                {
                    key = ParseRecordKey(accountId, asOf);
                }
                else
                {
                    key = new RecordKey(accountId, DateTime.MaxValue, uint.MaxValue);
                }

                var balance = db.GetBalance(accountId, key);
                return Results.Ok(new BalanceResponse(balance));
            }
        });
    }

    private static RecordKey ParseRecordKey(string accountId, string encoded)
    {
        // Format: "2024-01-01T00:00:00Z,0"
        var decoded = HttpUtility.UrlDecode(encoded);
        var parts = decoded.Split(',');
        var date = DateTime.Parse(parts[0]);
        var sequence = uint.Parse(parts[1]);
        return new RecordKey(accountId, date, sequence);
    }
}
