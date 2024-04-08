using System.IO.Enumeration;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace FinanceDB;

using System;
using System.IO;

public class BasicRs : IRecordStorage
{
    private string FileName;
    private List<Record> database = new List<Record>();

    public BasicRs(string file)
    {
        FileName = file;
    }

    public void Save()
    {
        string jsonString = JsonConvert.SerializeObject(database, Formatting.Indented);
        File.WriteAllText(FileName, jsonString);
    }

    public void Load()
    {
        string json = File.ReadAllText(FileName);
        database = JsonConvert.DeserializeObject<List<Record>>(json);
    }

    public bool Insert(Record record)
    {
        int index = GetIndexFromKey(record.Key);
        if (index >= 0)
            return false;
        index = ~index;
        database.Insert(index, record);
        return true;
    }

    public bool Update(Record record)
    {
        int index = GetIndexFromKey(record.Key);
        if (index < 0)
            return false;
        database[index] = record.Copy();
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
        database.RemoveAt(index);
        return true;
    }

    public IReadOnlyList<Record> List(string accountId)
    {
        RecordKey firstKey = new RecordKey(accountId, DateTime.MinValue, 0);   
        int index = GetIndexFromKey(firstKey);

        if (index < 0) 
            index = ~index;
        
        List<Record> result = new List<Record>();
        
        for (int i = index; i < database.Count; i++)
        {
            Record record = database[i];
            if (record.Key.AccountId != accountId)
                break;
            result.Add(record);
        }

        return result;
    }

    public decimal GetBalance(string accountId, RecordKey key)
    {
        throw new NotImplementedException();
    }

    public decimal GetBalance(string accountId)
    {
        decimal result = 0;
        foreach (Record record in List(accountId)) 
            result += record.GetAmount();
        return result;
    }

    public int RecordCount()
    {
        return database.Count;
    }

    public bool ContainsKey(RecordKey key)
    {
        int index = GetIndexFromKey(key);
        return index >= 0;
    }

    private int GetIndexFromKey(RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = database.BinarySearch(dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    public Record? Read(RecordKey key)
    {
        int index = GetIndexFromKey(key);
        if (index < 0)
            return null;
        return database[index];
    }


    public RecordKey AdjustKey(RecordKey key)
    {
        // Instantiate new key with maximum possible sequence
        RecordKey maxKey = key.WithSequence(uint.MaxValue);

        // Binary search record with maxKey
        // The returned index will be a complement of the insertion position
        int index = GetIndexFromKey(maxKey);

        // If a record is found at maximum value, throw an exception
        if (index >= 0)
            throw new Exception("Too many records on the same account and date.");

        // Record is not found, find insertion index
        int insertionIndex = ~index;

        // In case insertion index is 0, there is no duplicate record, return key
        if (insertionIndex == 0)
            return key;

        int previousIndex = insertionIndex - 1;

        // Obtain record at previous index
        Record before = database[previousIndex];

        // If there is a collision of records, return a new RecordKey with an increment to the previous sequence field
        if (before.Key.WithSequence(key.Sequence) == key)
            return key.WithSequence(before.Key.Sequence + 1);

        // If there is no collision of records, return the original key
        return key;
    }
}