using FinanceDB.Core.Storage;

namespace FinanceDB.Core.Interfaces;

public interface INodeStorage
{
    BTreeNode Get(long nodeId);
    void Put(BTreeNode node);
    void Delete(BTreeNode node);
    ICollection<BTreeNode> List();
    int CacheLength();
    void Save();
    long GenerateNodeId();
}
