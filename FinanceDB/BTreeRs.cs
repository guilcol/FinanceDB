using System.Data.Common;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace FinanceDB;

/// <summary>
/// B-tree based implementation of <see cref="IRecordStorage"/>.
/// Manages multiple accounts, each with its own B-tree stored in <see cref="AccountBTree"/>.
///
/// <para><b>ATOMICITY IMPLEMENTATION NOTES:</b></para>
/// This class serves as the facade for all storage operations. Each mutation entry point
/// delegates to the appropriate <see cref="AccountBTree"/> instance. The atomicity guarantees
/// documented in <see cref="IRecordStorage"/> are implemented here with the following caveats:
///
/// <list type="bullet">
///   <item>Single-record operations (Insert, Update, Delete) are atomic within in-memory state</item>
///   <item>DeleteRange is NOT atomic - iterates and deletes individually</item>
///   <item>Save is NOT transactional - partial disk writes possible on failure</item>
///   <item>No concurrent access protection - single-threaded design assumed</item>
/// </list>
///
/// <para><b>ARCHITECTURE:</b></para>
/// <code>
/// BTreeRs (this class)
///    |
///    +-- accounts: Dictionary&lt;string, AccountBTree&gt;
///           |
///           +-- AccountBTree (per-account B-tree operations)
///                  |
///                  +-- FileNodeStorage (node persistence)
/// </code>
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
    /// <inheritdoc cref="IRecordStorage.Save"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// foreach (account in accounts)
    ///     account.Save()  // triggers split + file write
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> NOT ATOMIC across accounts.</para>
    /// If Save() fails on account N, accounts 0..N-1 may have been persisted.
    /// Within a single account, Save() is also not atomic (see AccountBTree.Save).
    /// </summary>
    public void Save()
    {
        // ATOMICITY BOUNDARY: Save() represents ONE logical operation to the caller.
        // CURRENT REALITY: Iterates accounts sequentially; no rollback on partial failure.
        foreach (var accountTree in accounts)
            accountTree.Value.Save();
    }

    #endregion

    #region Mutation Entry Point #2: Load

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.Load"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// scan Nodes/ directory
    /// foreach (subdirectory as accountId)
    ///     if not already loaded:
    ///         accounts[accountId] = new AccountBTree(...)
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> Effectively atomic for read-only operation.</para>
    /// Creates AccountBTree instances but does not read node data (lazy load).
    /// Idempotent: safe to call multiple times.
    /// </summary>
    public void Load()
    {
        // ATOMICITY BOUNDARY: Load() represents ONE logical operation to the caller.
        // Scans filesystem for existing account directories and registers them.
        // Actual node data is NOT loaded here - lazy loaded on first access.

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
    /// <inheritdoc cref="IRecordStorage.Insert"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// if account not exists:
    ///     create new AccountBTree
    /// return account.Insert(record)
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> ATOMIC within in-memory state.</para>
    /// Either the record is inserted and all affected nodes updated, or nothing changes.
    /// Account creation is atomic with insertion (no half-created account state).
    /// Changes are NOT persisted until Save() is called.
    /// </summary>
    /// <param name="record">Record to insert. Key.AccountId determines target account.</param>
    /// <returns>True if inserted; false if duplicate key exists.</returns>
    public bool Insert(Record record)
    {
        // ATOMICITY BOUNDARY: Insert() represents ONE logical operation to the caller.
        // Creates account if needed, then delegates to AccountBTree.Insert().
        // On failure (duplicate key), no state change occurs.

        string accountId = record.Key.AccountId;
        if (!accounts.ContainsKey(accountId))
            accounts[accountId] = new AccountBTree(rand, degree, accountId);
        return accounts[accountId].Insert(record);
    }

    #endregion

    #region Mutation Entry Point #4: Update

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.Update"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// return accounts[record.Key.AccountId].Update(record)
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> ATOMIC within in-memory state.</para>
    /// Either the record is updated, or nothing changes (key not found).
    ///
    /// <para><b>WARNING:</b> Throws KeyNotFoundException if account doesn't exist.</para>
    /// </summary>
    /// <param name="record">Record with updated values. Key must exist.</param>
    /// <returns>True if updated; false if key not found in account.</returns>
    public bool Update(Record record)
    {
        // ATOMICITY BOUNDARY: Update() represents ONE logical operation to the caller.
        // Delegates to AccountBTree.Update() which performs atomic in-place replacement.
        // WARNING: Will throw if account doesn't exist (no defensive check).

        return accounts[record.Key.AccountId].Update(record);
    }

    #endregion

    #region Mutation Entry Point #5a: Delete (by Record)

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.Delete(Record)"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// return accounts[record.Key.AccountId].Delete(record)
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> ATOMIC within in-memory state.</para>
    /// Either the record is deleted, or nothing changes (not found).
    ///
    /// <para><b>WARNING:</b> Throws KeyNotFoundException if account doesn't exist.</para>
    /// </summary>
    public bool Delete(Record record)
    {
        // ATOMICITY BOUNDARY: Delete() represents ONE logical operation to the caller.
        // Delegates to AccountBTree.Delete() which removes record from leaf node.
        // WARNING: Will throw if account doesn't exist (no defensive check).

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
    /// <inheritdoc cref="IRecordStorage.Delete(RecordKey)"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// if account not exists: return false
    /// record = account.Read(key)
    /// if record is null: return false
    /// return account.Delete(record)
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> ATOMIC within in-memory state.</para>
    /// Read + Delete is not interleaved with other operations (single-threaded).
    /// Either the record is deleted, or nothing changes.
    /// </summary>
    /// <param name="key">Key of record to delete.</param>
    /// <returns>True if deleted; false if account or key not found.</returns>
    public bool Delete(RecordKey key)
    {
        // ATOMICITY BOUNDARY: Delete(key) represents ONE logical operation to the caller.
        // Two-phase: Read to get full record, then Delete.
        // Single-threaded assumption means no race between Read and Delete.

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
    /// <inheritdoc cref="IRecordStorage.DeleteRange"/>
    ///
    /// <para><b>IMPLEMENTATION:</b></para>
    /// <code>
    /// allRecords = account.List()
    /// count = 0
    /// foreach record in allRecords:
    ///     if startKey &lt;= record.Key &lt;= endKey:
    ///         if account.Delete(record):
    ///             count++
    /// return count
    /// </code>
    ///
    /// <para><b>ATOMICITY STATUS:</b> NOT ATOMIC - VIOLATION OF CONTRACT.</para>
    /// Current implementation deletes records one-by-one in a loop.
    /// If any individual Delete() fails or an exception occurs mid-loop,
    /// previously deleted records remain deleted (partial mutation visible).
    ///
    /// <para><b>TODO:</b> Implement batch delete with rollback capability.</para>
    /// </summary>
    /// <param name="startKey">Inclusive range start. AccountId determines target account.</param>
    /// <param name="endKey">Inclusive range end. Must have same AccountId as startKey.</param>
    /// <returns>Count of successfully deleted records.</returns>
    public int DeleteRange(RecordKey startKey, RecordKey endKey)
    {
        // ATOMICITY BOUNDARY: DeleteRange() SHOULD represent ONE logical operation.
        // CURRENT REALITY: Iterates and deletes individually - NOT ATOMIC.
        // KNOWN ISSUE: Partial deletes will persist if operation fails mid-way.

        if (!accounts.ContainsKey(startKey.AccountId))
            return 0;

        var allRecords = accounts[startKey.AccountId].List();
        if (allRecords == null)
            return 0;

        int count = 0;
        foreach (var record in allRecords)
        {
            // Inclusive range check: startKey <= record.Key <= endKey
            if (record.Key.CompareTo(startKey) >= 0 && record.Key.CompareTo(endKey) <= 0)
            {
                // Each Delete() is atomic, but the loop is not.
                // If Delete() returns false or throws, we have partial state.
                if (accounts[startKey.AccountId].Delete(record))
                    count++;
            }
        }
        return count;
    }

    #endregion

    #region Query Operations (Non-Mutating)

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.List"/>
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.List().</para>
    /// </summary>
    public IReadOnlyList<Record>? List(string accountId)
    {
        if (!accounts.ContainsKey(accountId))
            return null;

        return accounts[accountId].List();
    }

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.ListRange"/>
    /// <para><b>NON-MUTATING:</b> Filters List() result by key range.</para>
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
            // Inclusive range: startKey <= record.Key <= endKey
            if (record.Key.CompareTo(startKey) >= 0 && record.Key.CompareTo(endKey) <= 0)
            {
                result.Add(record);
            }
        }
        return result;
    }

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.GetBalance"/>
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.GetBalance(key).</para>
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
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.GetBalance().</para>
    /// </summary>
    public decimal GetBalance(string accountId)
    {
        if (!accounts.ContainsKey(accountId))
            return 0;

        return accounts[accountId].GetBalance();
    }

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.RecordCount"/>
    /// <para><b>NON-MUTATING:</b> Aggregates RecordCount() across all accounts.</para>
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
    /// <inheritdoc cref="IRecordStorage.ContainsKey"/>
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.ContainsKey().</para>
    /// </summary>
    public bool ContainsKey(RecordKey key)
    {
        accounts.TryGetValue(key.AccountId, out var accountBtree);
        return accountBtree?.ContainsKey(key) ?? false;
    }

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.Read"/>
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.Read().</para>
    /// </summary>
    public Record? Read(RecordKey key)
    {
        if (!accounts.ContainsKey(key.AccountId))
            return null;

        return accounts[key.AccountId].Read(key);
    }

    /// <summary>
    /// <inheritdoc cref="IRecordStorage.AdjustKey"/>
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.AdjustKey().</para>
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
    /// <para><b>NON-MUTATING:</b> Pure function on input array.</para>
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
    /// <para><b>NON-MUTATING:</b> Pure function on input array.</para>
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
    /// <para><b>NON-MUTATING:</b> Delegates to AccountBTree.CacheLength().</para>
    /// </summary>
    public int GetCacheLengthFromAccountId(string accountId)
    {
        return accounts[accountId].CacheLength();
    }

    #endregion
}
