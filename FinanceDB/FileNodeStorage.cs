using Newtonsoft.Json;

namespace FinanceDB;

public class FileNodeStorage : INodeStorage
{
    private readonly Random _rand;
    private readonly Dictionary<long, BTreeNode> _cache = new();
    private readonly string path;

    public FileNodeStorage(Random rand, string accountId)
    {
        _rand = rand;
        path = @"C:\\Users\\guilc\\RiderProjects\\FinanceDB\\FinanceDB\\Nodes\\" + accountId + @"\\";
    }

    public BTreeNode Get(long nodeId)
    {
        if (_cache.ContainsKey(nodeId))
        {
            return _cache[nodeId];
        }

        string fileName = $"{nodeId}.json";
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            return null;
        string fileContent = File.ReadAllText(fullPath);
        BTreeNode nodeFromFile = JsonConvert.DeserializeObject<BTreeNode>(fileContent);
        Put(nodeFromFile);
        return nodeFromFile;
    }
    
    public void Put(BTreeNode node)
    {
        if (_cache.ContainsKey(node.Id))
            _cache.Remove(node.Id);
        _cache.Add(node.Id, node);
    }
    
    public void Delete(BTreeNode node)
    {
        
    }

    public ICollection<BTreeNode> List()
    {
        return _cache.Values;
    }

    public int CacheLength()
    {
        return _cache.Count;
    }

    public void Save()
    {
        foreach (var item in _cache)
        {
            long nodeId = item.Key;
            BTreeNode node = item.Value;
            string fileName = nodeId + ".json";
            string filePath = path + fileName;
            if (!File.Exists(filePath))
            {
                Directory.CreateDirectory(path);
                string nodeToJson = JsonConvert.SerializeObject(node);
                File.WriteAllText(filePath, nodeToJson);
            }
        }
    }


    public long GenerateNodeId()
    {
        long result = _rand.NextInt64(long.MaxValue);
        while (_cache.ContainsKey(result))
            result = _rand.NextInt64(long.MaxValue);

        return result;
    }
}