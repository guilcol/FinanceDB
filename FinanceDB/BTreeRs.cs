using System.Data.Common;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace FinanceDB;

public class BTreeRs : IRecordStorage
{
    private readonly Random rand;
    public static int degree;
    private readonly Dictionary<string, AccountBTree> accounts = new Dictionary<string, AccountBTree>();

    public BTreeRs(Random rand, int deg)
    {
        this.rand = rand;
        degree = deg;
    }

    public void Save()
    {
        foreach (var accountTree in accounts)
            accountTree.Value.Save();
    }

    public void Load()
    {
        throw new NotImplementedException();
    }

    public bool Insert(Record record)
    {
        string accountId = record.Key.AccountId;
        if (!accounts.ContainsKey(accountId))
            accounts[accountId] = new AccountBTree(rand, degree, accountId);
        return accounts[accountId].Insert(record);
    }

    public bool Delete(Record record)
    {
        return accounts[record.Key.AccountId].Delete(record);
    }

    public bool Update(Record record)
    {
        return accounts[record.Key.AccountId].Update(record);

    } 

    private int GetIndexFromKey(RecordKey key, Record[] list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(list, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    bool IRecordStorage.Delete(Record record)
    {
        throw new NotImplementedException();
    }

    public bool Delete(RecordKey key)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<Record>? List(string accountId)
    {
        return accounts[accountId].List();
    }

    public decimal GetBalance(string accountId, RecordKey key)
    {
        if (accounts.ContainsKey(accountId))
        {
            return accounts[accountId].GetBalance(key);
        }

        return 0;
    }

    public int RecordCount()
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(RecordKey key, BTreeNode node)
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(RecordKey key)
    {
        accounts.TryGetValue(key.AccountId, out var accountBtree);
        return accountBtree?.ContainsKey(key) ?? false;
    }

    public bool ContainsKeyInRecords(RecordKey key, Record[] list)
    {
        int index = GetIndexFromKey(key, list);
        if (index >= 0)
            return true;
        return false;
    }

    public Record Read(RecordKey key)
    {
        return accounts[key.AccountId].Read(key); 
    }
    

    public Record? ReadFromRecords(Record[] currentNodeRecords, RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(currentNodeRecords, dummyRecord, ByKeyRecordComparer.Instance);
        if (index >= 0)
            return currentNodeRecords[index];
        return null;
    }

    public int GetCacheLengthFromAccountId(string accountId)
    {
        return accounts[accountId].CacheLength();
    }
    

    public RecordKey AdjustKey(RecordKey key)
    {
        throw new NotImplementedException();
    }
    
}