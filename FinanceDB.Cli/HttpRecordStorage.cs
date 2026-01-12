using System.Net;
using System.Net.Http.Json;
using System.Web;
using FinanceDB.Core.Dto;
using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Cli;

/// <summary>
/// HTTP client implementation of IRecordStorage.
/// Translates method calls to REST API requests.
/// </summary>
public class HttpRecordStorage : IRecordStorage
{
    private readonly HttpClient _client;

    public HttpRecordStorage(HttpClient client)
    {
        _client = client;
    }

    public bool Insert(Record record)
    {
        var request = new InsertRecordRequest(
            record.Key.Date,
            record.Key.Sequence,
            record.Description,
            record.Amount
        );

        var response = _client.PostAsJsonAsync(
            $"/accounts/{Encode(record.Key.AccountId)}/records",
            request
        ).GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.Created;
    }

    public bool Update(Record record)
    {
        var request = new UpdateRecordRequest(record.Description, record.Amount);
        var url = BuildRecordUrl(record.Key);

        var response = _client.PutAsJsonAsync(url, request)
            .GetAwaiter().GetResult();

        return response.IsSuccessStatusCode;
    }

    public bool Delete(Record record) => Delete(record.Key);

    public bool Delete(RecordKey key)
    {
        var url = BuildRecordUrl(key);
        var response = _client.DeleteAsync(url).GetAwaiter().GetResult();
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public int DeleteRange(RecordKey startKey, RecordKey endKey)
    {
        var url = $"/accounts/{Encode(startKey.AccountId)}/records" +
                  $"?from={EncodeKey(startKey)}&to={EncodeKey(endKey)}";

        var response = _client.DeleteAsync(url).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
            return 0;

        var result = response.Content.ReadFromJsonAsync<DeleteRangeResponse>()
            .GetAwaiter().GetResult();

        return result?.DeletedCount ?? 0;
    }

    public Record? Read(RecordKey key)
    {
        var url = BuildRecordUrl(key);
        var response = _client.GetAsync(url).GetAwaiter().GetResult();

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = response.Content.ReadFromJsonAsync<RecordDto>()
            .GetAwaiter().GetResult();

        return dto?.ToRecord();
    }

    public IReadOnlyList<Record>? List(string accountId)
    {
        var url = $"/accounts/{Encode(accountId)}/records";
        var response = _client.GetAsync(url).GetAwaiter().GetResult();

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dtos = response.Content.ReadFromJsonAsync<RecordDto[]>()
            .GetAwaiter().GetResult();

        return dtos?.Select(d => d.ToRecord()).ToList();
    }

    public IReadOnlyList<Record>? ListRange(RecordKey startKey, RecordKey endKey)
    {
        var url = $"/accounts/{Encode(startKey.AccountId)}/records" +
                  $"?from={EncodeKey(startKey)}&to={EncodeKey(endKey)}";

        var response = _client.GetAsync(url).GetAwaiter().GetResult();

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dtos = response.Content.ReadFromJsonAsync<RecordDto[]>()
            .GetAwaiter().GetResult();

        return dtos?.Select(d => d.ToRecord()).ToList();
    }

    public decimal GetBalance(string accountId, RecordKey key)
    {
        var url = $"/accounts/{Encode(accountId)}/balance?asOf={EncodeKey(key)}";
        var response = _client.GetAsync(url).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();

        var result = response.Content.ReadFromJsonAsync<BalanceResponse>()
            .GetAwaiter().GetResult();

        return result?.Balance ?? 0;
    }

    public bool ContainsKey(RecordKey key)
    {
        var url = BuildRecordUrl(key);
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = _client.SendAsync(request).GetAwaiter().GetResult();
        return response.IsSuccessStatusCode;
    }

    public RecordKey AdjustKey(RecordKey key)
    {
        var url = $"/accounts/{Encode(key.AccountId)}/records/adjust-key";
        var dto = RecordKeyDto.FromRecordKey(key);

        var response = _client.PostAsJsonAsync(url, dto)
            .GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();

        var result = response.Content.ReadFromJsonAsync<RecordKeyDto>()
            .GetAwaiter().GetResult();

        return result?.ToRecordKey() ?? key;
    }

    public int RecordCount()
    {
        var response = _client.GetAsync("/system/record-count")
            .GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();

        var result = response.Content.ReadFromJsonAsync<RecordCountResponse>()
            .GetAwaiter().GetResult();

        return result?.Count ?? 0;
    }

    public void Save()
    {
        var response = _client.PostAsync("/system/save", null)
            .GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Load()
    {
        var response = _client.PostAsync("/system/load", null)
            .GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    // Helper methods
    private string BuildRecordUrl(RecordKey key)
    {
        var date = HttpUtility.UrlEncode(key.Date.ToString("O"));
        return $"/accounts/{Encode(key.AccountId)}/records/{date}/{key.Sequence}";
    }

    private string EncodeKey(RecordKey key)
    {
        return HttpUtility.UrlEncode($"{key.Date:O},{key.Sequence}");
    }

    private string Encode(string value) => HttpUtility.UrlEncode(value);
}
