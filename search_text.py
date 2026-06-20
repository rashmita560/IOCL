with open("debug_approve.html", "r", encoding="utf-8") as f:
    text = f.read()

print("File length:", len(text))
print("Is 'no longer available' in file?", "no longer available" in text.lower())
print("Is 'conflict' in file?", "conflict" in text.lower())
print("Is 'decision' in file?", "decision" in text.lower())
print("Is 'decision' in file (exact case)?", "Decision" in text)

# Find all lines containing 'alert'
print("\n--- Alert containing lines ---")
lines = text.splitlines()
for idx, line in enumerate(lines):
    if "alert" in line.lower() or "danger" in line.lower() or "warning" in line.lower() or "success" in line.lower() or "error" in line.lower():
        if len(line.strip()) < 300:
            print(f"L{idx}: {line.strip()}")
