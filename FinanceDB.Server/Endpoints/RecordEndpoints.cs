using System.Collections.Concurrent;
using System.Web;
using FinanceDB.Core.Dto;
using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Server.Endpoints;

public static class RecordEndpoints
{
    public static void MapRecordEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/accounts/{accountId}/records");

        // POST /accounts/{accountId}/records - Insert
        group.MapPost("/", (
            string accountId,
            InsertRecordRequest request,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var date = request.Date ?? DateTime.UtcNow;
                var key = new RecordKey(accountId, date, request.Sequence ?? 0);

                // Auto-adjust key if sequence not specified
                if (request.Sequence == null)
                {
                    key = db.AdjustKey(key);
                }

                var record = new Record(key, request.Description, request.Amount);

                if (!db.Insert(record))
                {
                    return Results.Conflict(new ErrorResponse("Duplicate key"));
                }

                var location = $"/accounts/{accountId}/records/{Uri.EscapeDataString(key.Date.ToString("O"))}/{key.Sequence}";
                return Results.Created(location, RecordDto.FromRecord(record));
            }
        });

        // GET /accounts/{accountId}/records - List all or range
        group.MapGet("/", (
            string accountId,
            string? from,
            string? to,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                if (from != null && to != null)
                {
                    var startKey = ParseRecordKey(accountId, from);
                    var endKey = ParseRecordKey(accountId, to);
                    var records = db.ListRange(startKey, endKey);
                    if (records == null) return Results.NotFound(new ErrorResponse("Account not found"));
                    return Results.Ok(records.Select(RecordDto.FromRecord));
                }

                var allRecords = db.List(accountId);
                if (allRecords == null)
                {
                    return Results.NotFound(new ErrorResponse("Account not found"));
                }
                return Results.Ok(allRecords.Select(RecordDto.FromRecord));
            }
        });

        // GET /accounts/{accountId}/records/{date}/{sequence} - Read single
        group.MapGet("/{date}/{sequence}", (
            string accountId,
            string date,
            uint sequence,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var parsedDate = DateTime.Parse(HttpUtility.UrlDecode(date));
                var key = new RecordKey(accountId, parsedDate, sequence);
                var record = db.Read(key);

                if (record == null)
                {
                    return Results.NotFound(new ErrorResponse("Record not found"));
                }

                return Results.Ok(RecordDto.FromRecord(record));
            }
        });

        // PUT /accounts/{accountId}/records/{date}/{sequence} - Update
        group.MapPut("/{date}/{sequence}", (
            string accountId,
            string date,
            uint sequence,
            UpdateRecordRequest request,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var parsedDate = DateTime.Parse(HttpUtility.UrlDecode(date));
                var key = new RecordKey(accountId, parsedDate, sequence);
                var existing = db.Read(key);

                if (existing == null)
                {
                    return Results.NotFound(new ErrorResponse("Record not found"));
                }

                var updated = new Record(
                    key,
                    request.Description ?? existing.Description,
                    request.Amount ?? existing.Amount
                );

                if (!db.Update(updated))
                {
                    return Results.NotFound(new ErrorResponse("Update failed"));
                }

                return Results.Ok(RecordDto.FromRecord(updated));
            }
        });

        // DELETE /accounts/{accountId}/records/{date}/{sequence} - Delete single
        group.MapDelete("/{date}/{sequence}", (
            string accountId,
            string date,
            uint sequence,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var parsedDate = DateTime.Parse(HttpUtility.UrlDecode(date));
                var key = new RecordKey(accountId, parsedDate, sequence);

                if (!db.Delete(key))
                {
                    return Results.NotFound(new ErrorResponse("Record not found"));
                }

                return Results.NoContent();
            }
        });

        // DELETE /accounts/{accountId}/records?from=...&to=... - Delete range
        group.MapDelete("/", (
            string accountId,
            string from,
            string to,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var startKey = ParseRecordKey(accountId, from);
                var endKey = ParseRecordKey(accountId, to);

                var count = db.DeleteRange(startKey, endKey);

                return Results.Ok(new DeleteRangeResponse(count));
            }
        });

        // HEAD /accounts/{accountId}/records/{date}/{sequence} - ContainsKey
        group.MapMethods("/{date}/{sequence}", new[] { "HEAD" }, (
            string accountId,
            string date,
            uint sequence,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var parsedDate = DateTime.Parse(HttpUtility.UrlDecode(date));
                var key = new RecordKey(accountId, parsedDate, sequence);
                return db.ContainsKey(key) ? Results.Ok() : Results.NotFound();
            }
        });

        // POST /accounts/{accountId}/records/adjust-key - AdjustKey
        group.MapPost("/adjust-key", (
            string accountId,
            RecordKeyDto keyDto,
            IRecordStorage db,
            ConcurrentDictionary<string, object> lockDict) =>
        {
            var lockObj = lockDict.GetOrAdd(accountId, _ => new object());
            lock (lockObj)
            {
                var key = new RecordKey(accountId, keyDto.Date, keyDto.Sequence);
                var adjusted = db.AdjustKey(key);
                return Results.Ok(RecordKeyDto.FromRecordKey(adjusted));
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
