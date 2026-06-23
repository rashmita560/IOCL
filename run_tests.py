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
    cur.execute("SELECT Status, ApprovalStage FROM RentalRequests WHERE Id = ?", (request_id,))
    req = dict(cur.fetchone())
    conn.close()
    return req

def run_tests():
    session = requests.Session()

    print("\n--- STEP 1: Log in as SuperAdmin (sysadmin@iocl.co.in) ---")
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    if not token:
        print("[-] Error: Failed to extract __RequestVerificationToken from Login page.")
        return
    
    login_data = {
        "employeeId": "00000001",
        "password": "Admin@1234",
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    login_response = session.post(f"{BASE_URL}/Account/Login", data=login_data)
    if "Login" in login_response.url:
        print("[-] Error: Login failed.")
        return
    print("[+] Login as SuperAdmin successful.")

    import sys
    sys.stdout.reconfigure(encoding='utf-8')

    print("\n--- STEP 2: Try to approve Request #29 (insufficient stock) ---")
    # Request #29 is at http://localhost:5000/AdminRequest/Details/29
    details_page = session.get(f"{BASE_URL}/AdminRequest/Details/29")
    token = extract_token(details_page.text)

    # Check request 29 status first
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute("SELECT Status FROM RentalRequests WHERE Id = 29")
    row = cur.fetchone()
    conn.close()
    
    is_pending = row is not None and row[0] == 0
    
    if not is_pending or details_page.status_code == 404 or not token:
        print("[!] Request #29 not found or is not Pending. Skipping Step 2 stock block test.")
        logout_token = token
    else:
        approve_response = session.post(f"{BASE_URL}/AdminRequest/Approve/29", data={"__RequestVerificationToken": token})
        
        # Check if the error message is present in the response page immediately
        error_msg = "Requested inventory is no longer available for the selected date range"
        if error_msg in approve_response.text:
            print("[+] Success: Approval correctly BLOCKED due to stock guard!")
        else:
            print("[-] Error: Approval was not blocked by stock guard.")
            print(approve_response.text[:500])
        logout_token = extract_token(approve_response.text)

    print("\n--- STEP 3: Log out and log in as Employee Rajesh Sharma ---")
    if logout_token:
        session.post(f"{BASE_URL}/Account/Logout", data={"__RequestVerificationToken": logout_token})
    
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    login_data = {
        "employeeId": "10000001",
        "password": "Admin@1234",
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    session.post(f"{BASE_URL}/Account/Login", data=login_data)
    print("[+] Logged in as Rajesh Sharma.")

    print("\n--- STEP 4: Submit a new Rental Request for LED Light String (ItemId=12, Qty=10) ---")
    create_page = session.get(f"{BASE_URL}/RentalRequest/Create")
    token = extract_token(create_page.text)
    
    # We will book for 2026-06-25 to 2026-06-26
    req_data = {
        "EventDate": "2026-06-25",
        "StartDate": "2026-06-25",
        "EndDate": "2026-06-26",
        "RequestItems[0].InventoryItemId": "12",
        "RequestItems[0].RequestedQuantity": "10",
        "RequestItems[0].CurrentPrice": "50.00",
        "__RequestVerificationToken": token
    }
    # Let's populate the other items with 0 quantity so model binder is happy, or just post the item we want.
    files = {
        "InPrincipalDocumentFile": ("dummy.pdf", b"%PDF-1.4 dummy content", "application/pdf")
    }
    # Let's submit:
    create_response = session.post(f"{BASE_URL}/RentalRequest/Create", data=req_data, files=files)
    print(f"    Create POST final URL: {create_response.url}")
    
    # Save create response html for debugging
    with open("debug_create.html", "w", encoding="utf-8") as f:
        f.write(create_response.text)

    # Let's inspect the redirect URL to find the request ID or go to My Requests to find it
    my_requests = session.get(f"{BASE_URL}/RentalRequest/MyRequests")
    
    # Find request ID using regex on MyRequests page
    # It lists requests in table rows, look for links like /RentalRequest/Details/ID, id="modal-ID", or similar
    matches = re.findall(r'id="modal-(\d+)"', my_requests.text)
    if not matches:
        matches = re.findall(r'href="/RentalRequest/Details/(\d+)"', my_requests.text)
    if not matches:
        # Check if they are linked as AdminRequest details
        matches = re.findall(r'/Details/(\d+)', my_requests.text)
    if not matches:
        # Check if they are in the hidden form input
        matches = re.findall(r'name="id"\s+value="(\d+)"', my_requests.text)
    
    if not matches:
        print("[-] Error: Could not find any request links in MyRequests page.")
        # Let's check if there are model validation errors in the debug_create.html
        from bs4 import BeautifulSoup
        soup = BeautifulSoup(create_response.text, 'html.parser')
        val_summary = soup.find(class_="text-danger")
        if val_summary:
            print("    Validation Error Summary:", val_summary.get_text(strip=True))
        validation_errors = soup.find_all(class_="field-validation-error")
        for err in validation_errors:
            print("    Field Validation Error:", err.get_text(strip=True))
        return
        
    new_request_id = max(int(m) for m in matches)
    print(f"[+] Successfully created request with ID = {new_request_id}.")

    # Let's check initial DB state for new request and LED Light String
    db_req = check_request_db(new_request_id)
    print(f"    Initial DB State: Request Status={db_req['Status']} (0=Pending), Stage={db_req['ApprovalStage']} (0=PendingHOD)")
    item_before = check_item_db(12)
    print(f"    LED Light String ReservedQuantity in DB: {item_before['ReservedQuantity']}")

    print("\n--- STEP 5: Log out and log in as HOD Arjun Mehta to approve HOD stage ---")
    logout_token = extract_token(my_requests.text)
    session.post(f"{BASE_URL}/Account/Logout", data={"__RequestVerificationToken": logout_token})
    
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    login_data = {
        "employeeId": "30000001",
        "password": "Hod@1234",
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    session.post(f"{BASE_URL}/Account/Login", data=login_data)
    print("[+] Logged in as HOD Arjun Mehta.")

    # Get details page for new request and approve
    details_page = session.get(f"{BASE_URL}/AdminRequest/Details/{new_request_id}")
    token = extract_token(details_page.text)
    approve_response = session.post(f"{BASE_URL}/AdminRequest/Approve/{new_request_id}", data={"__RequestVerificationToken": token})
    
    db_req = check_request_db(new_request_id)
    print(f"    After HOD Approval: Request Status={db_req['Status']}, Stage={db_req['ApprovalStage']} (should be 2=PendingHR)")

    print("\n--- STEP 6: Log out and log in as SuperAdmin to approve HR stage (final) ---")
    logout_token = extract_token(approve_response.text)
    session.post(f"{BASE_URL}/Account/Logout", data={"__RequestVerificationToken": logout_token})
    
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    login_data = {
        "employeeId": "00000001",
        "password": "Admin@1234",
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    session.post(f"{BASE_URL}/Account/Login", data=login_data)
    print("[+] Logged in as SuperAdmin.")

    details_page = session.get(f"{BASE_URL}/AdminRequest/Details/{new_request_id}")
    token = extract_token(details_page.text)
    approve_final_response = session.post(f"{BASE_URL}/AdminRequest/Approve/{new_request_id}", data={"__RequestVerificationToken": token})

    db_req = check_request_db(new_request_id)
    print(f"    After HR (Final) Approval: Request Status={db_req['Status']} (should be 1=Approved), Stage={db_req['ApprovalStage']} (should be 3=Approved)")

    # Verify allocation in DB
    allocs = check_allocations_db(new_request_id)
    print(f"    Allocations for Request #{new_request_id}:")
    for a in allocs:
        print(f"      - ItemId={a['InventoryItemId']}, Qty={a['AllocatedQuantity']}, Status={a['Status']}, Dates={a['StartDate'][:10]} to {a['EndDate'][:10]}")

    item_after = check_item_db(12)
    print(f"    LED Light String ReservedQuantity in DB after final approval: {item_after['ReservedQuantity']}")
    print(f"    Did it increase by 10? {'Yes!' if item_after['ReservedQuantity'] == item_before['ReservedQuantity'] + 10 else 'No!'}")

    print("\n--- STEP 7: Log out and log in as Employee Rajesh Sharma to cancel the approved request ---")
    logout_token = extract_token(approve_final_response.text)
    session.post(f"{BASE_URL}/Account/Logout", data={"__RequestVerificationToken": logout_token})
    
    login_page = session.get(f"{BASE_URL}/Account/Login")
    token = extract_token(login_page.text)
    login_data = {
        "employeeId": "10000001",
        "password": "Admin@1234",
        "rememberMe": "false",
        "returnUrl": "",
        "__RequestVerificationToken": token
    }
    session.post(f"{BASE_URL}/Account/Login", data=login_data)
    
    # Cancel request POST /RentalRequest/Cancel with id in the form body
    cancel_token = extract_token(session.get(f"{BASE_URL}/RentalRequest/MyRequests").text)
    cancel_response = session.post(f"{BASE_URL}/RentalRequest/Cancel", data={"id": new_request_id, "__RequestVerificationToken": cancel_token})
    
    db_req = check_request_db(new_request_id)
    print(f"    After Cancellation: Request Status={db_req['Status']} (should be 3=Cancelled)")

    # Verify allocation is released
    allocs = check_allocations_db(new_request_id)
    print(f"    Allocations for Request #{new_request_id} after cancellation:")
    for a in allocs:
        print(f"      - ItemId={a['InventoryItemId']}, Qty={a['AllocatedQuantity']}, Status={a['Status']}")

    item_final = check_item_db(12)
    print(f"    LED Light String ReservedQuantity in DB after cancellation: {item_final['ReservedQuantity']} (should be back to {item_before['ReservedQuantity']})")

if __name__ == "__main__":
    run_tests()
