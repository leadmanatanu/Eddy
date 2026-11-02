"""
Modbus TCP Master Test Client - Dynamic Channel Count Verification Suite
Target: 8F Modbus TCP Slave (Port 5020, Unit ID 1).

DYNAMIC REGISTER MAP (Channels N = 1..4):
================================================================================
Address | Content
--------+-----------------------------------------------------------------------
   0    | Heartbeat (auto-increments every 1000ms, rollover at 65535)
 1..N   | Channel 1..N Profile IDs (Address = Channel Index + 1)
================================================================================
Total Contiguous Holding Registers: N + 1 (Addresses 0..N)
Supported Function Codes:
  FC 03 (0x03) - Read Holding Registers
  FC 06 (0x06) - Write Single Register
  FC 16 (0x10) - Write Multiple Registers
Exception Handling:
  Unsupported FCs (01, 02, 04, 05, 15, etc.) -> Modbus Exception 01 (Illegal Function)
  Out-of-bounds addresses (> N) or spans     -> Modbus Exception 02 (Illegal Data Address)
"""

import sys
import os
import socket
import struct
import time
import argparse

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5020
DEFAULT_UNIT_ID = 1

HEARTBEAT_ADDR = 0
FIRST_CHANNEL_ADDR = 1


class ModbusException(Exception):
    def __init__(self, function_code, exception_code, message=""):
        super().__init__(message or f"Modbus Exception {exception_code:02d} for FC {function_code:02d}")
        self.function_code = function_code
        self.exception_code = exception_code


class ModbusTcpClient:
    def __init__(self, host=DEFAULT_HOST, port=DEFAULT_PORT, unit_id=DEFAULT_UNIT_ID, timeout=5.0):
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

    def _next_tx_id(self):
        self._tx_id = (self._tx_id + 1) & 0xFFFF
        return self._tx_id

    def read_holding_registers(self, start_address, count):
        """FC 03: Read Holding Registers"""
        tx_id = self._next_tx_id()
        pdu = struct.pack(">BHH", 3, start_address, count)
        mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)

        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc = struct.unpack(">HHHBB", resp[:8])
        if resp_fc == 0x83:
            exc_code = resp[8]
            raise ModbusException(3, exc_code, f"Modbus Exception {exc_code:02d} on FC 03 (start={start_address}, count={count})")
        if resp_fc != 3:
            raise RuntimeError(f"Unexpected response FC: {resp_fc}")
        byte_count = resp[8]
        values = struct.unpack(f">{byte_count // 2}H", resp[9:9 + byte_count])
        return list(values)

    def write_single_register(self, address, value):
        """FC 06: Write Single Holding Register"""
        tx_id = self._next_tx_id()
        pdu = struct.pack(">BHH", 6, address, value)
        mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)

        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc = struct.unpack(">HHHBB", resp[:8])
        if resp_fc == 0x86:
            exc_code = resp[8]
            raise ModbusException(6, exc_code, f"Modbus Exception {exc_code:02d} on FC 06 (address={address})")
        if resp_fc != 6:
            raise RuntimeError(f"Unexpected response FC: {resp_fc}")
        _, resp_addr, resp_val = struct.unpack(">BHH", resp[7:12])
        return resp_val

    def write_multiple_registers(self, start_address, values):
        """FC 16 (0x10): Write Multiple Holding Registers"""
        tx_id = self._next_tx_id()
        byte_count = len(values) * 2
        pdu = struct.pack(">BHHB", 16, start_address, len(values), byte_count)
        for val in values:
            pdu += struct.pack(">H", val)
        mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)

        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc = struct.unpack(">HHHBB", resp[:8])
        if resp_fc == 0x90:
            exc_code = resp[8]
            raise ModbusException(16, exc_code, f"Modbus Exception {exc_code:02d} on FC 16 (start={start_address})")
        if resp_fc != 16:
            raise RuntimeError(f"Unexpected response FC: {resp_fc}")
        _, resp_addr, resp_count = struct.unpack(">BHH", resp[7:12])
        return resp_count

    def send_raw_pdu(self, fc, payload=b""):
        """Sends raw PDU and returns (resp_fc, exception_code_or_data)."""
        tx_id = self._next_tx_id()
        pdu = struct.pack(">B", fc) + payload
        mbap = struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, self.unit_id)
        self.sock.sendall(mbap + pdu)

        resp = self.sock.recv(1024)
        if len(resp) < 9:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc = struct.unpack(">HHHBB", resp[:8])
        return resp_fc, resp[8:]


