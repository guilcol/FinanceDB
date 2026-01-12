using Newtonsoft.Json;

namespace FinanceDB;

/// <summary>
/// File-based implementation of <see cref="INodeStorage"/> that persists B-tree nodes to JSON files.
///
/// <para><b>THREADING MODEL - CRITICAL:</b></para>
/// This class is designed for SINGLE-THREADED, EXCLUSIVE-ACCESS operation.
/// There are NO concurrency controls. The following assumptions MUST hold:
/// <list type="bullet">
///   <item>Only one thread accesses this instance at a time</item>
///   <item>No concurrent file system access to the Nodes directory</item>
///   <item>No external processes modify node files during operation</item>
/// </list>
/// Violation of these assumptions results in UNDEFINED BEHAVIOR including data corruption.
///
/// <para><b>STORAGE MODEL:</b></para>
/// <code>
/// Nodes/
///   {accountId}/
///     0.json          (root node, always Id=0)
///     {nodeId}.json   (other nodes with random long IDs)
/// </code>
///
/// <para><b>CACHING:</b></para>
/// Nodes are lazy-loaded from disk on first Get() and cached in memory.
/// Put() updates the cache but does NOT write to disk.
/// Save() writes all cached nodes to disk, overwriting existing files.
///
/// <para><b>RESOLVED LIMITATIONS:</b></para>
/// <list type="bullet">
///   <item>FL-004: FIXED - Save() now overwrites all cached nodes to disk</item>
///   <item>FL-005: FIXED - Path construction uses Path.Combine()</item>
/// </list>
/// </summary>
public class FileNodeStorage : INodeStorage
{
    private readonly Random _rand;

    /// <summary>
    /// In-memory cache of nodes. Key is node ID, value is the node.
    /// THREADING: Not thread-safe. Single-threaded access required.
    /// </summary>
    private readonly Dictionary<long, BTreeNode> _cache = new();

    /// <summary>
    /// Base path for node files: Nodes/{accountId}/
    /// </summary>
    private readonly string path;

    /// <summary>
    /// Creates a new FileNodeStorage for the specified account.
    /// THREADING: Constructor is not thread-safe.
    /// </summary>
    /// <param name="rand">Random number generator for node ID generation.</param>
    /// <param name="accountId">Account ID determines subdirectory name.</param>
    public FileNodeStorage(Random rand, string accountId)
    {
        // THREADING ASSUMPTION: Called from single thread during initialization.
        _rand = rand;
        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Nodes", accountId);
    }

    /// <summary>
    /// Retrieves a node by ID, loading from disk if not in cache.
    ///
    /// THREADING: Not thread-safe. Reads from cache and potentially disk.
    /// </summary>
    /// <param name="nodeId">ID of node to retrieve.</param>
    /// <returns>The node if found; null if not in cache and file doesn't exist.</returns>
    public BTreeNode? Get(long nodeId)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent cache modifications.

        if (_cache.ContainsKey(nodeId))
        {
            return _cache[nodeId];
        }

        // Lazy load from disk
        string fileName = $"{nodeId}.json";
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        // THREADING ASSUMPTION: No external modifications to file during read.
        string fileContent = File.ReadAllText(fullPath);
        BTreeNode? nodeFromFile = JsonConvert.DeserializeObject<BTreeNode>(fileContent);
        Put(nodeFromFile);
        return nodeFromFile;
    }

    /// <summary>
    /// Stores a node in the cache. Does NOT write to disk.
    ///
    /// THREADING: Not thread-safe. Modifies cache dictionary.
    /// </summary>
    /// <param name="node">Node to cache.</param>
    public void Put(BTreeNode node)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent cache reads.

        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);
        _cache.Add(node.Id, node);
    }

    /// <summary>
    /// Removes a node from cache and deletes its file from disk.
    ///
    /// THREADING: Not thread-safe. Modifies cache and file system.
    /// </summary>
    /// <param name="node">Node to delete.</param>
    public void Delete(BTreeNode node)
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent operations.

        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);

        string fileName = $"{node.Id}.json";
        string fullPath = Path.Combine(path, fileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Returns all cached nodes.
    ///
    /// THREADING: Not thread-safe. Returns reference to internal collection.
    /// Caller must not modify returned collection.
    /// </summary>
    /// <returns>Collection of all cached nodes.</returns>
    public ICollection<BTreeNode> List()
    {
        // THREADING ASSUMPTION: No concurrent cache modifications.
        return _cache.Values;
    }

    /// <summary>
    /// Returns number of nodes in cache.
    ///
    /// THREADING: Not thread-safe.
    /// </summary>
    public int CacheLength()
    {
        // THREADING ASSUMPTION: No concurrent cache modifications.
        return _cache.Count;
    }

    /// <summary>
    /// Writes all cached nodes to disk, overwriting existing files.
    ///
    /// <para><b>BEHAVIOR:</b></para>
    /// All nodes in the cache are serialized to JSON and written to disk.
    /// Existing files are overwritten to ensure modifications are persisted.
    ///
    /// THREADING: Not thread-safe. Reads cache, writes to file system.
    /// </summary>
    public void Save()
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent mutations or file access.

        // Ensure directory exists before writing any files
        Directory.CreateDirectory(path);

        foreach (var item in _cache)
        {
            long nodeId = item.Key;
            BTreeNode node = item.Value;
            string fileName = $"{nodeId}.json";
            string filePath = Path.Combine(path, fileName);

            // Always write/overwrite the file to persist all changes
            string nodeToJson = JsonConvert.SerializeObject(node);
            File.WriteAllText(filePath, nodeToJson);
        }
    }

    /// <summary>
    /// Generates a unique node ID not already in the cache.
    ///
    /// THREADING: Not thread-safe. Uses shared Random and reads cache.
    /// </summary>
    /// <returns>A random long value not currently used as a node ID in cache.</returns>
    public long GenerateNodeId()
    {
        // THREADING ASSUMPTION: Exclusive access. No concurrent cache modifications.

        long result = _rand.NextInt64(long.MaxValue);
        while (_cache.ContainsKey(result))
            result = _rand.NextInt64(long.MaxValue);

        return result;
    }
}
