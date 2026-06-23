import sqlite3
db = r"iocl_community_hall.db"
conn = sqlite3.connect(db)
conn.row_factory = sqlite3.Row
cur = conn.cursor()

print("=" * 60)
print("ALL RENTAL REQUESTS")
print("=" * 60)
cur.execute("""
    SELECT r.Id, r.RequestNumber, r.UserId, u.FullName, r.Status, r.ApprovalStage,
           r.StartDate, r.EndDate, r.GrandTotal
    FROM RentalRequests r
    JOIN AspNetUsers u ON r.UserId = u.Id
    ORDER BY r.Id
""")
requests = cur.fetchall()
for req in requests:
    print(f"ID={req['Id']}: {req['RequestNumber']} | User={req['FullName']} | Status={req['Status']} | Stage={req['ApprovalStage']} | Dates={req['StartDate'][:10]} to {req['EndDate'][:10]} | Total={req['GrandTotal']}")
    cur.execute("""
        SELECT ri.InventoryItemId, i.Name, ri.RequestedQuantity, ri.AllocatedQuantity, ri.Status
        FROM RentalRequestItems ri
        JOIN InventoryItems i ON ri.InventoryItemId = i.Id
        WHERE ri.RentalRequestId = ?
    """, (req['Id'],))
    for item in cur.fetchall():
        print(f"  - Item: {item['Name']} (ID={item['InventoryItemId']}) | Requested={item['RequestedQuantity']} | Allocated={item['AllocatedQuantity']} | Status={item['Status']}")

conn.close()
