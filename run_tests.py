import requests
import sqlite3
import re
from datetime import datetime, date

BASE_URL = "http://localhost:5000"
DB_PATH = r"iocl_community_hall.db"

def extract_token(html):
    match = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
    if not match:
        match = re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', html)
    if not match:
        match = re.search(r'<input[^>]*?name="__RequestVerificationToken"[^>]*?value="([^"]+)"', html)
    return match.group(1) if match else None

def get_db_connection():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn

def check_item_db(item_id):
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute("SELECT Name, TotalQuantity, ReservedQuantity FROM InventoryItems WHERE Id = ?", (item_id,))
    item = dict(cur.fetchone())
    conn.close()
    return item

def check_allocations_db(request_id):
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute("SELECT * FROM InventoryAllocations WHERE RequestId = ?", (request_id,))
    allocs = [dict(r) for r in cur.fetchall()]
    conn.close()
    return allocs

def check_request_db(request_id):
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute("""SELECT Status, ApprovalStage, HODApprovedByEmployeeId,
                          GMApprovedByEmployeeId, HRApprovedByEmployeeId
                   FROM RentalRequests WHERE Id = ?""", (request_id,))
    req = dict(cur.fetchone())
    conn.close()
    return req

def login(session, employee_id, password):
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    if not token:
        raise Exception("Failed to extract CSRF token from login page")
    login_data = {
        "employeeId": employee_id,
        "password": password,
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    resp = session.post(f"{BASE_URL}/Account/Login", data=login_data)
    if "Login" in resp.url:
        raise Exception(f"Login failed for {employee_id}")
    return resp

def logout(session, html_with_token):
    token = extract_token(html_with_token)
    if token:
        session.post(f"{BASE_URL}/Account/Logout", data={"__RequestVerificationToken": token})

def create_rental_request(session, item_id, qty, unit_price, days=1):
    """Creates a rental request and returns (request_id, reserved_before)."""
    create_page = session.get(f"{BASE_URL}/RentalRequest/Create")
    token = extract_token(create_page.text)

    from datetime import datetime, timedelta
    start = (datetime.now() + timedelta(days=3)).strftime("%Y-%m-%d")
    end   = (datetime.now() + timedelta(days=3 + days - 1)).strftime("%Y-%m-%d")

    req_data = {
        "EventDate":  start,
        "StartDate":  start,
        "EndDate":    end,
        f"RequestItems[0].InventoryItemId": str(item_id),
        f"RequestItems[0].RequestedQuantity": str(qty),
        f"RequestItems[0].CurrentPrice": str(unit_price),
        "__RequestVerificationToken": token
    }
    files = {"InPrincipalDocumentFile": ("dummy.pdf", b"%PDF-1.4 dummy content", "application/pdf")}
    session.post(f"{BASE_URL}/RentalRequest/Create", data=req_data, files=files)

    my_req_page = session.get(f"{BASE_URL}/RentalRequest/MyRequests")
    matches = re.findall(r'name="id"\s+value="(\d+)"', my_req_page.text)
    if not matches:
        matches = re.findall(r'/Details/(\d+)', my_req_page.text)
    if not matches:
        raise Exception("Could not find newly created request ID")
    return max(int(m) for m in matches), my_req_page.text

def approve_request(session, request_id):
    details_page = session.get(f"{BASE_URL}/AdminRequest/Details/{request_id}")
    token = extract_token(details_page.text)
    resp = session.post(f"{BASE_URL}/AdminRequest/Approve/{request_id}", data={"__RequestVerificationToken": token})
    return resp

def run_tests():
    import sys
    sys.stdout.reconfigure(encoding='utf-8')

    # ─────────────────────────────────────────────────────────────────
    # SCENARIO A: Employee ≤ ₹10,000  →  HOD → SuperAdmin (skip GM)
    # Item 12 (LED Light String) at ₹50/unit × 10 units × 1 day = ₹500
    # ─────────────────────────────────────────────────────────────────
    print("\n" + "="*60)
    print("SCENARIO A: Employee ≤₹10,000 → HOD → SuperAdmin (skip GM)")
    print("="*60)

    session = requests.Session()
    login(session, "10000001", "Admin@1234")
    print("[+] Logged in as Employee Rajesh Sharma (10000001)")

    item_before_a = check_item_db(12)
    request_id_a, my_req_html = create_rental_request(session, item_id=12, qty=10, unit_price=50.0, days=1)
    # Grand total = 10 × ₹50 × 1 day = ₹500 (≤ ₹10,000 → should skip GM)
    db = check_request_db(request_id_a)
    print(f"[+] Created Request #{request_id_a}  |  Status={db['Status']} (0=Pending), Stage={db['ApprovalStage']} (0=PendingHOD)")
    assert db['ApprovalStage'] == 0, f"FAIL: Expected PendingHOD (0), got {db['ApprovalStage']}"

    # HOD approves
    logout(session, my_req_html)
    login(session, "30000001", "Hod@1234")
    print("[+] Logged in as HOD Arjun Mehta (30000001)")
    resp_hod = approve_request(session, request_id_a)
    db = check_request_db(request_id_a)
    print(f"    After HOD Approval: Status={db['Status']}, Stage={db['ApprovalStage']} (expected 2=PendingHR — GM skipped)")
    assert db['ApprovalStage'] == 2, f"FAIL: Expected PendingHR (2) after HOD for ≤₹10k, got {db['ApprovalStage']}"

    # SuperAdmin approves (final)
    logout(session, resp_hod.text)
    login(session, "00000001", "Admin@1234")
    print("[+] Logged in as SuperAdmin (00000001)")
    resp_sa = approve_request(session, request_id_a)
    db = check_request_db(request_id_a)
    print(f"    After SuperAdmin Approval: Status={db['Status']} (1=Approved), Stage={db['ApprovalStage']} (3=Approved)")
    assert db['Status'] == 1,          f"FAIL: Expected Status=Approved (1), got {db['Status']}"
    assert db['ApprovalStage'] == 3,   f"FAIL: Expected Stage=Approved (3), got {db['ApprovalStage']}"
    assert not db['GMApprovedByEmployeeId'], f"FAIL: GM should NOT have approved (got {db['GMApprovedByEmployeeId']})"
    print("[PASS] SCENARIO A: Employee ≤₹10k correctly routed HOD → SuperAdmin (skipped GM)")

    # Cancel to free inventory
    logout(session, resp_sa.text)
    login(session, "10000001", "Admin@1234")
    mr = session.get(f"{BASE_URL}/RentalRequest/MyRequests")
    token = extract_token(mr.text)
    session.post(f"{BASE_URL}/RentalRequest/Cancel", data={"id": request_id_a, "__RequestVerificationToken": token})

    # ─────────────────────────────────────────────────────────────────
    # SCENARIO B: Employee > ₹10,000  →  HOD → GM → SuperAdmin
    # Item 12 (LED Light String) at ₹50/unit × 10 units × 30 days = ₹15,000
    # ─────────────────────────────────────────────────────────────────
    print("\n" + "="*60)
    print("SCENARIO B: Employee >₹10,000 → HOD → GM → SuperAdmin")
    print("="*60)

    session = requests.Session()
    login(session, "10000001", "Admin@1234")
    print("[+] Logged in as Employee Rajesh Sharma (10000001)")

    item_before_b = check_item_db(12)
    # 10 × ₹50 × 30 days = ₹15,000 > ₹10,000 → should involve GM
    request_id_b, my_req_html = create_rental_request(session, item_id=12, qty=10, unit_price=50.0, days=30)
    db = check_request_db(request_id_b)
    print(f"[+] Created Request #{request_id_b}  |  Status={db['Status']}, Stage={db['ApprovalStage']} (0=PendingHOD)")
    assert db['ApprovalStage'] == 0, f"FAIL: Expected PendingHOD (0), got {db['ApprovalStage']}"

    # HOD approves → should go to GM (not HR)
    logout(session, my_req_html)
    login(session, "30000001", "Hod@1234")
    print("[+] Logged in as HOD Arjun Mehta (30000001)")
    resp_hod = approve_request(session, request_id_b)
    db = check_request_db(request_id_b)
    print(f"    After HOD Approval: Status={db['Status']}, Stage={db['ApprovalStage']} (expected 1=PendingGM)")
    assert db['ApprovalStage'] == 1, f"FAIL: Expected PendingGM (1) after HOD for >₹10k, got {db['ApprovalStage']}"

    # GM approves → should go to SuperAdmin
    logout(session, resp_hod.text)
    login(session, "20000001", "Gm@12345")
    print("[+] Logged in as GM Suresh Patel (20000001)")
    resp_gm = approve_request(session, request_id_b)
    db = check_request_db(request_id_b)
    print(f"    After GM Approval: Status={db['Status']}, Stage={db['ApprovalStage']} (expected 2=PendingHR)")
    assert db['ApprovalStage'] == 2, f"FAIL: Expected PendingHR (2) after GM, got {db['ApprovalStage']}"

    # SuperAdmin approves (final)
    logout(session, resp_gm.text)
    login(session, "00000001", "Admin@1234")
    print("[+] Logged in as SuperAdmin (00000001)")
    resp_sa = approve_request(session, request_id_b)
    db = check_request_db(request_id_b)
    print(f"    After SuperAdmin Approval: Status={db['Status']} (1=Approved), Stage={db['ApprovalStage']} (3=Approved)")
    assert db['Status'] == 1,        f"FAIL: Expected Approved (1), got {db['Status']}"
    assert db['ApprovalStage'] == 3, f"FAIL: Expected Approved stage (3), got {db['ApprovalStage']}"
    assert db['GMApprovedByEmployeeId'], "FAIL: GM should have approved"
    print("[PASS] SCENARIO B: Employee >₹10k correctly routed HOD → GM → SuperAdmin")

    # Cancel to free inventory
    logout(session, resp_sa.text)
    login(session, "10000001", "Admin@1234")
    mr = session.get(f"{BASE_URL}/RentalRequest/MyRequests")
    token = extract_token(mr.text)
    session.post(f"{BASE_URL}/RentalRequest/Cancel", data={"id": request_id_b, "__RequestVerificationToken": token})

    # ─────────────────────────────────────────────────────────────────
    # SCENARIO C: GM submits → Always SuperAdmin (any amount)
    # ─────────────────────────────────────────────────────────────────
    print("\n" + "="*60)
    print("SCENARIO C: GM submits → SuperAdmin (any amount)")
    print("="*60)

    session = requests.Session()
    login(session, "20000001", "Gm@12345")
    print("[+] Logged in as GM Suresh Patel (20000001)")

    request_id_c, my_req_html = create_rental_request(session, item_id=12, qty=5, unit_price=50.0, days=1)
    db = check_request_db(request_id_c)
    print(f"[+] Created Request #{request_id_c}  |  Status={db['Status']}, Stage={db['ApprovalStage']} (expected 2=PendingHR)")
    assert db['ApprovalStage'] == 2, f"FAIL: GM request should start at PendingHR (2), got {db['ApprovalStage']}"
    assert not db['HODApprovedByEmployeeId'], "FAIL: HOD should not have approved GM's request"

    # SuperAdmin approves (final)
    logout(session, my_req_html)
    login(session, "00000001", "Admin@1234")
    print("[+] Logged in as SuperAdmin (00000001)")
    resp_sa = approve_request(session, request_id_c)
    db = check_request_db(request_id_c)
    print(f"    After SuperAdmin Approval: Status={db['Status']} (1=Approved), Stage={db['ApprovalStage']} (3=Approved)")
    assert db['Status'] == 1,        f"FAIL: Expected Approved (1), got {db['Status']}"
    assert db['ApprovalStage'] == 3, f"FAIL: Expected Approved stage (3), got {db['ApprovalStage']}"
    assert not db['GMApprovedByEmployeeId'], "FAIL: GM should NOT approve their own request"
    print("[PASS] SCENARIO C: GM request correctly routed directly to SuperAdmin")

    # Cancel to free inventory
    logout(session, resp_sa.text)
    login(session, "20000001", "Gm@12345")
    mr = session.get(f"{BASE_URL}/RentalRequest/MyRequests")
    token = extract_token(mr.text)
    session.post(f"{BASE_URL}/RentalRequest/Cancel", data={"id": request_id_c, "__RequestVerificationToken": token})

    # ─────────────────────────────────────────────────────────────────
    # SCENARIO D: SuperAdmin submits → Auto-Approved immediately
    # ─────────────────────────────────────────────────────────────────
    print("\n" + "="*60)
    print("SCENARIO D: SuperAdmin submits → Auto-Approved immediately")
    print("="*60)

    session = requests.Session()
    login(session, "00000001", "Admin@1234")
    print("[+] Logged in as SuperAdmin (00000001)")

    item_before_d = check_item_db(12)
    request_id_d, my_req_html = create_rental_request(session, item_id=12, qty=3, unit_price=50.0, days=1)
    db = check_request_db(request_id_d)
    print(f"[+] Created Request #{request_id_d}  |  Status={db['Status']} (expected 1=Approved), Stage={db['ApprovalStage']} (expected 3=Approved)")
    assert db['Status'] == 1,        f"FAIL: SuperAdmin request should be Auto-Approved (Status=1), got {db['Status']}"
    assert db['ApprovalStage'] == 3, f"FAIL: SuperAdmin request should be at Approved stage (3), got {db['ApprovalStage']}"
    assert db['HRApprovedByEmployeeId'], "FAIL: HRApprovedByEmployeeId should be set for auto-approve"

    # Verify inventory was allocated immediately
    allocs = check_allocations_db(request_id_d)
    item_after_d = check_item_db(12)
    print(f"    Allocations count: {len(allocs)}  |  ReservedQty before={item_before_d['ReservedQuantity']}, after={item_after_d['ReservedQuantity']}")
    assert len(allocs) > 0, "FAIL: Inventory should have been allocated immediately on auto-approve"
    print("[PASS] SCENARIO D: SuperAdmin request auto-approved immediately with inventory allocated")

    # Cancel to free inventory
    token = extract_token(my_req_html)
    session.post(f"{BASE_URL}/RentalRequest/Cancel", data={"id": request_id_d, "__RequestVerificationToken": token})

    print("\n" + "="*60)
    print("ALL 4 SCENARIOS PASSED")
    print("="*60)

if __name__ == "__main__":
    run_tests()
