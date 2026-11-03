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
    # Test reading count=10
    tx_id = 1
    pdu = struct.pack(">BHH", 3, 0, 10) # FC 03, addr 0, count 10
    mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, 1)
    s.sendall(mbap + pdu)
    try:
        resp = s.recv(1024)
        print(f"Read count=10: Response ({len(resp)} bytes): {resp.hex()}")
    except socket.timeout:
        print("Read count=10: TIMEOUT!")
    
    # Check if connection stayed open
    pdu2 = struct.pack(">BHH", 3, 0, 1)
    mbap2 = struct.pack(">HHHB", 2, 0, len(pdu2) + 1, 1)
    s.sendall(mbap2 + pdu2)
    try:
        resp2 = s.recv(1024)
        print(f"Subsequent valid read: Response ({len(resp2)} bytes): {resp2.hex()}")
    except Exception as e:
        print(f"Subsequent valid read failed: {e}")
    s.close()

finally:
    p.terminate()
    p.wait()
    print("8F terminated.")
