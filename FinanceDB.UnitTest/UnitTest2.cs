using System.Runtime.InteropServices.ComTypes;

namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest2
{

    [TestMethod]
    public void TestInsert()
    {
        
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 2);
        
        // Generate random keys to be inserted
        List<RecordKey> randomkeys = GenerateRandomRecordKeys("", 10, 9000);
        
        // Insert keys
        foreach (var item in randomkeys)
            db.Insert(new Record(item, "", 0));

        // Verify keys exist
        foreach (var item in randomkeys)
            Assert.IsTrue(db.ContainsKey(randomkeys[0]));
        
    }
    
    [TestMethod]
    public void TestList()
    {
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 3);
        
        // Generate random keys to be inserted under different account IDs
        List<RecordKey> randomKeysEarth = GenerateRandomRecordKeys("Earth", 5, 9000);
        List<RecordKey> randomKeysVenus  = GenerateRandomRecordKeys("Venus", 5, 9000);
        List<RecordKey> randomKeysJupiter = GenerateRandomRecordKeys("Jupiter", 5, 9000);
     
        // Add all new records to the database
        foreach (var item in randomKeysEarth)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysVenus)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysJupiter)
            db.Insert(new Record(item, "", 0));

        db.Save();
        
        IReadOnlyList<Record>? jupiterRecords = db.List("Jupiter");
        Console.Write("aga");

    }

    private List<RecordKey> GenerateRandomRecordKeys(string id, int amount, int sequenceCap)
    {
        Random rand = new Random();
        DateTime date = DateTime.UtcNow;
        RecordKey baseKey = new RecordKey(id, date, 0);
        List<RecordKey> result = new List<RecordKey>();
        
        while (amount > 0)
        {
            int index = result.BinarySearch(baseKey);

            while (index >= 0)
            {
                baseKey = baseKey.WithSequence((uint)rand.Next(sequenceCap));
                index = result.BinarySearch(baseKey);
            }
            result.Insert(~index, baseKey);
            amount--;
        }
        return result;
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
        BTreeRs db = new BTreeRs(new Random(0), 5);
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
        BTreeRs db = new BTreeRs(new Random(0), 5);
        Random rand = new Random(0);
        DateTime date = DateTime.UtcNow;

        // Add 10 random records to the database, this will case an overflow error
        for (int i = 0; i < 10000; i++)
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