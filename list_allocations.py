import sqlite3
db = r"IOCLCommunityHall\iocl_community_hall.db"
conn = sqlite3.connect(db)
conn.row_factory = sqlite3.Row
cur = conn.cursor()

print("=" * 60)
print("ALL INVENTORY ALLOCATIONS")
print("=" * 60)
cur.execute("""
    SELECT a.AllocationId, a.RequestId, a.InventoryItemId, i.Name, a.AllocatedQuantity,
           a.StartDate, a.EndDate, a.Status, a.AllocationDate
    FROM InventoryAllocations a
    JOIN InventoryItems i ON a.InventoryItemId = i.Id
    ORDER BY a.InventoryItemId, a.StartDate
""")
for r in cur.fetchall():
    print(f"AllocId={r['AllocationId']} | ReqId={r['RequestId']} | Item={r['Name']} (ID={r['InventoryItemId']}) | Qty={r['AllocatedQuantity']} | Dates={r['StartDate'][:10]} to {r['EndDate'][:10]} | Status={r['Status']} | DateCreated={r['AllocationDate']}")
conn.close()
