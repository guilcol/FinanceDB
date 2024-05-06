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
        List<RecordKey> randomKeysFalcon = GenerateRandomRecordKeys("Falcon", 5, 9000);
        List<RecordKey> randomKeysEagle  = GenerateRandomRecordKeys("Eagle", 5, 9000);
        List<RecordKey> randomKeysHawk = GenerateRandomRecordKeys("Hawk", 5, 9000);
     
        // Add all new records to the database
        foreach (var item in randomKeysFalcon)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysEagle)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysHawk)
            db.Insert(new Record(item, "", 0));

        // Save database and build tree
        db.Save();
        
        // Obtain list of records from each accountId
        IReadOnlyList<Record>? falconRecords = db.List("Falcon");
        IReadOnlyList<Record>? eagleRecords = db.List("Eagle");
        IReadOnlyList<Record>? hawkRecords = db.List("Hawk");
        
        Random rand = new Random();

        // Pick out one random record from each accountId
        Record randomFalconRecord = falconRecords[rand.Next(falconRecords.Count-1)];
        Record randomEagleRecord = eagleRecords[rand.Next(eagleRecords.Count-1)];
        Record randomHawkRecord = hawkRecords[rand.Next(hawkRecords.Count-1)];

        // Delete records
        bool deleteFalconRecord = db.Delete(randomFalconRecord);
        bool deleteEagleRecord = db.Delete(randomEagleRecord);
        bool deleteHawkRecord = db.Delete(randomHawkRecord);
        
        // Save and build tree
        db.Save();
        
        // Assert records are no longer found in database
        Assert.IsTrue(deleteFalconRecord);
        Assert.IsTrue(deleteEagleRecord);
        Assert.IsTrue(deleteHawkRecord);

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
        List<RecordKey> randomKeysPolonium = GenerateRandomRecordKeys("Polonium", 5, 9000);
        List<RecordKey> randomKeysCobalt  = GenerateRandomRecordKeys("Cobalt", 5, 9000);
        List<RecordKey> randomKeysUranium = GenerateRandomRecordKeys("Uranium", 5, 9000);
     
        // Add all new records to the database
        foreach (var item in randomKeysPolonium)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysCobalt)
            db.Insert(new Record(item, "", 0));
        foreach (var item in randomKeysUranium)
            db.Insert(new Record(item, "", 0));

        db.Save();
        
        IReadOnlyList<Record>? poloniumRecords = db.List("Polonium");
        IReadOnlyList<Record>? cobaltRecords = db.List("Cobalt");
        IReadOnlyList<Record>? uraniumRecords = db.List("Uranium");
        
        Assert.AreEqual(5, poloniumRecords.Count);
        Assert.AreEqual(5, cobaltRecords.Count);
        Assert.AreEqual(5, uraniumRecords.Count);
    }

    [TestMethod]
    public void TestBalance()
    {
        // Instantiate database
        BTreeRs db = new BTreeRs(new Random(0), 20);

        Random rand = new Random();
        
        // Generate random keys to be inserted under different account IDs
        List<RecordKey> randomKeysAltair = GenerateRandomRecordKeys("Altair", 200, 9000);
        
        // Store correct result
        decimal correctResult = 0;
     
        // Add all new records to the database
        foreach (var item in randomKeysAltair)
        {
            decimal randomAmount = (decimal)rand.Next(-200, 1000);
            db.Insert(new Record(item, "", randomAmount));
            correctResult += randomAmount;

        }

        db.Save();
        
        Assert.AreEqual(correctResult, db.GetBalance("Altair"));
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
    public void CreateJSON()
    {
        BTreeRs db = new BTreeRs(new Random(0), 5);

        Random rand = new Random();

        List<RecordKey> randomKeys = GenerateRandomRecordKeys("Guilherme", 200, 100_000_000);


        foreach (var key in randomKeys)
        {
            db.Insert(new Record(key, "", rand.Next(-100, 9000)));
        }
        
        db.Save();
        
        Assert.IsTrue(File.Exists("BTreeJSON"));
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