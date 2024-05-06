namespace FinanceDB;

public class MemoryNodeStorage : INodeStorage
{
    public BTreeNode Get(long nodeId)
    {
        throw new NotImplementedException();
    }

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