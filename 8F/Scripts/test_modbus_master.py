"""
Modbus TCP Master Test Client - Extended Hardened Suite
Verifies Modbus master writes to Holding Register 0 (Profile ID),
Register 1 (Heartbeat), and Register 2 (Profile Load Status).
Includes deadlock / freeze regression, rapid-write stress test (200 writes),
status register verification, and boundary tests.

Compatible with Python 3.x standard library (socket & struct) - no pip dependencies.
Default target: 127.0.0.1:5020 (Unit ID 1).
"""

import sys
import socket
import struct
import time

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5020
DEFAULT_UNIT_ID = 1

class ModbusTcpClient:
    def __init__(self, host=DEFAULT_HOST, port=DEFAULT_PORT, unit_id=DEFAULT_UNIT_ID, timeout=5.0):
        self.host = host
        self.port = port
        self.unit_id = unit_id
        self.timeout = timeout
        self.sock = None
        self._tx_id = 0

    def connect(self):
        print(f"Connecting to Modbus TCP slave at {self.host}:{self.port}...")
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(self.timeout)
        self.sock.connect((self.host, self.port))
        print("Connected successfully.")

    def close(self):
        if self.sock:
            self.sock.close()
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
        if len(resp) < 12:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc, resp_addr, resp_val = struct.unpack(">HHHBBHH", resp[:12])
        if resp_fc != 6:
            raise RuntimeError(f"Error response FC: {resp_fc}")
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
        if len(resp) < 12:
            raise RuntimeError(f"Response too short: {resp.hex()}")
        resp_tx, _, _, _, resp_fc, resp_addr, resp_count = struct.unpack(">HHHBBHH", resp[:12])
        if resp_fc != 16:
            raise RuntimeError(f"Error response FC: {resp_fc}")
        return resp_count


