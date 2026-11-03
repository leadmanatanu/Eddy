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
    for host in ["127.0.0.1", "192.168.68.108"]:
        print(f"\n--- Testing host {host} ---")
        for unit_id in [1, 255]:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(2.0)
            try:
                s.connect((host, 5020))
                tx_id = 1
                pdu = struct.pack(">BHH", 3, 0, 1) # FC 03, addr 0, count 1
                mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, unit_id)
                s.sendall(mbap + pdu)
                resp = s.recv(1024)
                print(f"[{host}] Unit ID {unit_id:3d}: Got response ({len(resp)} bytes): {resp.hex()}")
            except socket.timeout:
                print(f"[{host}] Unit ID {unit_id:3d}: TIMEOUT! (No response received)")
            except Exception as e:
                print(f"[{host}] Unit ID {unit_id:3d}: Error: {e}")
            finally:
                s.close()
finally:
    p.terminate()
    p.wait()
    print("8F terminated.")
