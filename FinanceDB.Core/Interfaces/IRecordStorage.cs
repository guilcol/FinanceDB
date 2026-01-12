using FinanceDB.Core.Models;

namespace FinanceDB.Core.Interfaces;

/// <summary>
/// Defines the contract for record storage operations in FinanceDB.
///
/// <para><b>ATOMICITY CONTRACT:</b></para>
/// Each mutation method in this interface represents exactly one atomic logical operation
/// from the perspective of the caller. An atomic operation must either:
/// <list type="bullet">
///   <item>Complete fully with all intended side effects visible, OR</item>
///   <item>Have no externally visible effect (as if the operation never occurred)</item>
/// </list>
///
/// <para><b>MUTATION ENTRY POINTS:</b></para>
/// The following methods are classified as mutation entry points:
/// <list type="number">
///   <item><see cref="Insert"/> - Single record insertion</item>
///   <item><see cref="Update"/> - Single record modification</item>
///   <item><see cref="Delete(Record)"/> - Single record deletion by record</item>
///   <item><see cref="Delete(RecordKey)"/> - Single record deletion by key</item>
///   <item><see cref="DeleteRange"/> - Bulk deletion within key bounds</item>
///   <item><see cref="Save"/> - Persistence of in-memory state to durable storage</item>
///   <item><see cref="Load"/> - Hydration of in-memory state from durable storage</item>
/// </list>
/// </summary>
public interface IRecordStorage
{
    #region Mutation Entry Point #1: Save

    /// <summary>
    /// Persists all in-memory state to durable storage.
    /// </summary>
    public void Save();

    #endregion

    #region Mutation Entry Point #2: Load

    /// <summary>
    /// Hydrates in-memory state from durable storage.
    /// </summary>
    public void Load();

    #endregion

    #region Mutation Entry Point #3: Insert

    /// <summary>
    /// Inserts a single record into the storage.
    /// </summary>
    /// <param name="record">The record to insert. Key must be unique within the account.</param>
    /// <returns>True if inserted successfully; false if key already exists.</returns>
    public bool Insert(Record record);

    #endregion

    #region Mutation Entry Point #4: Update

    /// <summary>
    /// Updates an existing record in storage.
    /// </summary>
    /// <param name="record">The record with updated values. Key must exist in storage.</param>
    /// <returns>True if updated successfully; false if key not found.</returns>
    public bool Update(Record record);

    #endregion

    #region Mutation Entry Point #5a: Delete (by Record)

    /// <summary>
    /// Deletes a single record from storage.
    /// </summary>
    /// <param name="record">The record to delete. Matched by Key.</param>
    /// <returns>True if deleted successfully; false if not found.</returns>
    public bool Delete(Record record);

    #endregion

    #region Mutation Entry Point #5b: Delete (by Key)

    /// <summary>
    /// Deletes a single record from storage by its key.
    /// </summary>
    /// <param name="key">The key of the record to delete.</param>
    /// <returns>True if deleted successfully; false if not found.</returns>
    public bool Delete(RecordKey key);

    #endregion

    #region Mutation Entry Point #6: DeleteRange

    /// <summary>
    /// Deletes all records within an inclusive key range.
    /// </summary>
    /// <param name="startKey">Inclusive start of range.</param>
    /// <param name="endKey">Inclusive end of range. Must have same AccountId as startKey.</param>
    /// <returns>Count of records deleted.</returns>
    public int DeleteRange(RecordKey startKey, RecordKey endKey);

    #endregion

    #region Query Operations (Non-Mutating)

    /// <summary>
    /// Lists all records for a given account.
    /// </summary>
    public IReadOnlyList<Record>? List(string accountId);

    /// <summary>
    /// Lists records within an inclusive key range.
    /// </summary>
    public IReadOnlyList<Record>? ListRange(RecordKey startKey, RecordKey endKey);

    /// <summary>
    /// Calculates cumulative balance up to and including the specified key.
    /// </summary>
    public decimal GetBalance(string accountId, RecordKey key);

    /// <summary>
    /// Returns total count of records across all accounts.
    /// </summary>
    public int RecordCount();

    /// <summary>
    /// Checks if a key exists in storage.
    /// </summary>
    public bool ContainsKey(RecordKey key);

    /// <summary>
    /// Retrieves a record by key.
    /// </summary>
    public Record? Read(RecordKey key);

    /// <summary>
    /// Computes the next available sequence number for a given key's account and date.
    /// </summary>
    public RecordKey AdjustKey(RecordKey key);

    #endregion
}
