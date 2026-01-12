using System.Text.Json;
using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Core.Storage;

/// <summary>
/// Simple list-based implementation of IRecordStorage for testing.
/// Uses binary search on an in-memory list.
/// </summary>
public class BasicRs : IRecordStorage
{
    private readonly string _fileName;
    private List<Record> _database = new List<Record>();

    public BasicRs(string file)
    {
        _fileName = file;
    }

    public void Save()
    {
        string jsonString = JsonSerializer.Serialize(_database, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_fileName, jsonString);
    }

    public void Load()
    {
        string json = File.ReadAllText(_fileName);
        _database = JsonSerializer.Deserialize<List<Record>>(json) ?? new List<Record>();
    }

    public bool Insert(Record record)
    {
        int index = GetIndexFromKey(record.Key);
        if (index >= 0)
            return false;
        index = ~index;
        _database.Insert(index, record);
        return true;
    }

    public bool Update(Record record)
    {
        int index = GetIndexFromKey(record.Key);
        if (index < 0)
            return false;
        _database[index] = record.Copy();
        return true;
    }

    public bool Delete(Record record)
    {
        return Delete(record.Key);
    }

    public bool Delete(RecordKey key)
    {
        int index = GetIndexFromKey(key);
        if (index < 0)
            return false;
        _database.RemoveAt(index);
        return true;
    }

    public int DeleteRange(RecordKey startKey, RecordKey endKey)
    {
        int startIndex = GetIndexFromKey(startKey);
        if (startIndex < 0)
            startIndex = ~startIndex;

        int endIndex = GetIndexFromKey(endKey);
        if (endIndex < 0)
            endIndex = ~endIndex - 1;

        if (startIndex > endIndex || startIndex >= _database.Count)
            return 0;

        int count = endIndex - startIndex + 1;
        _database.RemoveRange(startIndex, count);
        return count;
    }

    public IReadOnlyList<Record>? List(string accountId)
    {
        RecordKey firstKey = new RecordKey(accountId, DateTime.MinValue, 0);
        int index = GetIndexFromKey(firstKey);

        if (index < 0)
            index = ~index;

        List<Record> result = new List<Record>();

        for (int i = index; i < _database.Count; i++)
        {
            Record record = _database[i];
            if (record.Key.AccountId != accountId)
                break;
            result.Add(record);
        }

        return result;
    }

    public IReadOnlyList<Record>? ListRange(RecordKey startKey, RecordKey endKey)
    {
        int startIndex = GetIndexFromKey(startKey);
        if (startIndex < 0)
            startIndex = ~startIndex;

        List<Record> result = new List<Record>();

        for (int i = startIndex; i < _database.Count; i++)
        {
            Record record = _database[i];
            if (record.Key.CompareTo(endKey) > 0)
                break;
            result.Add(record);
        }

        return result;
    }

    public decimal GetBalance(string accountId, RecordKey key)
    {
        var records = List(accountId);
        if (records == null)
            return 0;

        decimal balance = 0;
        foreach (var record in records)
        {
            balance += record.GetAmount();
            if (record.Key.CompareTo(key) >= 0)
                break;
        }
        return balance;
    }

    public decimal GetBalance(string accountId)
    {
        decimal result = 0;
        var records = List(accountId);
        if (records != null)
        {
            foreach (Record record in records)
                result += record.GetAmount();
        }
        return result;
    }

    public int RecordCount()
    {
        return _database.Count;
    }

    public bool ContainsKey(RecordKey key)
    {
        int index = GetIndexFromKey(key);
        return index >= 0;
    }

    private int GetIndexFromKey(RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = _database.BinarySearch(dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    public Record? Read(RecordKey key)
    {
        int index = GetIndexFromKey(key);
        if (index < 0)
            return null;
        return _database[index];
    }

    public RecordKey AdjustKey(RecordKey key)
    {
        RecordKey maxKey = key.WithSequence(uint.MaxValue);
        int index = GetIndexFromKey(maxKey);

        if (index >= 0)
            throw new Exception("Too many records on the same account and date.");

        int insertionIndex = ~index;

        if (insertionIndex == 0)
            return key;

        int previousIndex = insertionIndex - 1;
        Record before = _database[previousIndex];

        if (before.Key.WithSequence(key.Sequence) == key)
            return key.WithSequence(before.Key.Sequence + 1);

        return key;
    }
}
