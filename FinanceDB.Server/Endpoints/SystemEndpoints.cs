using System.Collections.Concurrent;
using FinanceDB.Core.Dto;
using FinanceDB.Core.Interfaces;

namespace FinanceDB.Server.Endpoints;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/system");

        // POST /system/save
        group.MapPost("/save", (
            IRecordStorage db,
            GlobalLock globalLock) =>
        {
            // Global lock for save - blocks all account operations
            lock (globalLock.Lock)
            {
                db.Save();
                return Results.NoContent();
            }
        });

        // POST /system/load
        group.MapPost("/load", (
            IRecordStorage db,
            GlobalLock globalLock) =>
        {
            // Global lock for load - blocks all account operations
            lock (globalLock.Lock)
            {
                db.Load();
                return Results.NoContent();
            }
        });

        // GET /system/record-count
        group.MapGet("/record-count", (
            IRecordStorage db,
            GlobalLock globalLock) =>
        {
            lock (globalLock.Lock)
            {
                var count = db.RecordCount();
                return Results.Ok(new RecordCountResponse(count));
            }
        });
    }
}
