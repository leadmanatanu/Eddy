import json

transcript_path = r"C:\Users\atm00\.gemini\antigravity\brain\7fa834b8-17cc-41e4-85a9-0eccd6ccda9c\.system_generated\logs\transcript_full.jsonl"
out_path = r"Scripts\user_prompt.txt"

with open(transcript_path, "r", encoding="utf-8") as f:
    for line in f:
        if '"step_index":1733' in line:
            data = json.loads(line)
            with open(out_path, "w", encoding="utf-8") as out:
                out.write(data.get("content", ""))
            print("Extracted successfully")
            break
