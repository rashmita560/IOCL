import requests
import re
import sys
sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "http://localhost:5000"

def extract_token(html):
    match = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
    if not match:
        match = re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', html)
    if not match:
        match = re.search(r'<input[^>]*?name="__RequestVerificationToken"[^>]*?value="([^"]+)"', html)
    return match.group(1) if match else None

session = requests.Session()

# Log in
login_page = session.get(f"{BASE_URL}/Account/Login")
token = extract_token(login_page.text)
login_data = {
    "employeeId": "00000001",
    "password": "Admin@1234",
    "rememberMe": "false",
    "returnUrl": "",
    "__RequestVerificationToken": token
}
login_response = session.post(f"{BASE_URL}/Account/Login", data=login_data)
print(f"Logged in. Final URL: {login_response.url}")

# Approve Request 29
details_page = session.get(f"{BASE_URL}/AdminRequest/Details/29")
token = extract_token(details_page.text)
print(f"Details token: {token}")

approve_response = session.post(f"{BASE_URL}/AdminRequest/Approve/29", data={"__RequestVerificationToken": token})
print(f"Approve POST final URL: {approve_response.url}")

with open("debug_approve.html", "w", encoding="utf-8") as f:
    f.write(approve_response.text)

print("Is 'Requested inventory is no longer available' in page?", "Requested inventory is no longer available" in approve_response.text)
print("Is 'Request fully approved' in page?", "Request fully approved" in approve_response.text)
print("Is 'Success' or 'Error' in page?")
for line in approve_response.text.splitlines():
    if "TempData" in line or "alert" in line or "danger" in line or "success" in line or "Error" in line:
        if len(line.strip()) < 200:
            print("  Line:", line.strip())
