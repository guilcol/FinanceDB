using System.Runtime.InteropServices.ComTypes;

namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestInitialBalanceIsZero()
    {
        var db = CreateRecordStorage();
        Assert.AreEqual((decimal)0, db.GetBalance("", new RecordKey("", DateTime.UtcNow, 0)), "Invalid balance");

    }
    
    [TestMethod]
    public void TestBasicInsert()
    {
        var db = CreateRecordStorage();

        var key1 = new RecordKey("", DateTime.UtcNow, 0);
        var record1 = new Record(key1, "test", (decimal)123.45);
        db.Insert(record1);
        
        var key2 = new RecordKey("", DateTime.UtcNow, 1);
        var record2 = new Record(key2, "test", (decimal)678.90);
        db.Insert(record2);

        Assert.AreEqual((decimal)(123.45 + 678.90), db.GetBalance("", key2), "Invalid balance");

        var key3 = new RecordKey("", DateTime.UtcNow, 3);
        var record3 = new Record(key3, "test", (decimal)12.0);
        db.Insert(record3);

        Assert.AreEqual((decimal)(123.45 + 678.90 + 12.0), db.GetBalance("", key3), "Invalid balance");
        
        var insert = db.Insert(record3);
        Assert.IsFalse(insert, "Insert does not catch duplicates");

    }

    [TestMethod]
    public void TestBasicDelete()
    {
        var db = CreateRecordStorage();
        
        var key1 = new RecordKey("", DateTime.UtcNow, 1);
        var record1 = new Record(key1, "test", (decimal)123.45);
        db.Insert(record1);
        db.Delete(record1);
        
        Assert.AreEqual((int) 0, db.RecordCount(), "Delete doesn't work" );
        
        var key2 = new RecordKey("", DateTime.UtcNow, 10);
        var record2 = new Record(key2, "test", (decimal)123.45);

        var deleted = db.Delete(record2); 
        
        Assert.IsFalse(deleted, "Delete method not catching non-existent records");
    }

    [TestMethod]
    public void TestBasicUpdate()
    {
        
        var db = CreateRecordStorage();

        RecordKey key = new RecordKey("", DateTime.UtcNow, 0);

        var record1 = new Record(key, "test", (decimal)123.45);

        var record2 = new Record(key, "blagonga", (decimal)123.45);
        
        var record3 = new Record(key.WithSequence(5), "blagonga", (decimal)123.45);
        
        db.Insert(record1);

        var updated = db.Update(record2); 
        
        Assert.IsTrue(updated);
        
        Assert.AreEqual(db.Read(key).GetDescription(), "blagonga", "Update did not work.");

        updated = db.Update(record3);
        
        Assert.IsFalse(updated, "Update method not catching non-existent records");
    }
    
    /*

    [TestMethod]
    public void TestUpdatePerformance()
    {
        var baseTime = DateTime.UtcNow;
        var db = CreateRecordStorage();

        const int testSize = 1_000_000; 
        // Add 100k records in order.
        for (int i = 0; i < testSize; ++i)
        {
            var key = new RecordKey("", baseTime+TimeSpan.FromSeconds(i), 0);
            AddRandomRecord(db, key);
        }
        
        // Update 100,000 in random order.
        var rnd = new Random(0);
        for (int i = 0; i < testSize; ++i)
        {
            var index = rnd.Next(10000);
            var date = baseTime + TimeSpan.FromSeconds(index);
            var key = new RecordKey("", date, 0); 
            var updated = db.Update(new Record(key, "", rnd.Next(testSize)));
            Assert.IsTrue(updated, "Update failure.");
        }


    }

    [TestMethod]
    public void TestSavingToJSON()
    {
        var baseTime = DateTime.UtcNow;
        var db = CreateRecordStorage();
        
        // Add 100 records in order.
        for (int i = 0; i < 100; ++i)
        {
            var key = new RecordKey("", baseTime+TimeSpan.FromSeconds(i), 0);
            AddRandomRecord(db, key);
        }
        db.Save();
    }

    [TestMethod]
    public void TestAdjustKey()
    {
        var db = CreateRecordStorage();
        var date = DateTime.UtcNow;
        
        var keyA = new RecordKey("A", date, 0);
        var keyB = new RecordKey("B", date, 1);
        var keyC = new RecordKey("C", date, 2);
        var keyD = new RecordKey("D", date, 1);
        var keyE = new RecordKey("E", date, 1);
        var keyC2 = new RecordKey("C", date, 0);
        var keyF = new RecordKey("F", date, uint.MaxValue);
        var keyF2 = new RecordKey("F", date, 0);

        db.Insert(new Record(keyB, "", 0));
        db.Insert(new Record(keyC, "", 0));
        db.Insert(new Record(keyD, "", 0));

        keyE = db.AdjustKey(keyE);
        Assert.AreEqual(1u, keyE.Sequence, "KeyE wrong sequence");

        keyC2 = db.AdjustKey(keyC2);
        Assert.AreEqual(3u, keyC2.Sequence, "KeyC2 wrong sequence");

        keyA = db.AdjustKey(keyA);
        Assert.AreEqual(0u, keyA.Sequence, "KeyA wrong sequence");

        db.Insert(new Record(keyF, "", 0));
        
        try
        {
            db.AdjustKey(keyF2);
            throw new AssertFailedException("Expected exception was not thrown");
        }
        catch (Exception e)
        {
            Assert.IsTrue(e.Message.Contains("Too many records on the same account and date."));
        }
    }

    [TestMethod]
    public void TestContainsKey()
    {
        var key = new RecordKey("", DateTime.UtcNow, 0);
        var record = new Record(key, "", 0);
        var db = CreateRecordStorage();
        db.Insert(record);
        Assert.IsTrue(db.ContainsKey(key), "ContainsKey does not work");
        Assert.IsFalse(db.ContainsKey(key.WithSequence(2)), "ContainsKey with missing key did not work.");
    }

    [TestMethod]
    public void TestList()
    {
        var keyA = new RecordKey("A", DateTime.UtcNow, 0);
        var recordA = new Record(keyA, "", 0);
        
        var keyB = new RecordKey("B", DateTime.UtcNow, 0);
        var recordB = new Record(keyB, "", 0);

        var db = CreateRecordStorage();

        db.Insert(recordA);
        db.Insert(recordB);

        var testList = db.List("A");
        
        Assert.AreEqual(1, testList.Count, "List not displaying correct count");
    }

    [TestMethod]
    public void TestRead()
    {
        var date = DateTime.UtcNow;
        var key1 = new RecordKey("", date, 0);
        var record1 = new Record(key1, "", 0);

        var db = CreateRecordStorage();

        db.Insert(record1);

        var readRecord = db.Read(key1);
        Assert.AreEqual(record1.Key, readRecord.Key);
        
        var missingRecord = new Record(key1.WithSequence(5), "", 0);

        var readMissingRecord = db.Read(missingRecord.Key);
        Assert.IsNull(readMissingRecord, "Read method not catching non-existent records");
    }

    [TestMethod]
    public void TestLoadDatabase()
    {
        var db = CreateRecordStorage();

        var key = new RecordKey("", DateTime.UtcNow, 0);
        var record1 = new Record(key, "", 123.50m);

        db.Insert(record1);
        db.Save();

        var db2 = CreateRecordStorage();
        db2.Load();
        
        Assert.IsTrue(db2.ContainsKey(key));
        
    }
    */

    private BasicRs CreateRecordStorage()
    {
        return new BasicRs("db.json");
    }
    
    private void AddRandomRecord(IRecordStorage db, RecordKey key)
    {
        var record = new Record(key, Guid.NewGuid().ToString(), new Random(0).Next(1_000_000));
        db.Insert(record);
    }
    
}