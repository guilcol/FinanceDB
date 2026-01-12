namespace FinanceDB;

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
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS: All modified nodes have been written to disk, tree structure is consistent</item>
    ///   <item>FAILURE: Implementation-dependent; current implementation may leave partial writes</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>Iterates all accounts, calling AccountBTree.Save() on each</item>
    ///   <item>AccountBTree.Save() triggers node splitting for overflowing nodes before file write</item>
    ///   <item>FileNodeStorage.Save() writes only NEW files (existing files not overwritten)</item>
    ///   <item>NOT transactional: partial failure leaves inconsistent state on disk</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Creates/updates files in Nodes/{accountId}/ directories</item>
    ///   <item>May modify in-memory tree structure (node splits)</item>
    /// </list>
    /// </summary>
    public void Save();

    #endregion

    #region Mutation Entry Point #2: Load

    /// <summary>
    /// Hydrates in-memory state from durable storage.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS: All persisted accounts are registered and ready for lazy-load access</item>
    ///   <item>FAILURE: Partial account registration possible; no rollback mechanism</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>Scans Nodes/ directory for account subdirectories</item>
    ///   <item>Creates AccountBTree instance for each discovered account</item>
    ///   <item>Actual node data is lazy-loaded on first access (not during Load)</item>
    ///   <item>Idempotent: calling multiple times does not duplicate accounts</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Populates internal accounts dictionary</item>
    ///   <item>No disk writes</item>
    /// </list>
    /// </summary>
    public void Load();

    #endregion

    #region Mutation Entry Point #3: Insert

    /// <summary>
    /// Inserts a single record into the storage.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS (returns true): Record exists in storage, queryable immediately</item>
    ///   <item>FAILURE (returns false): Storage unchanged, record not inserted (duplicate key)</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>Creates AccountBTree for account if not exists</item>
    ///   <item>Delegates to AccountBTree.Insert() which traverses tree to leaf</item>
    ///   <item>Creates new immutable BTreeNode with record inserted</item>
    ///   <item>Updates node cache; does NOT persist to disk (requires Save())</item>
    ///   <item>Returns false if key already exists (no partial state change)</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Modifies in-memory node cache</item>
    ///   <item>May create new AccountBTree instance</item>
    ///   <item>Updates Amount fields on affected nodes</item>
    /// </list>
    ///
    /// <para><b>NOTE:</b> Caller should use AdjustKey() before Insert() to handle sequence auto-increment.</para>
    /// </summary>
    /// <param name="record">The record to insert. Key must be unique within the account.</param>
    /// <returns>True if inserted successfully; false if key already exists.</returns>
    public bool Insert(Record record);

    #endregion

    #region Mutation Entry Point #4: Update

    /// <summary>
    /// Updates an existing record in storage.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS (returns true): Record updated with new values, immediately queryable</item>
    ///   <item>FAILURE (returns false): Storage unchanged, record not found</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>Delegates to AccountBTree.Update() which traverses tree to find record</item>
    ///   <item>Creates new immutable BTreeNode with record replaced at same index</item>
    ///   <item>Updates node cache; does NOT persist to disk (requires Save())</item>
    ///   <item>Returns false if key not found (no partial state change)</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Modifies in-memory node cache</item>
    ///   <item>Does NOT update Amount field (potential bug in current implementation)</item>
    /// </list>
    ///
    /// <para><b>INVARIANT:</b> Record.Key must match an existing key; key cannot be changed via Update.</para>
    /// </summary>
    /// <param name="record">The record with updated values. Key must exist in storage.</param>
    /// <returns>True if updated successfully; false if key not found.</returns>
    public bool Update(Record record);

    #endregion

    #region Mutation Entry Point #5a: Delete (by Record)

    /// <summary>
    /// Deletes a single record from storage.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS (returns true): Record removed, no longer queryable</item>
    ///   <item>FAILURE (returns false): Storage unchanged, record not found</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>Delegates to AccountBTree.Delete() which traverses tree to find record</item>
    ///   <item>Creates new immutable BTreeNode with record removed</item>
    ///   <item>Updates node cache; does NOT persist to disk (requires Save())</item>
    ///   <item>Does NOT rebalance tree (may leave unbalanced structure)</item>
    ///   <item>Does NOT update Amount field (bug in current implementation)</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Modifies in-memory node cache</item>
    ///   <item>Tree may become unbalanced after many deletes</item>
    /// </list>
    /// </summary>
    /// <param name="record">The record to delete. Matched by Key.</param>
    /// <returns>True if deleted successfully; false if not found.</returns>
    public bool Delete(Record record);

    #endregion

    #region Mutation Entry Point #5b: Delete (by Key)

    /// <summary>
    /// Deletes a single record from storage by its key.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS (returns true): Record removed, no longer queryable</item>
    ///   <item>FAILURE (returns false): Storage unchanged, key not found or account not found</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1):</b></para>
    /// <list type="bullet">
    ///   <item>First performs Read() to retrieve the full record</item>
    ///   <item>Then delegates to Delete(Record) overload</item>
    ///   <item>Returns false if account doesn't exist OR key not found</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Same as Delete(Record)</item>
    /// </list>
    /// </summary>
    /// <param name="key">The key of the record to delete.</param>
    /// <returns>True if deleted successfully; false if not found.</returns>
    public bool Delete(RecordKey key);

    #endregion

    #region Mutation Entry Point #6: DeleteRange

    /// <summary>
    /// Deletes all records within an inclusive key range.
    ///
    /// <para><b>ATOMICITY ASSERTION:</b></para>
    /// This operation represents ONE atomic logical operation. Upon return:
    /// <list type="bullet">
    ///   <item>SUCCESS (returns count > 0): All records in range removed atomically</item>
    ///   <item>PARTIAL SUCCESS: NOT GUARANTEED - current implementation may delete some records before failure</item>
    ///   <item>NO MATCHES (returns 0): Storage unchanged, no records in range</item>
    /// </list>
    ///
    /// <para><b>CURRENT BEHAVIOR (v1) - ATOMICITY WARNING:</b></para>
    /// <list type="bullet">
    ///   <item>Retrieves full list of records for account via List()</item>
    ///   <item>Iterates and calls Delete() on each record in range</item>
    ///   <item>NOT ATOMIC: If Delete() fails mid-iteration, partial deletes remain</item>
    ///   <item>Inclusive range: startKey &lt;= record.Key &lt;= endKey</item>
    ///   <item>startKey and endKey must have same AccountId</item>
    /// </list>
    ///
    /// <para><b>SIDE EFFECTS:</b></para>
    /// <list type="bullet">
    ///   <item>Modifies in-memory node cache (potentially many nodes)</item>
    ///   <item>Does NOT persist to disk (requires Save())</item>
    /// </list>
    ///
    /// <para><b>TODO:</b> Implement true atomicity with rollback on partial failure.</para>
    /// </summary>
    /// <param name="startKey">Inclusive start of range.</param>
    /// <param name="endKey">Inclusive end of range. Must have same AccountId as startKey.</param>
    /// <returns>Count of records deleted.</returns>
    public int DeleteRange(RecordKey startKey, RecordKey endKey);

    #endregion

    #region Query Operations (Non-Mutating)

    /// <summary>
    /// Lists all records for a given account.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public IReadOnlyList<Record>? List(string accountId);

    /// <summary>
    /// Lists records within an inclusive key range.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public IReadOnlyList<Record>? ListRange(RecordKey startKey, RecordKey endKey);

    /// <summary>
    /// Calculates cumulative balance up to and including the specified key.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public decimal GetBalance(string accountId, RecordKey key);

    /// <summary>
    /// Returns total count of records across all accounts.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public int RecordCount();

    /// <summary>
    /// Checks if a key exists in storage.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public bool ContainsKey(RecordKey key);

    /// <summary>
    /// Retrieves a record by key.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation.</para>
    /// </summary>
    public Record? Read(RecordKey key);

    /// <summary>
    /// Computes the next available sequence number for a given key's account and date.
    /// <para><b>NON-MUTATING:</b> This is a read-only query operation that informs Insert.</para>
    /// </summary>
    public RecordKey AdjustKey(RecordKey key);

    #endregion
}
