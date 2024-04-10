using System.Runtime.InteropServices.ComTypes;

namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest2
{

    [TestMethod]
    public void TestInsert()
    {
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 4);

        Random rand = new Random();
        
        // Generate random keys to be inserted under different account IDs
        List<RecordKey> randomKeysEarth = GenerateRandomRecordKeys("Earth", 20, 9000);
        List<RecordKey> randomKeysVenus  = GenerateRandomRecordKeys("Venus", 20, 9000);
        List<RecordKey> randomKeysJupiter = GenerateRandomRecordKeys("Jupiter", 20, 9000);
     
        // Add all new records to the database
        foreach (var item in randomKeysEarth)
            db.Insert(new Record(item, "", rand.Next(-200, 1000)));
        foreach (var item in randomKeysVenus)
            db.Insert(new Record(item, "", rand.Next(-200, 1000)));
        foreach (var item in randomKeysJupiter)
            db.Insert(new Record(item, "", rand.Next(-200, 1000)));

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
        bool deleteEarthRecord = db.Delete(randomEarthRecord);
        bool deleteJupiterRecord = db.Delete(randomJupiterRecord);
        bool deleteVenusRecord = db.Delete(randomVenusRecord);
        
        // Save and build tree
        db.Save();
        
        // Assert records are no longer found in database
        Assert.IsTrue(deleteEarthRecord);
        Assert.IsTrue(deleteJupiterRecord);
        Assert.IsTrue(deleteVenusRecord);

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
    public void TestBalance()
    {
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 20);

        Random rand = new Random();
        
        // Generate random keys to be inserted under different account IDs
        List<RecordKey> randomKeysEarth = GenerateRandomRecordKeys("Earth", 200, 9000);
        
        // Store correct result
        decimal correctResult = 0;
     
        // Add all new records to the database
        foreach (var item in randomKeysEarth)
        {
            decimal randomAmount = (decimal)rand.Next(-200, 1000);
            db.Insert(new Record(item, "", randomAmount));
            correctResult += randomAmount;

        }

        db.Save();
        
        Assert.AreEqual(correctResult, db.GetBalance("Earth"));
    }

    [TestMethod]
    public void TestGetBalance()
    {
        BTreeRs db = new BTreeRs(new Random(0), 20);
        List<RecordKey> randomKeysAnubis = GenerateRandomRecordKeys("Anubis", 2000, 100_000_000);
        Random rand = new Random();
        
        foreach (RecordKey key in randomKeysAnubis)
        {
            double minValue = -100.00;
            double maxValue = 9000.00;
            decimal randomValue = (decimal)(rand.NextDouble() * (maxValue - minValue) + minValue);
            db.Insert(new Record(key, "", randomValue));
        }
        
        db.Save();

        List<Record> AllRecords = new List<Record>();
        AllRecords.AddRange(db.List("Anubis") ?? throw new InvalidOperationException("Null records"));
        
        RecordKey randomKey = AllRecords[rand.Next(AllRecords.Count)].Key;
        decimal balanceFromList = RetrieveBalanceFromList(AllRecords, randomKey);
        
        Assert.AreEqual(balanceFromList, db.GetBalance("Anubis", randomKey));
    }

    private decimal RetrieveBalanceFromList(List<Record> allRecords, RecordKey randomKey)
    {
        decimal balance = 0;
        
        foreach (Record record in allRecords)
        {
            balance += record.GetAmount();
            if (record.Key == randomKey)
                break;
        }

        return balance;
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
        
        Assert.AreEqual(517, db.GetCacheLengthFromAccountId(""));

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