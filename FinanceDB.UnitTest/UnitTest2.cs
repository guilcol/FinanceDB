using System.Runtime.InteropServices.ComTypes;

namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest2
{

    [TestMethod]
    public void TestInsert()
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

        RecordKey newKey = new RecordKey("Earth", DateTime.UtcNow, 0);
        Record newRecord = new Record(newKey, "", 0);

        db.Insert(newRecord);
        db.Save();
        
        Assert.IsTrue(db.ContainsKey(newKey));
    }
    
    [TestMethod]
    public void TestDelete()
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

        // Save database and build tree
        db.Save();
        
        // Obtain list of records from each accountId
        IReadOnlyList<Record>? earthRecords = db.List("Earth");
        IReadOnlyList<Record>? jupiterRecords = db.List("Jupiter");
        IReadOnlyList<Record>? venusRecords = db.List("Venus");
        
        Random rand = new Random();

        // Pick out one random record from each accountId
        Record randomEarthRecord = earthRecords[rand.Next(earthRecords.Count-1)];
        Record randomJupiterRecord = jupiterRecords[rand.Next(jupiterRecords.Count-1)];
        Record randomVenusRecord = venusRecords[rand.Next(venusRecords.Count-1)];

        // Delete records
        db.Delete(randomEarthRecord);
        db.Delete(randomJupiterRecord);
        db.Delete(randomVenusRecord);
        
        // Save and build tree
        db.Save();
        
        // Assert records are no longer found in database
        Assert.IsFalse(db.ContainsKey(randomEarthRecord.Key));
        Assert.IsFalse(db.ContainsKey(randomJupiterRecord.Key));
        Assert.IsFalse(db.ContainsKey(randomVenusRecord.Key));
    }
    
    [TestMethod]
    public void TestUpdate()
    {
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 3);
        
        // Generate random keys to be inserted under different account IDs
        List<RecordKey> randomKeysApple = GenerateRandomRecordKeys("Apple", 5, 9000);
        List<RecordKey> randomKeysBanana  = GenerateRandomRecordKeys("Banana", 5, 9000);
        List<RecordKey> randomKeysMango = GenerateRandomRecordKeys("Mango", 5, 9000);
     
        // Add all new records to the database
        foreach (var item in randomKeysApple)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysBanana)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysMango)
            db.Insert(new Record(item, "", 0));

        // Save database and build tree
        db.Save();
        
        // Obtain list of records from each accountId
        IReadOnlyList<Record>? appleRecords = db.List("Apple");
        IReadOnlyList<Record>? bananaRecords = db.List("Banana");
        IReadOnlyList<Record>? mangoRecords = db.List("Mango");
        
        Random rand = new Random();

        // Pick out one random record from each accountId
        Record randomAppleRecord = appleRecords[rand.Next(appleRecords.Count)];
        Record randomBananaRecord = bananaRecords[rand.Next(bananaRecords.Count)];
        Record randomMangoRecord = mangoRecords[rand.Next(mangoRecords.Count)];

        Record updatedRecord = new Record(randomBananaRecord.Key, "", 425);
        // Delete records
        db.Update(updatedRecord);

        Record retrievedUpdatedRecord = db.Read(updatedRecord.Key);
        
        Assert.AreEqual(425, retrievedUpdatedRecord.GetAmount());
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
        
        IReadOnlyList<Record>? earthRecords = db.List("Earth");
        IReadOnlyList<Record>? jupiterRecords = db.List("Jupiter");
        IReadOnlyList<Record>? venusRecords = db.List("Venus");
        
        Assert.AreEqual(5, earthRecords.Count);
        Assert.AreEqual(5, jupiterRecords.Count);
        Assert.AreEqual(5, venusRecords.Count);
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
        
        RecordKey key2 = key.WithSequence(10_000_000);
        ChildReferences.Add(new BTreeNodeReference(key2, key2.WithSequence(11_000_000), 0));
        BTreeNode node = new BTreeNode(0, false, null, ChildReferences);

        int index = node.FindChildReference(key2);
        
        Assert.AreEqual(99999, index);
    }

    [TestMethod]
    public void TestSaveSplitCache()
    {
        
        // Initialize database, Random object, and a constant DateTime date.
        BTreeRs db = new BTreeRs(new Random(0), 200);
        Random rand = new Random(0);
        DateTime date = DateTime.UtcNow;
 
        // Add 10 random records to the database, this will case an overflow error
        for (int i = 0; i < 100000; i++)
        { 
            // Create a key of random sequence
            RecordKey key = new RecordKey("", date, (uint)rand.Next(100_000_000));
 
            // Ascertain key is unique
            while (db.ContainsKey(key)) 
                key = key.WithSequence((uint)rand.Next(100_000_000));

            // Insert Record with key in database
            db.Insert(new Record(key, "", 0));
        }
        
        db.Save();
        
        Assert.AreEqual(517, db.CacheLength());

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
}