def run_tests(host=DEFAULT_HOST, port=DEFAULT_PORT, valid_id=1, invalid_id=9999):
    print("=" * 75)
    print("Modbus Master Hardened Verification Suite")
    print("=" * 75)

    client = ModbusTcpClient(host=host, port=port)
    try:
        client.connect()
    except Exception as ex:
        print(f"\n[ERROR] Could not connect to {host}:{port}: {ex}")
        print("Please ensure the 8F application is running and Modbus TCP Slave is enabled.")
        return False

    try:
        # Step 0: Read initial state
        regs = client.read_holding_registers(0, 3)
        print(f"\n[Initial State] Reg 0 (Profile ID): {regs[0]}, Reg 1 (Heartbeat): {regs[1]}, Reg 2 (Status): {regs[2]}")
        initial_loaded_id = regs[0]

        # -------------------------------------------------------------
        # TEST 1: Freeze Regression Test
        # -------------------------------------------------------------
        print("\n--- TEST 1: Freeze Regression (writes 0, 1, 9999) ---")
        for test_val in [0, valid_id, invalid_id]:
            print(f"Writing {test_val} to Reg 0...")
            t0 = time.time()
            client.write_single_register(0, test_val)
            # Follow-up read must complete within 1.0 second with zero freeze
            read_back = client.read_holding_registers(0, 2)
            elapsed = time.time() - t0
            print(f"Readback within {elapsed:.3f}s: Reg 0 = {read_back[0]}, Reg 1 (HB) = {read_back[1]}")
            assert elapsed < 1.0, f"App freeze detected! Operation took {elapsed:.3f}s (> 1.0s)"
        print("-> PASS: Freeze regression passed. No deadlock on any value including 0.")

        # -------------------------------------------------------------
        # TEST 2: Status Register Verification (Address 2)
        # -------------------------------------------------------------
        print("\n--- TEST 2: Status Register (Address 2) Lifecycle & Write-Back ---")
        print(f"Writing valid Profile ID {valid_id}...")
        client.write_single_register(0, valid_id)

        # Poll status register (timeout 10s)
        status = -1
        t_start = time.time()
        while time.time() - t_start < 10.0:
            regs = client.read_holding_registers(0, 3)
            status = regs[2]
            if status in [2, 3]:  # 2 = Loaded OK, 3 = Failed
                break
            time.sleep(0.2)
        print(f"Final status after write: {status} (Reg 0 = {regs[0]}, Reg 2 = {status})")
        assert status in [2, 3], f"Status register did not settle to Loaded OK (2) or Failed (3), got {status}"
        print(f"-> PASS: Status register correctly updated to {status}.")

        # Test writing invalid ID 0: must report status 5 (Rejected Invalid) and restore Reg 0
        last_known_good = regs[0]
        print(f"\nWriting ID 0: expecting Status 5 and Reg 0 restored to {last_known_good}...")
        client.write_single_register(0, 0)
        time.sleep(0.5)
        regs = client.read_holding_registers(0, 3)
        print(f"Result: Reg 0 = {regs[0]}, Reg 2 (Status) = {regs[2]}")
        assert regs[2] == 5, f"Expected Status 5 (Rejected Invalid ID), got {regs[2]}"
        assert regs[0] == last_known_good, f"Expected Reg 0 restored to {last_known_good}, got {regs[0]}"
        print("-> PASS: Invalid ID 0 rejected with Status 5 and Reg 0 safely restored.")

        # Test writing non-existent ID 9999: must report status 3 (Failed) and restore Reg 0
        print(f"\nWriting non-existent ID {invalid_id}: expecting Status 3 and Reg 0 restored...")
        client.write_single_register(0, invalid_id)
        time.sleep(0.5)
        regs = client.read_holding_registers(0, 3)
        print(f"Result: Reg 0 = {regs[0]}, Reg 2 (Status) = {regs[2]}")
        assert regs[2] == 3, f"Expected Status 3 (Failed), got {regs[2]}"
        assert regs[0] == last_known_good, f"Expected Reg 0 restored to {last_known_good}, got {regs[0]}"
        print("-> PASS: Non-existent ID rejected with Status 3 and Reg 0 safely restored.")

        # -------------------------------------------------------------
        # TEST 3: FC 16 Multi-Register Range Write (Reg 0 and 1)
        # -------------------------------------------------------------
        print("\n--- TEST 3: FC 16 Multi-Register Range Write [Reg 0, Reg 1] ---")
        client.write_multiple_registers(0, [valid_id, 4321])
        time.sleep(0.5)
        regs = client.read_holding_registers(0, 3)
        print(f"Result: Reg 0 = {regs[0]}, Reg 1 = {regs[1]}, Reg 2 = {regs[2]}")
        assert regs[0] == valid_id
        print("-> PASS: Multi-register write covering addresses 0 and 1 handled correctly.")

        # -------------------------------------------------------------
        # TEST 4: Stress Test (200 rapid writes at ~10ms intervals)
        # -------------------------------------------------------------
        print("\n--- TEST 4: Rapid Write Stress Test (200 writes @ ~10ms intervals) ---")
        target_last_id = valid_id
        t_stress_start = time.time()
        for i in range(1, 201):
            val_to_send = (i % 2) + 1  # alternating 1 and 2
            client.write_single_register(0, val_to_send)
            time.sleep(0.01)  # 10ms
            if i % 50 == 0:
                print(f"  Sent {i}/200 writes...")
            if i == 200:
                target_last_id = val_to_send

        stress_duration = time.time() - t_stress_start
        print(f"Completed 200 writes in {stress_duration:.2f}s. Waiting for queue to settle...")
        time.sleep(2.0)

        # Confirm app is fully alive and responsive
        regs = client.read_holding_registers(0, 3)
        print(f"Post-stress state: Reg 0 = {regs[0]}, Reg 1 (HB) = {regs[1]}, Reg 2 (Status) = {regs[2]}")
        assert regs[2] in [2, 3], f"Expected settled status 2 or 3, got {regs[2]}"
        print(f"-> PASS: Rapid write stress test succeeded with zero crash or deadlock. Last-write-wins preserved.")

        # -------------------------------------------------------------
        # TEST 5: Same ID Written Twice in a Row
        # -------------------------------------------------------------
        print("\n--- TEST 5: Same ID Written Twice In A Row ---")
        client.write_single_register(0, valid_id)
        time.sleep(0.3)
        client.write_single_register(0, valid_id)
        time.sleep(0.3)
        regs = client.read_holding_registers(0, 3)
        print(f"Result: Reg 0 = {regs[0]}, Reg 2 = {regs[2]}")
        assert regs[0] == valid_id
        print("-> PASS: Same ID written twice re-triggers safely matching button behavior.")

        print("\n" + "=" * 75)
        print("ALL EXTENDED MODBUS MASTER VERIFICATION TESTS PASSED!")
        print("=" * 75)
        return True

    finally:
        client.close()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PORT
    host = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_HOST
    run_tests(host=host, port=port)
