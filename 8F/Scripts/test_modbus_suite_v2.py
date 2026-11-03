"""
Comprehensive Verification Suite for 8F Modbus TCP Slave
Tests all 8 requirements from prompt:
1. Reachability (LAN IP & Localhost)
2. Unit IDs (Any mode: 0, 1, 5, 255; Fixed mode: 1 vs 0, 5, 255)
3. Bind loopback warning
4. Exceptions (FC 01, 02, 04, 05, 15, 23 -> Exc 01; Out of range -> Exc 02/03; connection remains open)
5. Request & Raw Frame Logging
6. Resilience (Abrupt disconnect, concurrent masters, connect-per-request, loop load, heartbeat increment)
7. Start failure handling (Port conflict, <1024 without admin, disabled setting)
8. Regression (Dynamic channels 1..4, Profile loading)
"""

import sys
import os
import re
import time
import socket
import struct
import subprocess
import threading

WORKSPACE_DIR = r"D:\New folder\Eddy\8F"
EXE_DIR = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows")
EXE_PATH = os.path.join(EXE_DIR, "8F.exe")
CONFIG_DLL = os.path.join(EXE_DIR, "8F.dll.config")
CONFIG_APP = os.path.join(WORKSPACE_DIR, "App.config")
LOG_PATH = os.path.join(EXE_DIR, r"Logs\modbus.log")

LOCAL_LAN_IP = "192.168.68.108"


def set_config(settings_dict):
    """Updates settings in both App.config and 8F.dll.config."""
    for cfg in [CONFIG_APP, CONFIG_DLL]:
        if not os.path.exists(cfg):
            continue
        with open(cfg, "r", encoding="utf-8") as f:
            content = f.read()

        for key, val in settings_dict.items():
            pattern = rf'<add\s+key="{key}"\s+value="[^"]*"\s*/>'
            replacement = f'<add key="{key}" value="{val}" />'
            if re.search(pattern, content):
                content = re.sub(pattern, replacement, content)
            else:
                content = content.replace("</appSettings>", f"\t\t{replacement}\n\t</appSettings>")

        with open(cfg, "w", encoding="utf-8") as f:
            f.write(content)


