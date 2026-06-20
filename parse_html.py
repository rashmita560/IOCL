from bs4 import BeautifulSoup
import sys
sys.stdout.reconfigure(encoding='utf-8')

with open("debug_approve.html", "r", encoding="utf-8") as f:
    soup = BeautifulSoup(f.read(), 'html.parser')

print("--- Alerts ---")
for alert in soup.find_all(class_=lambda x: x and 'alert' in x):
    print("Alert class:", alert.get('class'))
    print("Text:", alert.get_text(strip=True))
    print()

print("--- TempData or Messages ---")
for msg in soup.find_all(id=lambda x: x and ('message' in x or 'error' in x or 'success' in x)):
    print("ID:", msg.get('id'))
    print("Text:", msg.get_text(strip=True))
    print()

print("--- Badges / Status ---")
for badge in soup.find_all(class_=lambda x: x and 'badge' in x):
    print("Badge text:", badge.get_text(strip=True))
