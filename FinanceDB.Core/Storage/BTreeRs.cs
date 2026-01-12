using FinanceDB.Core.Interfaces;
using FinanceDB.Core.Models;

namespace FinanceDB.Core.Storage;

/// <summary>
/// B-tree based implementation of <see cref="IRecordStorage"/>.
/// Manages multiple accounts, each with its own B-tree stored in <see cref="AccountBTree"/>.
/// </summary>
public class BTreeRs : IRecordStorage
{
    private readonly Random rand;

    /// <summary>
    /// B-tree degree (maximum children per node). Shared across all AccountBTree instances.
    /// </summary>
    public static int degree;

    /// <summary>
    /// Maps account IDs to their respective B-tree instances.
    /// Created lazily on first access to each account.
    /// </summary>
    private readonly Dictionary<string, AccountBTree> accounts = new Dictionary<string, AccountBTree>();

    public BTreeRs(Random rand, int deg)
    {
        this.rand = rand;
        degree = deg;
    }

    #region Mutation Entry Point #1: Save

    /// <summary>
    /// Persists all in-memory state to durable storage.
    /// </summary>
    public void Save()
    {
        foreach (var accountTree in accounts)
            accountTree.Value.Save();
    }

    #endregion

    #region Mutation Entry Point #2: Load

    /// <summary>
    /// Hydrates in-memory state from durable storage.
    /// </summary>
    public void Load()
    {
        string nodesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Nodes");
        if (!Directory.Exists(nodesPath))
            return;

        foreach (var accountDir in Directory.GetDirectories(nodesPath))
        {
            string accountId = Path.GetFileName(accountDir);
            if (!accounts.ContainsKey(accountId))
            {
                accounts[accountId] = new AccountBTree(rand, degree, accountId);
            }
        }
    }

    #endregion

    #region Mutation Entry Point #3: Insert

    /// <summary>
    /// Inserts a single record into the storage.
    /// </summary>
    public bool Insert(Record record)
    {
        string accountId = record.Key.AccountId;
        if (!accounts.ContainsKey(accountId))
            accounts[accountId] = new AccountBTree(rand, degree, accountId);
        return accounts[accountId].Insert(record);
    }

    #endregion

    #region Mutation Entry Point #4: Update

    /// <summary>
    /// Updates an existing record in storage.
    /// </summary>
    public bool Update(Record record)
    {
        return accounts[record.Key.AccountId].Update(record);
    }

    #endregion

    #region Mutation Entry Point #5a: Delete (by Record)

    /// <summary>
    /// Deletes a single record from storage.
    /// </summary>
    public bool Delete(Record record)
    {
        return accounts[record.Key.AccountId].Delete(record);
    }

    /// <summary>
    /// Explicit interface implementation - forwards to public Delete(Record).
    /// </summary>
    bool IRecordStorage.Delete(Record record)
    {
        return Delete(record);
    }

    #endregion

    #region Mutation Entry Point #5b: Delete (by Key)

    /// <summary>
    /// Deletes a single record from storage by its key.
    /// </summary>
    public bool Delete(RecordKey key)
    {
        if (!accounts.ContainsKey(key.AccountId))
            return false;

        Record record = accounts[key.AccountId].Read(key);
        if (record == null)
            return false;

        return accounts[key.AccountId].Delete(record);
    }

    #endregion

    #region Mutation Entry Point #6: DeleteRange

    /// <summary>
    /// Deletes all records within an inclusive key range.
    /// </summary>
    public int DeleteRange(RecordKey startKey, RecordKey endKey)
    {
        if (!accounts.ContainsKey(startKey.AccountId))
            return 0;

        var allRecords = accounts[startKey.AccountId].List();
        if (allRecords == null)
            return 0;

        int count = 0;
        foreach (var record in allRecords)
        {
            if (record.Key.CompareTo(startKey) >= 0 && record.Key.CompareTo(endKey) <= 0)
            {
                if (accounts[startKey.AccountId].Delete(record))
                    count++;
            }
        }
        return count;
    }

    #endregion

    #region Query Operations (Non-Mutating)

    /// <summary>
    /// Lists all records for a given account.
    /// </summary>
    public IReadOnlyList<Record>? List(string accountId)
    {
        if (!accounts.ContainsKey(accountId))
            return null;

        return accounts[accountId].List();
    }

    /// <summary>
    /// Lists records within an inclusive key range.
    /// </summary>
    public IReadOnlyList<Record>? ListRange(RecordKey startKey, RecordKey endKey)
    {
        if (!accounts.ContainsKey(startKey.AccountId))
            return null;

        var allRecords = accounts[startKey.AccountId].List();
        if (allRecords == null)
            return null;

        List<Record> result = new List<Record>();
        foreach (var record in allRecords)
        {
            if (record.Key.CompareTo(startKey) >= 0 && record.Key.CompareTo(endKey) <= 0)
            {
                result.Add(record);
            }
        }
        return result;
    }

    /// <summary>
    /// Calculates cumulative balance up to and including the specified key.
    /// </summary>
    public decimal GetBalance(string accountId, RecordKey key)
    {
        if (accounts.ContainsKey(accountId))
        {
            return accounts[accountId].GetBalance(key);
        }

        return 0;
    }

    /// <summary>
    /// Returns total account balance (sum of all record amounts).
    /// </summary>
    public decimal GetBalance(string accountId)
    {
        if (!accounts.ContainsKey(accountId))
            return 0;

        return accounts[accountId].GetBalance();
    }

    /// <summary>
    /// Returns total count of records across all accounts.
    /// </summary>
    public int RecordCount()
    {
        int count = 0;
        foreach (var accountTree in accounts)
        {
            count += accountTree.Value.RecordCount();
        }
        return count;
    }

    /// <summary>
    /// Checks if a key exists in storage.
    /// </summary>
    public bool ContainsKey(RecordKey key)
    {
        accounts.TryGetValue(key.AccountId, out var accountBtree);
        return accountBtree?.ContainsKey(key) ?? false;
    }

    /// <summary>
    /// Retrieves a record by key.
    /// </summary>
    public Record? Read(RecordKey key)
    {
        if (!accounts.ContainsKey(key.AccountId))
            return null;

        return accounts[key.AccountId].Read(key);
    }

    /// <summary>
    /// Computes the next available sequence number for a given key's account and date.
    /// </summary>
    public RecordKey AdjustKey(RecordKey key)
    {
        if (!accounts.ContainsKey(key.AccountId))
            return key;

        return accounts[key.AccountId].AdjustKey(key);
    }

    #endregion

    #region Internal Utilities

    private int GetIndexFromKey(RecordKey key, Record[] list)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(list, dummyRecord, ByKeyRecordComparer.Instance);
        return index;
    }

    /// <summary>
    /// Checks if key exists in a sorted record array.
    /// </summary>
    public bool ContainsKeyInRecords(RecordKey key, Record[] list)
    {
        int index = GetIndexFromKey(key, list);
        if (index >= 0)
            return true;
        return false;
    }

    /// <summary>
    /// Reads a record from a sorted record array by key.
    /// </summary>
    public Record? ReadFromRecords(Record[] currentNodeRecords, RecordKey key)
    {
        Record dummyRecord = new Record(key, "", 0);
        int index = Array.BinarySearch(currentNodeRecords, dummyRecord, ByKeyRecordComparer.Instance);
        if (index >= 0)
            return currentNodeRecords[index];
        return null;
    }

    /// <summary>
    /// Returns the node cache size for diagnostic purposes.
    /// </summary>
    public int GetCacheLengthFromAccountId(string accountId)
    {
        return accounts[accountId].CacheLength();
    }

    #endregion
}
