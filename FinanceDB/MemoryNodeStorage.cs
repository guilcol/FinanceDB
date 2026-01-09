using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace FinanceDB;

public class MemoryNodeStorage : INodeStorage
{
    private readonly Random _rand;
    private readonly Dictionary<long, BTreeNode> _cache = new();
    private readonly Dictionary<long, BTreeNode> memory = new();

    private readonly string path = "FinanceDB\\Nodes";
    public BTreeNode Get(long nodeId)
    {
        if (_cache.ContainsKey(nodeId))
        {
            return _cache[nodeId];
        }

        if (memory.ContainsKey(nodeId))
        {
            return memory[nodeId];
        }
        Put(memory[nodeId]); 
        
        string fileName = $"{nodeId}.json";
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            return null;
        string fileContent = File.ReadAllText(fullPath);
        BTreeNode nodeFromFile = JsonConvert.DeserializeObject<BTreeNode>(fileContent);
        Put(nodeFromFile);
        return nodeFromFile;    }

    public void Put(BTreeNode node)
    {
        throw new NotImplementedException();
    }

    public void Delete(BTreeNode node)
    {
        throw new NotImplementedException();
    }

    public ICollection<BTreeNode> List()
    {
        throw new NotImplementedException();
    }

    public int CacheLength()
    {
        throw new NotImplementedException();
    }

    public void Save()
    {
        throw new NotImplementedException();
    }

    public long GenerateNodeId()
    {
        throw new NotImplementedException();
    }
}