import sqlite3
db = r"iocl_community_hall.db"
conn = sqlite3.connect(db)
conn.row_factory = sqlite3.Row
cur = conn.cursor()

print("=" * 80)
print("USERS AND ROLES WITH DETAILS")
print("=" * 80)
cur.execute("""
    SELECT u.Id, u.UserName, u.Email, u.FullName, u.Department, u.EmployeeId, r.Name as RoleName
    FROM AspNetUsers u
    LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
    LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
""")
for row in cur.fetchall():
    print(f"User: {row['FullName']} ({row['Email']}) | Username: {row['UserName']} | Dept: {row['Department']} | EmpId: {row['EmployeeId']} | Role: {row['RoleName']}")
conn.close()