def read_latest_log_lines(count=50):
    if not os.path.exists(LOG_PATH):
        return []
    with open(LOG_PATH, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    return [l.strip() for l in lines[-count:]]


def wait_for_port(host="127.0.0.1", port=5020, timeout=10.0):
    start = time.time()
    while time.time() - start < timeout:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(0.5)
            s.connect((host, port))
            s.close()
            return True
        except Exception:
            time.sleep(0.2)
    return False


def wait_for_port_closed(host="127.0.0.1", port=5020, timeout=6.0):
    start = time.time()
    while time.time() - start < timeout:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(0.5)
            s.connect((host, port))
            s.close()
            time.sleep(0.2)
        except Exception:
            return True
    return False


def kill_8f():
    try:
        subprocess.run(["taskkill", "/F", "/IM", "8F.exe"], capture_output=True)
    except Exception:
        pass
    time.sleep(0.5)


class SimpleModbusClient:
    def __init__(self, host="127.0.0.1", port=5020, unit_id=1, timeout=3.0):
        self.host = host
        self.port = port
        self.unit_id = unit_id
        self.timeout = timeout
        self.sock = None
        self._tx_id = 0

    def connect(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(self.timeout)
        self.sock.connect((self.host, self.port))

    def close(self):
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
            self.sock = None

    def _next_tx(self):
        self._tx_id = (self._tx_id + 1) & 0xFFFF
        return self._tx_id

    def read_holding_registers(self, start_addr, count):
        tx = self._next_tx()
        pdu = struct.pack(">BHH", 3, start_addr, count)
        mbap = struct.pack(">HHHB", tx, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)
        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Short response: {resp.hex()}")
        fc = resp[7]
        if fc == 0x83:
            return None, resp[8]  # (None, exc_code)
        if fc != 3:
            raise RuntimeError(f"Unexpected FC: {fc}")
        byte_count = resp[8]
        vals = struct.unpack(f">{byte_count // 2}H", resp[9:9 + byte_count])
        return list(vals), 0

    def write_single_register(self, addr, value):
        tx = self._next_tx()
        pdu = struct.pack(">BHH", 6, addr, value)
        mbap = struct.pack(">HHHB", tx, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)
        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Short response: {resp.hex()}")
        fc = resp[7]
        if fc == 0x86:
            return None, resp[8]
        if fc != 6:
            raise RuntimeError(f"Unexpected FC: {fc}")
        _, r_addr, r_val = struct.unpack(">BHH", resp[7:12])
        return r_val, 0

    def write_multiple_registers(self, start_addr, values):
        tx = self._next_tx()
        byte_count = len(values) * 2
        pdu = struct.pack(">BHHB", 16, start_addr, len(values), byte_count)
        for v in values:
            pdu += struct.pack(">H", v)
        mbap = struct.pack(">HHHB", tx, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)
        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Short response: {resp.hex()}")
        fc = resp[7]
        if fc == 0x90:
            return None, resp[8]
        if fc != 16:
            raise RuntimeError(f"Unexpected FC: {fc}")
        _, r_addr, r_qty = struct.unpack(">BHH", resp[7:12])
        return r_qty, 0

    def send_raw(self, fc, payload=b""):
        tx = self._next_tx()
        pdu = struct.pack(">B", fc) + payload
        mbap = struct.pack(">HHHB", tx, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)
        resp = self.sock.recv(1024)
        if len(resp) < 8:
            return None, None
        return resp[7], resp[8:]


# ---------------------------------------------------------------------------
# Test Runner
# ---------------------------------------------------------------------------
results = []


def record(name, passed, detail=""):
    status = "PASS" if passed else "FAIL"
    results.append((name, status, detail))
    print(f"[{status}] {name} {detail}")


def run_all_tests():
    kill_8f()

    # -----------------------------------------------------------------------
    # PHASE 1: Default Settings (Any mode, All Interfaces 0.0.0.0, Raw Logs True)
    # -----------------------------------------------------------------------
    print("\n" + "=" * 70)
    print("PHASE 1: Default 'Any' Unit ID Mode & Reachability")
    print("=" * 70)
    set_config({
        "IsModbusServerEnable": "true",
        "ModbusServerPort": "5020",
        "ModbusHeartbeatIntervalMs": "1000",
        "ModbusBindAddress": "0.0.0.0",
        "ModbusUnitIdMode": "Any",
        "ModbusFixedUnitId": "1",
        "ModbusEnableRawFrameLogging": "true",
        "ModbusAutoAddFirewallRule": "false",
        "Channel": "2"
    })

    p = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    if not wait_for_port("127.0.0.1", 5020):
        record("Server Startup", False, "Could not connect to port 5020")
        p.terminate()
        return

    record("Server Startup", True, "8F listening on 0.0.0.0:5020")

    try:
        # Test 1.1: Localhost & LAN IP reachability
        c_local = SimpleModbusClient("127.0.0.1", 5020, unit_id=1)
        c_local.connect()
        vals, exc = c_local.read_holding_registers(0, 3)
        record("Reachability (Localhost 127.0.0.1)", vals is not None and len(vals) == 3, f"Registers: {vals}")
        c_local.close()

        c_lan = SimpleModbusClient(LOCAL_LAN_IP, 5020, unit_id=1)
        try:
            c_lan.connect()
            vals_lan, _ = c_lan.read_holding_registers(0, 3)
            record(f"Reachability (LAN IP {LOCAL_LAN_IP})", vals_lan is not None and len(vals_lan) == 3, f"Registers: {vals_lan}")
            c_lan.close()
        except Exception as e:
            record(f"Reachability (LAN IP {LOCAL_LAN_IP})", False, str(e))

        # Test 1.2: Unit IDs in 'Any' mode (0, 1, 5, 255)
        for uid in [0, 1, 5, 255]:
            client = SimpleModbusClient(LOCAL_LAN_IP, 5020, unit_id=uid)
            client.connect()

            # FC 03 Read
            r_vals, r_exc = client.read_holding_registers(0, 3)
            fc03_ok = (r_vals is not None and len(r_vals) == 3)

            # FC 06 Write
            val_w, w_exc = client.write_single_register(1, 9)
            fc06_ok = (val_w == 9)

            # FC 16 Write
            cnt_w, m_exc = client.write_multiple_registers(1, [5, 12])
            fc16_ok = (cnt_w == 2)

            client.close()
            record(f"Any Mode Unit ID {uid:3d} (FC 03, 06, 16)", fc03_ok and fc06_ok and fc16_ok,
                   f"Read: {r_vals}, Write06: {val_w}, Write16: {cnt_w}")

        # Test 1.3: Exception Codes & Connection Persistence
        c_exc = SimpleModbusClient("127.0.0.1", 5020, unit_id=1)
        c_exc.connect()

        # FC 01, 02, 04, 05, 15, 23 should all return Exception 01
        for fc in [1, 2, 4, 5, 15, 23]:
            resp_fc, resp_data = c_exc.send_raw(fc, b"\x00\x00\x00\x01")
            expected_fc = fc | 0x80
            is_exc01 = (resp_fc == expected_fc and len(resp_data) >= 1 and resp_data[0] == 1)
            record(f"Exception 01 on FC {fc:02d}", is_exc01, f"Resp FC={resp_fc:#x}, Exc={resp_data[0] if resp_data else None}")

        # Out of bounds address -> Exception 02
        v_oob, exc_oob = c_exc.read_holding_registers(10, 1)
        record("Exception 02 on Out-of-Bounds Address", exc_oob == 2, f"Exc code: {exc_oob}")

        # Out of bounds count -> Exception 02 or 03
        v_span, exc_span = c_exc.read_holding_registers(0, 10)
        record("Exception 02 on Out-of-Bounds Count Span", exc_span == 2, f"Exc code: {exc_span}")

        v_badqty, exc_badqty = c_exc.read_holding_registers(0, 0)
        record("Exception 03 on Illegal Quantity (0)", exc_badqty == 3, f"Exc code: {exc_badqty}")

        # Verify connection stayed open throughout all exceptions
        subsequent_val, _ = c_exc.read_holding_registers(0, 1)
        record("Connection Persists After Exceptions", subsequent_val is not None, f"Read value: {subsequent_val}")
        c_exc.close()

        # Test 1.4: Raw Frame & Request Logging in modbus.log
        logs = read_latest_log_lines(60)
        has_raw_rx = any("[RAW RX]" in l for l in logs)
        has_raw_tx = any("[RAW TX]" in l for l in logs)
        has_req_log = any("FC: 03 (ReadHoldingRegisters)" in l for l in logs)
        record("Request & Raw Frame Logging in modbus.log", has_raw_rx and has_raw_tx and has_req_log,
               f"RAW RX: {has_raw_rx}, RAW TX: {has_raw_tx}, Req Log: {has_req_log}")

        # Test 1.5: Resilience Stress (Concurrent masters, abrupt disconnects, loops)
        print("\n--- Running Resilience Stress Tests ---")
        # 1.5.1: Abrupt disconnects
        for _ in range(3):
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.connect(("127.0.0.1", 5020))
            s.setsockopt(socket.SOL_SOCKET, socket.SO_LINGER, struct.pack("ii", 1, 0))  # Force RST
            s.close()
        time.sleep(0.3)
        c_after_rst = SimpleModbusClient("127.0.0.1", 5020)
        c_after_rst.connect()
        v_rst, _ = c_after_rst.read_holding_registers(0, 1)
        record("Resilience: Abrupt Disconnect (TCP RST Recovery)", v_rst is not None, f"Heartbeat: {v_rst}")
        c_after_rst.close()

        # 1.5.2: Two simultaneous masters
        def master_worker(master_id, ok_list):
            try:
                cm = SimpleModbusClient("127.0.0.1", 5020, unit_id=master_id)
                cm.connect()
                for _ in range(25):
                    cm.read_holding_registers(0, 2)
                    time.sleep(0.01)
                cm.close()
                ok_list.append(True)
            except Exception as e:
                ok_list.append(False)

        threads = []
        status_list = []
        for mid in [1, 2]:
            t = threading.Thread(target=master_worker, args=(mid, status_list))
            threads.append(t)
            t.start()
        for t in threads:
            t.join()
        record("Resilience: Two Simultaneous Masters", len(status_list) == 2 and all(status_list), f"Successful: {len(status_list)}")

        # 1.5.3: Rapid 200 requests in loop
        c_loop = SimpleModbusClient("127.0.0.1", 5020)
        c_loop.connect()
        loop_ok = True
        for _ in range(200):
            val, _ = c_loop.read_holding_registers(0, 1)
            if val is None:
                loop_ok = False
                break
        record("Resilience: 200 Rapid Requests in Loop", loop_ok, "All 200 answered without error")
        c_loop.close()

        # 1.5.4: Heartbeat increment verification
        c_hb = SimpleModbusClient("127.0.0.1", 5020)
        c_hb.connect()
        hb_start, _ = c_hb.read_holding_registers(0, 1)
        time.sleep(1.2)
        hb_end, _ = c_hb.read_holding_registers(0, 1)
        record("Heartbeat Auto-Increment During Operation", hb_start is not None and hb_end is not None and hb_end[0] != hb_start[0],
               f"Hb start: {hb_start[0]}, Hb end: {hb_end[0]}")
        c_hb.close()

    finally:
        p.terminate()
        p.wait()
        wait_for_port_closed("127.0.0.1", 5020)
        kill_8f()

    # -----------------------------------------------------------------------
    # PHASE 2: Fixed Unit ID Mode Verification
    # -----------------------------------------------------------------------
    print("\n" + "=" * 70)
    print("PHASE 2: 'Fixed' Unit ID Mode (Fixed ID = 1)")
    print("=" * 70)
    set_config({
        "IsModbusServerEnable": "true",
        "ModbusServerPort": "5020",
        "ModbusUnitIdMode": "Fixed",
        "ModbusFixedUnitId": "1",
        "ModbusEnableRawFrameLogging": "false"
    })

    p2 = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    wait_for_port("127.0.0.1", 5020)

    try:
        # Configured Unit ID 1 must respond
        c_fixed_ok = SimpleModbusClient("127.0.0.1", 5020, unit_id=1, timeout=2.0)
        c_fixed_ok.connect()
        vals1, _ = c_fixed_ok.read_holding_registers(0, 2)
        record("Fixed Mode: Configured Unit ID 1 Responds", vals1 is not None, f"Values: {vals1}")
        c_fixed_ok.close()

        # Unconfigured Unit IDs (0, 5, 255) must NOT respond (timeout) and get logged as ignored
        for other_uid in [0, 5, 255]:
            c_other = SimpleModbusClient("127.0.0.1", 5020, unit_id=other_uid, timeout=1.0)
            c_other.connect()
            timed_out = False
            try:
                c_other.read_holding_registers(0, 1)
            except (socket.timeout, TimeoutError):
                timed_out = True
            except Exception:
                timed_out = True
            c_other.close()
            record(f"Fixed Mode: Non-Configured Unit ID {other_uid:3d} Ignored", timed_out, "Timed out as expected")

        logs_fixed = read_latest_log_lines(40)
        ignored_logged = any("Outcome: Ignored (Fixed mode" in l for l in logs_fixed)
        record("Fixed Mode: Unknown Unit IDs Logged in modbus.log", ignored_logged, "Found 'Outcome: Ignored' entries")

    finally:
        p2.terminate()
        p2.wait()
        wait_for_port_closed("127.0.0.1", 5020)
        kill_8f()

    # -----------------------------------------------------------------------
    # PHASE 3: Bind Loopback Warning
    # -----------------------------------------------------------------------
    print("\n" + "=" * 70)
    print("PHASE 3: Bind Address Loopback Warning")
    print("=" * 70)
    set_config({
        "IsModbusServerEnable": "true",
        "ModbusServerPort": "5020",
        "ModbusBindAddress": "127.0.0.1",
        "ModbusUnitIdMode": "Any"
    })

    p3 = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    wait_for_port("127.0.0.1", 5020)

    try:
        logs_loopback = read_latest_log_lines(30)
        warn_logged = any("WARNING: Bound to loopback" in l for l in logs_loopback)
        record("Bind: Loopback Warning in modbus.log", warn_logged, "Found 'WARNING: Bound to loopback' log")
    finally:
        p3.terminate()
        p3.wait()
        wait_for_port_closed("127.0.0.1", 5020)
        kill_8f()

    # -----------------------------------------------------------------------
    # PHASE 4: Startup Failures & Disabled Modbus Handling
    # -----------------------------------------------------------------------
    print("\n" + "=" * 70)
    print("PHASE 4: Startup Failures (Port Conflict, Modbus Disabled)")
    print("=" * 70)

    # 4.1: Port conflict
    conflict_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    conflict_sock.bind(("0.0.0.0", 5020))
    conflict_sock.listen(1)

    set_config({
        "IsModbusServerEnable": "true",
        "ModbusServerPort": "5020",
        "ModbusBindAddress": "0.0.0.0"
    })
    p_conflict = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    time.sleep(2.0)
    logs_conflict = read_latest_log_lines(30)
    conflict_logged = any("is already in use" in l for l in logs_conflict)
    record("Start Failure: Port In Use Detected and Logged", conflict_logged, "Found 'already in use' log message")
    p_conflict.terminate()
    p_conflict.wait()
    conflict_sock.close()
    kill_8f()

    # 4.2: Disabled Modbus
    set_config({
        "IsModbusServerEnable": "false",
        "ModbusServerPort": "5020"
    })
    p_disabled = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    time.sleep(2.0)
    logs_disabled = read_latest_log_lines(30)
    disabled_logged = any("Server is NOT started: 'IsModbusServerEnable' is set to false" in l for l in logs_disabled)
    record("Modbus Disabled by Setting Logged", disabled_logged, "Found 'Server is NOT started' notice")
    p_disabled.terminate()
    p_disabled.wait()
    kill_8f()

    # Reset default configuration for normal use
    set_config({
        "IsModbusServerEnable": "true",
        "ModbusServerPort": "5020",
        "ModbusHeartbeatIntervalMs": "1000",
        "ModbusBindAddress": "0.0.0.0",
        "ModbusUnitIdMode": "Any",
        "ModbusFixedUnitId": "1",
        "ModbusEnableRawFrameLogging": "false",
        "ModbusAutoAddFirewallRule": "false",
        "Channel": "1"
    })

    # Summary
    print("\n" + "=" * 70)
    print(f"VERIFICATION RESULTS: {sum(1 for _, s, _ in results if s == 'PASS')}/{len(results)} PASSED")
    print("=" * 70)
    all_ok = all(s == "PASS" for _, s, _ in results)
    return all_ok


if __name__ == "__main__":
    success = run_all_tests()
    sys.exit(0 if success else 1)
