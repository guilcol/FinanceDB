namespace FinanceDB.UnitTest;

[TestClass]
public class UnitTest2
{
    [TestMethod]
    public void TestInsertOnEmptyRoot()
    {
        BTreeRs db = new BTreeRs();
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
        BTreeRs db = new BTreeRs();
        RecordKey key = new RecordKey("", DateTime.UtcNow, 0);
        Record record = new Record(key, "", 49);
        
        db.Insert(record);
        db.Delete(record);
        
        Assert.IsFalse(db.ContainsKey(key));
        
        
    }
    
    [TestMethod]
    public void TestUpdate()
    {
        BTreeRs db = CreateSmallDatabase();
        RecordKey key = new RecordKey("", DateTime.UtcNow, 0);
        Record record = new Record(key, "", 49);
        Record updatedRecord = new Record(key, "", 500);
        
        db.Insert(record);

        

    }
    
        public BTreeRs CreateSmallDatabase()
    {
        // Universal key
        RecordKey key = new RecordKey("", DateTime.UtcNow, 1);

        // Leaf node 1
        Record rec1a = new Record(key.WithSequence(1), "", 0);
        Record rec1b = new Record(key.WithSequence(5), "", 0);
        Record rec1c = new Record(key.WithSequence(10), "", 0);
        
        List<Record> records1 = new List<Record>();
        
        records1.Add(rec1a);
        records1.Add(rec1b);
        records1.Add(rec1c);
        
        // Leaf node 2
        Record rec2a = new Record(key.WithSequence(20), "", 0);
        Record rec2b = new Record(key.WithSequence(22), "", 0);
        Record rec2c = new Record(key.WithSequence(30), "", 0);
        
        List<Record> records2 = new List<Record>();
        
        records2.Add(rec2a);
        records2.Add(rec2b);
        records2.Add(rec2c);
        
        // Leaf node 3
        Record rec3a = new Record(key.WithSequence(40), "", 0);
        Record rec3b = new Record(key.WithSequence(42), "", 0);
        Record rec3c = new Record(key.WithSequence(44), "", 0);
        Record rec3d = new Record(key.WithSequence(50), "", 0);

        
        List<Record> records3 = new List<Record>();
        
        records3.Add(rec3a);
        records3.Add(rec3b);
        records3.Add(rec3c);
        records3.Add(rec3d);
        
        // Root refs
        BTreeNodeReference rootRef1 = new BTreeNodeReference(rec1a.Key, rec1c.Key, 1);
        BTreeNodeReference rootRef2 = new BTreeNodeReference(rec2a.Key, rec2c.Key, 2);
        BTreeNodeReference rootRef3 = new BTreeNodeReference(rec3a.Key, rec3d.Key, 3);
        
        List<BTreeNodeReference> childrenRef = new List<BTreeNodeReference>();
        childrenRef.Add(rootRef1);
        childrenRef.Add(rootRef2);
        childrenRef.Add(rootRef3);
        
        BTreeNode root = new BTreeNode(0, false, null, childrenRef);
        BTreeNode node1 = new BTreeNode(1, true, records1, null);
        BTreeNode node2 = new BTreeNode(2, true, records2, null);
        BTreeNode node3 = new BTreeNode(3, true, records3, null);

        BTreeRs db = new BTreeRs();

        return db;
    }
    

}