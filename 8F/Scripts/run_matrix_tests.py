"""
Automated Test Runner for Dynamic Modbus Channel Configurations
Tests Channel = 1, 2, 3, 4 and config validation tests (0, 5, abc, empty, missing).
"""

import sys
import os
import re
import time
import socket
import subprocess
import xml.etree.ElementTree as ET

WORKSPACE_DIR = r"d:\New folder\Eddy\8F"
EXE_PATH = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows\8F.exe")
EXE_DIR = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows")
CONFIG_DLL = os.path.join(WORKSPACE_DIR, r"bin\Debug\net8.0-windows\8F.dll.config")
CONFIG_APP = os.path.join(WORKSPACE_DIR, "App.config")
LOG_PATH = os.path.join(EXE_DIR, r"Logs\modbus.log")
TEST_SCRIPT = os.path.join(WORKSPACE_DIR, r"Scripts\test_modbus_master.py")


def read_file(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def write_file(path, content):
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def set_config_channel_value(path, value):
    content = read_file(path)
    pattern = r'<add\s+key="Channel"\s+value="[^"]*"\s*/>'
    if value is None:
        # Remove key
        content = re.sub(pattern, '<!-- <add key="Channel" value="removed" /> -->', content)
    else:
        replacement = f'<add key="Channel" value="{value}" />'
        if re.search(pattern, content):
            content = re.sub(pattern, replacement, content)
        else:
            # If commented out or missing, replace the comment
            comment_pattern = r'<!-- <add key="Channel" value="removed" /> -->'
            if re.search(comment_pattern, content):
                content = re.sub(comment_pattern, replacement, content)
            else:
                # Add before </appSettings>
                content = content.replace('</appSettings>', f'\t\t{replacement}\n\t</appSettings>')
    write_file(path, content)


def wait_for_port(port=5020, timeout=12.0):
    start = time.time()
    while time.time() - start < timeout:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(0.5)
            s.connect(("127.0.0.1", port))
            s.close()
            return True
        except Exception:
            time.sleep(0.3)
    return False


def wait_for_port_closed(port=5020, timeout=8.0):
    start = time.time()
    while time.time() - start < timeout:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(0.5)
            s.connect(("127.0.0.1", port))
            s.close()
            time.sleep(0.3)
        except Exception:
            return True
    return False


def kill_all_8f_processes():
    try:
        subprocess.run(["powershell", "-Command", "Get-Process -Name '8F' -ErrorAction SilentlyContinue | Stop-Process -Force"], capture_output=True)
    except Exception:
        pass
    time.sleep(1.0)


def start_8f():
    proc = subprocess.Popen([EXE_PATH], cwd=EXE_DIR)
    if not wait_for_port(5020, timeout=12.0):
        try:
            proc.terminate()
            proc.wait(timeout=2.0)
        except Exception:
            proc.kill()
        raise RuntimeError("8F did not start listening on port 5020 within timeout.")
    return proc


def stop_8f(proc):
    if proc:
        try:
            proc.terminate()
            proc.wait(timeout=3.0)
        except Exception:
            try:
                proc.kill()
                proc.wait(timeout=2.0)
            except Exception:
                pass
    kill_all_8f_processes()
    wait_for_port_closed(5020, timeout=5.0)


def get_latest_log_lines(count=20):
    if not os.path.exists(LOG_PATH):
        return []
    with open(LOG_PATH, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    return [l.strip() for l in lines[-count:]]


def run_matrix():
    backup_dll = read_file(CONFIG_DLL)
    backup_app = read_file(CONFIG_APP)

    matrix_results = []

    print("================================================================================")
    print("STARTING MODBUS TCP SLAVE CHANNEL CONFIGURATION MATRIX TESTS")
    print("================================================================================")

    try:
        kill_all_8f_processes()

        # Part 1: Channel = 1, 2, 3, 4
        for ch in [1, 2, 3, 4]:
            print(f"\n>>>>>>> TESTING CHANNEL = {ch} <<<<<<<")
            set_config_channel_value(CONFIG_DLL, str(ch))
            set_config_channel_value(CONFIG_APP, str(ch))

            proc = start_8f()
            time.sleep(1.0)

            # Check startup log
            logs = get_latest_log_lines(15)
            startup_log = [l for l in logs if "Started on port" in l or "Configuration 'Channel' read" in l]
            print(f"[LOG AT STARTUP] {startup_log[-2:] if len(startup_log) >= 2 else startup_log}")

            # Run test script
            cmd = [sys.executable, TEST_SCRIPT, "--channels", str(ch)]
            res = subprocess.run(cmd, capture_output=True, text=True)
            print(res.stdout)
            if res.stderr:
                print("[STDERR]", res.stderr)

            passed = (res.returncode == 0)
            matrix_results.append((f"Channel = {ch}", passed, f"Exit code {res.returncode}"))

            stop_8f(proc)
            time.sleep(1.0)

        # Part 2: Configuration Validation Tests
        validation_cases = [
            ("0", "Value 0 (Below Min)"),
            ("5", "Value 5 (Above Max)"),
            ("abc", "Value 'abc' (Non-numeric)"),
            ("", "Empty Value"),
            (None, "Missing Key entirely")
        ]

        for val, desc in validation_cases:
            print(f"\n>>>>>>> TESTING CONFIG VALIDATION: {desc} <<<<<<<")
            set_config_channel_value(CONFIG_DLL, val)
            set_config_channel_value(CONFIG_APP, val)

            proc = start_8f()
            time.sleep(1.0)

            logs = get_latest_log_lines(20)
            err_logs = [l for l in logs if "Configuration ERROR for 'Channel' setting" in l]
            has_error_logged = len(err_logs) > 0
            if has_error_logged:
                print(f"[VERIFIED ERROR LOG] {err_logs[-1]}")
            else:
                print("[ERROR] Expected configuration error was NOT found in modbus.log!")

            # Verify slave operates safely in fallback mode (Channel = 1, 2 registers)
            cmd = [sys.executable, TEST_SCRIPT, "--channels", "1"]
            res = subprocess.run(cmd, capture_output=True, text=True)
            print(res.stdout)

            passed = (res.returncode == 0) and has_error_logged
            matrix_results.append((f"Config Validation: {desc}", passed, f"Fallback to 1 OK, Error Logged={has_error_logged}"))

            stop_8f(proc)
            time.sleep(1.0)

    finally:
        print("\nRestoring original configuration files...")
        write_file(CONFIG_DLL, backup_dll)
        write_file(CONFIG_APP, backup_app)
        kill_all_8f_processes()
        print("Restoration complete.")

    print("\n" + "=" * 80)
    print("MATRIX TEST EXECUTION SUMMARY")
    print("=" * 80)
    all_ok = True
    for test_name, passed, detail in matrix_results:
        status = "PASS" if passed else "FAIL"
        if not passed:
            all_ok = False
        print(f"  {status:4s} | {test_name:<35s} | {detail}")
    print("=" * 80)
    print(f"OVERALL RESULT: {'ALL MATRIX TESTS PASSED' if all_ok else 'SOME MATRIX TESTS FAILED'}")
    print("=" * 80)

    return all_ok


if __name__ == "__main__":
    success = run_matrix()
    sys.exit(0 if success else 1)
