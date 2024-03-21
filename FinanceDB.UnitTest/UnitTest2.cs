using System.Runtime.InteropServices.ComTypes;

namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest2
{
    [TestMethod]
    public void TestInsertOnEmptyRoot()
    {
        BTreeRs db = new BTreeRs(new Random(0));
        RecordKey key = new RecordKey("", DateTime.UtcNow, 0);
        Record record = new Record(key, "", 49);
        
        db.Insert(record);
        
        Assert.IsTrue(db.ContainsKey(key));
    }

    [TestMethod]
    public void TestBTreeNodeReferenceBinarySearch()
    {
        List<BTreeNodeReference> ChildReferences = new List<BTreeNodeReference>();
        DateTime date = DateTime.UtcNow;
        RecordKey key = new RecordKey("", date, 0);
        
        for (int i = 1; i < 100_000; i++)
        {
            ChildReferences.Add(new BTreeNodeReference(key.WithSequence((uint)i*10), key.WithSequence((uint)(i*10)+9), 0));
        } 
        RecordKey key2 = key.WithSequence(1000008);

        BTreeNode node = new BTreeNode(0, false, null, ChildReferences);

        int index = node.FindChildReference(key2);
        
        Assert.AreEqual(99999, index);
    }
    
    [TestMethod]
    public void TestDelete()
    {
        BTreeRs db = new BTreeRs(new Random(0));
        RecordKey key = new RecordKey("", DateTime.UtcNow, 0);
        Record record = new Record(key, "", 49);
        
        db.Insert(record);
        db.Delete(record);
        
        Assert.IsFalse(db.ContainsKey(key));
        
        
    }
    
    [TestMethod]
    public void TestUpdate()
    {
        //todo: implement
    }

    [TestMethod]
    public void TestSaveSplitCache()
    {
        
        // Initialize database, Random object, and a constant DateTime date.
        BTreeRs db = new BTreeRs(new Random(0));
        Random rand = new Random(0);
        DateTime date = DateTime.UtcNow;

        // Add 10 random records to the database, this will case an overflow error
        for (int i = 0; i < 100; i++)
        {
            // Create a key of random sequence
            RecordKey key = new RecordKey("", date, (uint)rand.Next(9000));
            
            // Ascertain key is unique
            while (db.ContainsKey(key)) 
                key.WithSequence((uint)rand.Next(9000));

            // Insert Record with key in database
            db.Insert(new Record(key, "", 0));
        }
        
        db.Save();
        
        Console.Write("ong");
        
        Assert.AreEqual(5, db.CacheLength());

    }
}