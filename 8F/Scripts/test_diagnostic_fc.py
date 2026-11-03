import socket
import struct
import subprocess
import time
import os

WORKSPACE_DIR = r"D:\New folder\Eddy\8F"
EXE_PATH = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows\8F.exe")
EXE_DIR = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows")

print("Starting 8F...")
p = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
time.sleep(2.5)

try:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(2.0)
    s.connect(("127.0.0.1", 5020))
    for fc in [1, 2, 4, 5, 15, 23]:
        tx_id = fc
        pdu = struct.pack(">BHH", fc, 0, 1) # simple payload
        mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, 1)
        s.sendall(mbap + pdu)
        try:
            resp = s.recv(1024)
            print(f"FC {fc:02d}: Got response ({len(resp)} bytes): {resp.hex()}")
        except socket.timeout:
            print(f"FC {fc:02d}: TIMEOUT! (No response)")
        except Exception as e:
            print(f"FC {fc:02d}: Error: {e}")
    s.close()
finally:
    p.terminate()
    p.wait()
    print("8F terminated.")
