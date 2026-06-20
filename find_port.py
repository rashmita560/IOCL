import re
log_path = r"C:\Users\Rashmita\.gemini\antigravity-ide\brain\106b36e9-2c80-4401-ac72-5ec0b2d50c86\.system_generated\tasks\task-78.log"
with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

urls = re.findall(r'Now listening on:\s*(http\S+)', content)
print("Listening URLs found:")
for url in urls:
    print("  ", url)
