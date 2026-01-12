using FinanceDB.Core.Interfaces;
using Newtonsoft.Json;

namespace FinanceDB.Core.Storage;

/// <summary>
/// File-based implementation of <see cref="INodeStorage"/> that persists B-tree nodes to JSON files.
/// </summary>
public class FileNodeStorage : INodeStorage
{
    private readonly Random _rand;

    /// <summary>
    /// In-memory cache of nodes. Key is node ID, value is the node.
    /// </summary>
    private readonly Dictionary<long, BTreeNode> _cache = new();

    /// <summary>
    /// Base path for node files: Nodes/{accountId}/
    /// </summary>
    private readonly string path;

    /// <summary>
    /// Creates a new FileNodeStorage for the specified account.
    /// </summary>
    public FileNodeStorage(Random rand, string accountId)
    {
        _rand = rand;
        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Nodes", accountId);
    }

    /// <summary>
    /// Retrieves a node by ID, loading from disk if not in cache.
    /// </summary>
    public BTreeNode? Get(long nodeId)
    {
        if (_cache.ContainsKey(nodeId))
        {
            return _cache[nodeId];
        }

        string fileName = $"{nodeId}.json";
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        string fileContent = File.ReadAllText(fullPath);
        BTreeNode? nodeFromFile = JsonConvert.DeserializeObject<BTreeNode>(fileContent);
        Put(nodeFromFile);
        return nodeFromFile;
    }

    /// <summary>
    /// Stores a node in the cache. Does NOT write to disk.
    /// </summary>
    public void Put(BTreeNode node)
    {
        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);
        _cache.Add(node.Id, node);
    }

    /// <summary>
    /// Removes a node from cache and deletes its file from disk.
    /// </summary>
    public void Delete(BTreeNode node)
    {
        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);

        string fileName = $"{node.Id}.json";
        string fullPath = Path.Combine(path, fileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Returns all cached nodes.
    /// </summary>
    public ICollection<BTreeNode> List()
    {
        return _cache.Values;
    }

    /// <summary>
    /// Returns number of nodes in cache.
    /// </summary>
    public int CacheLength()
    {
        return _cache.Count;
    }

    /// <summary>
    /// Writes all cached nodes to disk, overwriting existing files.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(path);

        foreach (var item in _cache)
        {
            long nodeId = item.Key;
            BTreeNode node = item.Value;
            string fileName = $"{nodeId}.json";
            string filePath = Path.Combine(path, fileName);

            string nodeToJson = JsonConvert.SerializeObject(node);
            File.WriteAllText(filePath, nodeToJson);
        }
    }

    /// <summary>
    /// Generates a unique node ID not already in the cache.
    /// </summary>
    public long GenerateNodeId()
    {
        long result = _rand.NextInt64(long.MaxValue);
        while (_cache.ContainsKey(result))
            result = _rand.NextInt64(long.MaxValue);

        return result;
    }
}