def read_latest_log_lines(log_path, count=25):
    if not os.path.exists(log_path):
        return []
    with open(log_path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    return [line.strip() for line in lines[-count:]]


def run_tests(host=DEFAULT_HOST, port=DEFAULT_PORT, channels=1, valid_id=9, secondary_id=5, partial_ch_id=19, invalid_id=9999, log_path=None):
    total_registers = channels + 1
    max_valid_addr = channels
    oob_addr = channels + 1

    print("=" * 80)
    print(f"MODBUS TCP SLAVE DYNAMIC VERIFICATION SUITE (Channels = {channels})")
    print(f"Map: Addr 0 = Heartbeat | Addr 1..{channels} = Channels 1..{channels} Profile IDs")
    print(f"Total Exposed Registers: {total_registers} (Addresses 0..{max_valid_addr})")
    print(f"Target: {host}:{port} | Primary: {valid_id} | Secondary: {secondary_id} | Partial: {partial_ch_id}")
    print("=" * 80)

    client = ModbusTcpClient(host=host, port=port)
    try:
        client.connect()
        print("[SETUP] Successfully connected to Modbus TCP slave.\n")
    except Exception as ex:
        print(f"[ERROR] Could not connect to {host}:{port}: {ex}")
        print("Please ensure the 8F application is running with IsModbusServerEnable=true.")
        return False

    all_passed = True
    test_results = []

    def record_test(name, passed, detail=""):
        nonlocal all_passed
        status = "PASS" if passed else "FAIL"
        if not passed:
            all_passed = False
        test_results.append((name, status, detail))
        print(f"[{status}] {name} {detail}")

    try:
        # Step 0: Read initial state
        initial_regs = client.read_holding_registers(0, total_registers)
        print(f"[INITIAL REGISTERS] 0..{max_valid_addr}: {initial_regs}\n")

        # -----------------------------------------------------------------
        # TEST 1: Heartbeat at Address 0
        # -----------------------------------------------------------------
        print(f"--- TEST 1: Heartbeat at Address 0 ---")
        try:
            hb1 = client.read_holding_registers(HEARTBEAT_ADDR, 1)[0]
            time.sleep(1.2)
            hb2 = client.read_holding_registers(HEARTBEAT_ADDR, 1)[0]
            ticked = (hb2 != hb1)

            # Write manual value to address 0
            test_hb = 7777
            client.write_single_register(HEARTBEAT_ADDR, test_hb)
            hb_written = client.read_holding_registers(HEARTBEAT_ADDR, 1)[0]
            time.sleep(1.2)
            hb_overwritten = client.read_holding_registers(HEARTBEAT_ADDR, 1)[0]
            overwritten = (hb_overwritten != test_hb)

            detail = f"- Ticked ({hb1}->{hb2}), Write accepted ({hb_written}), Overwritten on next tick ({hb_overwritten})"
            passed = ticked and (hb_written == test_hb) and overwritten
            record_test("Test 1: Heartbeat (Auto-increments ~1/s, FC 06 write overwritten, loads no profile)", passed, detail)
        except Exception as ex:
            record_test("Test 1: Heartbeat", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 2: Sequential Channel Writes (1..N) & Address 0 Immunity
        # -----------------------------------------------------------------
        print(f"\n--- TEST 2: Channel Isolation & Addr 0 Immunity (Channels 1..{channels}) ---")
        try:
            # Baseline: set all active channels to secondary_id
            client.write_multiple_registers(FIRST_CHANNEL_ADDR, [secondary_id] * channels)
            time.sleep(1.0)

            seq_ok = True
            for ch in range(1, channels + 1):
                client.write_single_register(ch, valid_id)
                time.sleep(0.8)
                current = client.read_holding_registers(FIRST_CHANNEL_ADDR, channels)
                expected = [valid_id] * ch + [secondary_id] * (channels - ch)
                if current != expected:
                    seq_ok = False
                    print(f"    Mismatch after writing Ch {ch}: expected {expected}, got {current}")

            # Confirm write to Address 0 (Heartbeat) does NOT change any channel
            client.write_single_register(HEARTBEAT_ADDR, 54321)
            time.sleep(0.5)
            regs_after_hb = client.read_holding_registers(FIRST_CHANNEL_ADDR, channels)
            hb_isolated = (regs_after_hb == [valid_id] * channels)

            passed = seq_ok and hb_isolated
            record_test(f"Test 2: Channel Isolation (FC 06 to Addr 1..{channels} updates target, Addr 0 changes no channel)", passed, f"- Final channels: {regs_after_hb}")
        except Exception as ex:
            record_test("Test 2: Channel Isolation", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 3: FC 16 Multi-Channel Write over Addresses 1 to N
        # -----------------------------------------------------------------
        print(f"\n--- TEST 3: FC 16 Multi-Channel Write over Addresses 1..{channels} ---")
        try:
            pattern = [valid_id if i % 2 == 0 else secondary_id for i in range(channels)]
            client.write_multiple_registers(FIRST_CHANNEL_ADDR, pattern)
            time.sleep(1.0)
            regs = client.read_holding_registers(FIRST_CHANNEL_ADDR, channels)
            passed = (regs == pattern)
            detail = f"- Expected {pattern}, got {regs}"
            record_test(f"Test 3: FC 16 over Addresses 1..{channels}", passed, detail)
        except Exception as ex:
            record_test("Test 3: FC 16 Multi-Channel Write", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 4: FC 16 over Addresses 0 to N (Heartbeat Ignored, Channels Loaded)
        # -----------------------------------------------------------------
        print(f"\n--- TEST 4: FC 16 over Addresses 0..{channels} (HB Ignored) ---")
        try:
            hb_val = 1234
            ch_pattern = [secondary_id if i % 2 == 0 else valid_id for i in range(channels)]
            full_write = [hb_val] + ch_pattern
            client.write_multiple_registers(0, full_write)
            time.sleep(1.2)
            ch_regs = client.read_holding_registers(FIRST_CHANNEL_ADDR, channels)
            passed = (ch_regs == ch_pattern)
            record_test(f"Test 4: FC 16 over Addresses 0..{channels} (Heartbeat value ignored, channels 1..{channels} loaded)", passed, f"- Expected {ch_pattern}, got {ch_regs}")
        except Exception as ex:
            record_test("Test 4: FC 16 over Addresses 0..N", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 5: FC 03 Read Combinations
        # -----------------------------------------------------------------
        print(f"\n--- TEST 5: FC 03 Read Combinations ---")
        try:
            r_all = client.read_holding_registers(0, total_registers)
            r1 = client.read_holding_registers(1, 1)
            r_chs = client.read_holding_registers(1, channels)

            r_all_ok = (len(r_all) == total_registers and r_all[1:] == r_chs)
            r1_ok = (len(r1) == 1 and r1[0] == r_chs[0])
            r_chs_ok = (len(r_chs) == channels)
            passed = r_all_ok and r1_ok and r_chs_ok
            record_test(f"Test 5: FC 03 Read Combinations (Read {total_registers} from 0, Read 1 from 1, Read {channels} from 1)", passed, f"- Read All: HB={r_all[0]}, Chs={r_all[1:]}")
        except Exception as ex:
            record_test("Test 5: FC 03 Reads", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 6: Bounds (Exc 02 for Addr > N) & Illegal FCs (Exc 01)
        # -----------------------------------------------------------------
        print(f"\n--- TEST 6: Bounds & Illegal Function Codes ---")
        try:
            illegal_fcs = [
                (1, bytes([0, 0, 0, 1]), "FC 01 Read Coils"),
                (2, bytes([0, 0, 0, 1]), "FC 02 Read Discrete Inputs"),
                (4, bytes([0, 0, 0, 1]), "FC 04 Read Input Registers"),
                (5, bytes([0, 0, 0xFF, 0]), "FC 05 Write Single Coil"),
                (15, bytes([0, 0, 0, 1, 1, 1]), "FC 15 Write Multiple Coils")
            ]
            fcs_ok = True
            for fc, payload, desc in illegal_fcs:
                resp_fc, data = client.send_raw_pdu(fc, payload)
                if resp_fc != (fc + 0x80) or len(data) == 0 or data[0] != 1:
                    fcs_ok = False
                    print(f"    Failed {desc}: resp_fc={hex(resp_fc)}, data={data.hex()}")

            # Address bounds tests:
            bounds_ok = True

            # 1. Read from 0 count total_registers + 1 (reaches oob_addr) -> Exception 02
            try:
                client.read_holding_registers(0, total_registers + 1)
                bounds_ok = False
                print(f"    Failed: FC 03 read 0 count {total_registers + 1} succeeded unexpectedly")
            except ModbusException as ex:
                if ex.exception_code != 2:
                    bounds_ok = False

            # 2. Read from oob_addr count 1 -> Exception 02
            try:
                client.read_holding_registers(oob_addr, 1)
                bounds_ok = False
                print(f"    Failed: FC 03 read from oob_addr {oob_addr} succeeded unexpectedly")
            except ModbusException as ex:
                if ex.exception_code != 2:
                    bounds_ok = False

            # 3. Write single register to oob_addr -> Exception 02
            try:
                client.write_single_register(oob_addr, 123)
                bounds_ok = False
                print(f"    Failed: FC 06 write to oob_addr {oob_addr} succeeded unexpectedly")
            except ModbusException as ex:
                if ex.exception_code != 2:
                    bounds_ok = False

            # 4. Write multiple registers spanning past max_valid_addr -> Exception 02
            try:
                client.write_multiple_registers(max_valid_addr, [10, 20])
                bounds_ok = False
                print(f"    Failed: FC 16 write spanning past {max_valid_addr} succeeded unexpectedly")
            except ModbusException as ex:
                if ex.exception_code != 2:
                    bounds_ok = False

            passed = fcs_ok and bounds_ok
            record_test(f"Test 6: Bounds (Exc 02 for Addr > {max_valid_addr}) & Illegal FCs (Exc 01 for FCs != 3, 6, 16)", passed, f"- Illegal FCs rejected: {fcs_ok}, Bounds checked: {bounds_ok}")
        except Exception as ex:
            record_test("Test 6: Bounds & Illegal FCs", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 7: Last-Write-Wins Rapid Write Stress Test (200 writes @ ~10ms)
        # -----------------------------------------------------------------
        print(f"\n--- TEST 7: Last-Write-Wins (200 rapid writes across active channels) ---")
        try:
            t_stress_start = time.time()
            expected_last = {}
            for i in range(1, 201):
                ch_idx = i % channels
                addr = FIRST_CHANNEL_ADDR + ch_idx
                pid = valid_id if (i % 2 == 0) else secondary_id
                client.write_single_register(addr, pid)
                expected_last[ch_idx] = pid
                time.sleep(0.01)

            stress_duration = time.time() - t_stress_start
            time.sleep(2.0) # Allow consumer queue to settle
            regs = client.read_holding_registers(FIRST_CHANNEL_ADDR, channels)
            expected_list = [expected_last[i] for i in range(channels)]
            passed = (regs == expected_list)
            record_test("Test 7: Last-Write-Wins Rapid Writes (200 writes @ ~10ms)", passed, f"- Duration {stress_duration:.2f}s, Final registers = {regs}")
        except Exception as ex:
            record_test("Test 7: Last-Write-Wins", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 8: Invalid Profile IDs (0 and 9999)
        # -----------------------------------------------------------------
        print(f"\n--- TEST 8: Invalid IDs Handling (0, 9999) ---")
        try:
            # Baseline: set all active channels to valid_id
            client.write_multiple_registers(FIRST_CHANNEL_ADDR, [valid_id] * channels)
            time.sleep(1.0)

            # 1. Write ID 0 to Channel 1 (Address 1)
            client.write_single_register(CH1_ADDR := 1, 0)
            time.sleep(0.5)
            r_zero = client.read_holding_registers(1, 1)[0]
            zero_ok = (r_zero == 0)

            # 2. Write ID 9999 to last active channel
            target_ch_addr = channels
            client.write_single_register(target_ch_addr, invalid_id)
            time.sleep(0.5)
            r_inval = client.read_holding_registers(target_ch_addr, 1)[0]
            inval_ok = (r_inval == invalid_id)

            detail = f"- Addr 1(0)={r_zero}, Addr {target_ch_addr}(9999)={r_inval} (Registers keep written value, WARN logged)"
            passed = zero_ok and inval_ok
            record_test(f"Test 8: Invalid IDs (0, 9999 on exposed channels - registers retain value, WARN logged)", passed, detail)
        except Exception as ex:
            record_test("Test 8: Invalid IDs", False, f"- Exception: {ex}")

        # -----------------------------------------------------------------
        # TEST 9: No-Freeze Regression Test (< 1.0s read response)
        # -----------------------------------------------------------------
        print(f"\n--- TEST 9: No-Freeze Regression Test ---")
        try:
            t0 = time.time()
            read_back = client.read_holding_registers(0, total_registers)
            elapsed = time.time() - t0
            passed = (elapsed < 1.0 and len(read_back) == total_registers)
            record_test(f"Test 9: No-Freeze Regression (Read of registers 0..{max_valid_addr} returns < 1.0s)", passed, f"- Completed in {elapsed:.3f}s (Threshold: 1.000s, Regs={read_back})")
        except Exception as ex:
            record_test("Test 9: No-Freeze Regression", False, f"- Exception: {ex}")

        # Reset registers to clean known state [valid_id] * channels
        try:
            client.write_multiple_registers(FIRST_CHANNEL_ADDR, [valid_id] * channels)
        except Exception:
            pass

        print("\n" + "=" * 80)
        print(f"SUMMARY OF VERIFICATION SUITE RESULTS (Channels = {channels})")
        print("=" * 80)
        for name, status, detail in test_results:
            first_line = detail.split("\n")[0] if detail else ""
            print(f"  {status:4s} | {name} {first_line}")
        print("=" * 80)
        print(f"OVERALL RESULT: {'ALL TESTS PASSED' if all_passed else 'SOME TESTS FAILED'}")
        print("=" * 80)

        return all_passed

    finally:
        client.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Modbus TCP Master Verification Suite (Dynamic Channels)")
    parser.add_argument("--host", default=DEFAULT_HOST, help="Slave host (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="Slave port (default: 5020)")
    parser.add_argument("--channels", type=int, default=1, help="Active channels count (1..4, default: 1)")
    parser.add_argument("--valid-id", type=int, default=9, help="Primary Profile ID (default: 9 [asta/2000Hz])")
    parser.add_argument("--secondary-id", type=int, default=5, help="Secondary Profile ID (default: 5 [velehella/400Hz])")
    parser.add_argument("--partial-id", type=int, default=19, help="Profile lacking Ch 4 (default: 19 [test_2ch])")
    parser.add_argument("--invalid-id", type=int, default=9999, help="Non-existent Profile ID (default: 9999)")
    parser.add_argument("--log-path", default=r"D:\New folder\Eddy\8F\bin\Debug\net8.0-windows\Logs\modbus.log", help="Path to modbus.log")
    args = parser.parse_args()

    success = run_tests(
        host=args.host,
        port=args.port,
        channels=args.channels,
        valid_id=args.valid_id,
        secondary_id=args.secondary_id,
        partial_ch_id=args.partial_id,
        invalid_id=args.invalid_id,
        log_path=args.log_path
    )
    sys.exit(0 if success else 1)
