#!/usr/bin/env python3
"""
Modbus TCP Slave Simulator — GUI edition
========================================
© 2026 My_Company. All rights reserved.

A generalised Modbus TCP *server* (slave) with a Tkinter control panel.
Stands in for an ESP32 / STM32 / any Modbus master's target device so you can
develop and test a Modbus TCP master (client) with no hardware attached.

What it does
------------
  * Serves all four Modbus object types at once:
        - Holding Registers   (read/write, 16-bit)
        - Input Registers     (read-only to the master, 16-bit)
        - Coils               (read/write, 1-bit)
        - Discrete Inputs     (read-only to the master, 1-bit)
  * Start / Stop the server from the GUI (configurable host / port / size).
  * Live editable value table — change a register/coil while the server runs
    and the connected master reads the new value immediately. This is how you
    feed "sensor" values (input registers / discrete inputs) to the master.
  * Real-time traffic log — every read and write the master performs is
    printed with a timestamp, object type and address.

Network reachability
--------------------
  * Binds to 0.0.0.0 by default so OTHER machines on the LAN can connect.
    On Start it logs the actual IP(s) a remote master should point at.
    Bind to 127.0.0.1 and it will warn you that only this PC can connect.
  * On Windows it auto-adds an inbound firewall allow-rule for the port
    ("Open port in firewall" checkbox). If it can't (not run as admin,
    or non-Windows), it logs the exact command for you to run once.

Install
-------
    pip install "pymodbus>=3.0,<3.13"

    IMPORTANT — pymodbus version:
    pymodbus 3.13 rewrote the datastore around its new simulator (SimData) and
    removed the per-block getValues/setValues hook this tool uses for the live
    table and the traffic log. So this app targets the classic datastore, which
    is present in 3.0 through 3.12. On 3.13+ the app still opens but Start is
    disabled with an on-screen note. (Tested against pymodbus 3.12.)

Run
---
    python mb_slave_gui.py

    Port 502 is the standard Modbus port but needs root/admin. 5020 is used as
    the default here for unprivileged local testing. Point your master at the
    same host/port and unit id 1.

Author's note: unit-id handling is "single" mode — the slave answers requests
for any unit id (matches the common single-device test setup).
"""

import asyncio
import ctypes
import os
import queue
import random
import socket
import subprocess
import threading
import time
import tkinter as tk
from tkinter import ttk, messagebox

# ---------------------------------------------------------------------------
# pymodbus import + capability probe (handles the 3.0 -> 3.12 -> 3.13 churn)
# ---------------------------------------------------------------------------
PYMODBUS_OK = True
PYMODBUS_ERR = ""
PYMODBUS_VER = "?"

try:
    import pymodbus
    PYMODBUS_VER = getattr(pymodbus, "__version__", "?")
    from pymodbus.datastore import ModbusServerContext, ModbusSequentialDataBlock

    # ModbusSlaveContext was renamed to ModbusDeviceContext around 3.11.
    try:
        from pymodbus.datastore import ModbusSlaveContext as DeviceContext
    except ImportError:
        from pymodbus.datastore import ModbusDeviceContext as DeviceContext

    from pymodbus.server import ModbusTcpServer

    # Probe: classic datastore is 0-based and exposes getValues/setValues we can
    # subclass. 3.13+ raises on address 0 and drops those methods.
    _probe = ModbusSequentialDataBlock(0, [0, 0])
    if not (hasattr(_probe, "getValues") and hasattr(_probe, "setValues")):
        raise RuntimeError("datastore has no getValues/setValues hook")
except Exception as exc:  # noqa: BLE001 - want any failure to disable Start cleanly
    PYMODBUS_OK = False
    PYMODBUS_ERR = (
        f"pymodbus {PYMODBUS_VER} is not compatible with this tool "
        f"({exc}).\n\nInstall a classic-datastore version:\n"
        f'    pip install "pymodbus>=3.0,<3.13"'
    )

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
COMPANY = ""          # <-- put your company / your name here

# Classic pymodbus datastore is 1-based: a master request for protocol address
# A is served from block index A+1. We pad every block by one slot and offset
# all GUI access by +1 so that "address A" in the GUI is exactly the address the
# master uses on the wire.
OFFSET = 1

# key, label, is_bit, writable-by-master
OBJ_TYPES = [
    ("hr", "Holding Registers", False, True),
    ("ir", "Input Registers", False, False),
    ("co", "Coils", True, True),
    ("di", "Discrete Inputs", True, False),
]
OBJ_LABELS = [t[1] for t in OBJ_TYPES]
LABEL_TO_KEY = {t[1]: t[0] for t in OBJ_TYPES}
KEY_INFO = {t[0]: t for t in OBJ_TYPES}

# 0.0.0.0 = listen on ALL network interfaces so other machines on the LAN can
# connect. Use 127.0.0.1 only if you want to restrict to this PC.
DEFAULT_HOST = "0.0.0.0"
DEFAULT_PORT = 502
DEFAULT_COUNT = 10
MAX_LOG_LINES = 2000

# ---------------------------------------------------------------------------
# Network / firewall helpers
# Goal: make the listening port reachable from other machines on the LAN and
# report any blockage in the traffic log instead of failing silently.
# ---------------------------------------------------------------------------
IS_WINDOWS = (os.name == "nt")
FW_RULE_PREFIX = "ModbusSlaveSim"
LOOPBACK_HOSTS = {"127.0.0.1", "localhost", "::1"}
ANY_HOSTS = {"0.0.0.0", "::", ""}


def _no_window_kwargs():
    """Stop netsh from flashing a console window when we're a windowed exe."""
    if IS_WINDOWS:
        return {"creationflags": 0x08000000}  # CREATE_NO_WINDOW
    return {}


def is_admin():
    """True if the process can add a firewall rule (admin on Windows / root)."""
    if IS_WINDOWS:
        try:
            return bool(ctypes.windll.shell32.IsUserAnAdmin())
        except Exception:
            return False
    return hasattr(os, "geteuid") and os.geteuid() == 0


def local_ipv4s():
    """Best-effort list of this machine's LAN IPv4 addresses — i.e. what a
    remote master should point at. Excludes loopback. Sends no packets."""
    ips = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if not ip.startswith("127."):
                ips.add(ip)
    except Exception:
        pass
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            s.connect(("8.8.8.8", 80))  # no traffic sent; just picks the NIC
            ip = s.getsockname()[0]
            if not ip.startswith("127."):
                ips.add(ip)
        finally:
            s.close()
    except Exception:
        pass
    return sorted(ips)


def _run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, **_no_window_kwargs())


def firewall_rule_name(port):
    return f"{FW_RULE_PREFIX}_{port}"


def firewall_rule_exists(port):
    if not IS_WINDOWS:
        return False
    try:
        r = _run(["netsh", "advfirewall", "firewall", "show", "rule",
                  f"name={firewall_rule_name(port)}"])
        return r.returncode == 0
    except Exception:
        return False


def firewall_add_rule(port):
    """Add an inbound allow rule for TCP <port>. Returns (ok, detail)."""
    if not IS_WINDOWS:
        return (False, "not Windows")
    try:
        r = _run(["netsh", "advfirewall", "firewall", "add", "rule",
                  f"name={firewall_rule_name(port)}", "dir=in", "action=allow",
                  "protocol=TCP", f"localport={port}"])
        if r.returncode == 0:
            return (True, "rule added")
        detail = (r.stderr or r.stdout or "").strip() or f"netsh exit {r.returncode}"
        return (False, detail.splitlines()[0] if detail else "netsh failed")
    except Exception as exc:
        return (False, str(exc))


# ---------------------------------------------------------------------------
# Data block that reports every master-driven read / write to a sink callback
# ---------------------------------------------------------------------------
class LoggingBlock(ModbusSequentialDataBlock):
    """A sequential data block that calls sink(op, kind, proto_addr, values)
    on every access made by the server (i.e. by the connected master).

    GUI-side access uses gui_get / gui_set, which bypass the sink so table
    refreshes and operator edits do not spam the traffic log."""

    def __init__(self, count, fill, kind, sink):
        super().__init__(0, [fill] * (count + OFFSET))
        self.kind = kind
        self.sink = sink
        self.count = count

    # --- server-side (master) access: logged --------------------------------
    def getValues(self, address, count=1):
        vals = super().getValues(address, count)
        try:
            self.sink("READ", self.kind, address - OFFSET, list(vals))
        except Exception:
            pass
        return vals

    def setValues(self, address, values):
        super().setValues(address, values)
        vals = list(values) if isinstance(values, (list, tuple)) else [values]
        try:
            self.sink("WRITE", self.kind, address - OFFSET, vals)
        except Exception:
            pass

    # --- GUI-side access: not logged, protocol addressing -------------------
    def gui_get(self, addr, count=1):
        return list(super().getValues(addr + OFFSET, count))

    def gui_set(self, addr, values):
        if not isinstance(values, (list, tuple)):
            values = [values]
        super().setValues(addr + OFFSET, list(values))


# ---------------------------------------------------------------------------
# Background server thread — owns its own asyncio loop
# ---------------------------------------------------------------------------
class _ServerThread(threading.Thread):
    def __init__(self, context, address):
        super().__init__(daemon=True)
        self.context = context
        self.address = address
        self.loop = None
        self.server = None
        self.ready = threading.Event()
        self.err = None

    def run(self):
        self.loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self.loop)
        try:
            self.loop.run_until_complete(self._serve())
        except Exception as exc:  # bind failures etc.
            self.err = exc
            self.ready.set()
        finally:
            try:
                self.loop.close()
            except Exception:
                pass

    async def _serve(self):
        self.server = ModbusTcpServer(self.context, address=self.address)
        self.loop.call_soon(self.ready.set)
        await self.server.serve_forever()

    def stop(self):
        if self.server and self.loop and self.loop.is_running():
            try:
                fut = asyncio.run_coroutine_threadsafe(
                    self.server.shutdown(), self.loop
                )
                fut.result(timeout=5)
            except Exception:
                pass


# ---------------------------------------------------------------------------
# Engine: builds the datastore and manages the server lifecycle
# ---------------------------------------------------------------------------
class SlaveEngine:
    def __init__(self, sink):
        self.sink = sink
        self.blocks = {}
        self.thread = None

    def _build_context(self, count):
        self.blocks = {}
        for key, _label, is_bit, _wr in OBJ_TYPES:
            fill = False if is_bit else 0
            self.blocks[key] = LoggingBlock(count, fill, key, self.sink)
        device = DeviceContext(
            hr=self.blocks["hr"],
            ir=self.blocks["ir"],
            co=self.blocks["co"],
            di=self.blocks["di"],
        )
        try:
            return ModbusServerContext(slaves=device, single=True)
        except TypeError:
            return ModbusServerContext(devices=device, single=True)

    def start(self, host, port, count):
        context = self._build_context(count)
        self.thread = _ServerThread(context, (host, port))
        self.thread.start()
        started = self.thread.ready.wait(timeout=5)
        if self.thread.err:
            err = self.thread.err
            self.thread = None
            raise err
        if not started:
            self.thread = None
            raise RuntimeError("Server did not start within 5 s.")

    def stop(self):
        if self.thread:
            self.thread.stop()
            self.thread.join(timeout=5)
        self.thread = None

    @property
    def running(self):
        return self.thread is not None and self.thread.is_alive()


# ---------------------------------------------------------------------------
# GUI
# ---------------------------------------------------------------------------
class SlaveApp:
    def __init__(self, root):
        self.root = root
        self.root.title(f"Modbus TCP Slave Simulator")
        self.root.geometry("980x680")
        self.root.minsize(860, 580)

        self.log_q = queue.Queue()
        self.engine = SlaveEngine(self._sink)
        self.current_key = "hr"
        self.count = DEFAULT_COUNT
        self._last_snapshot = {}  # addr -> value, for cheap diff refresh
        self._editor = None
        self._log_paused = tk.BooleanVar(value=False)
        self._autoscroll = tk.BooleanVar(value=True)
        self._open_fw = tk.BooleanVar(value=True)

        self._build_style()
        self._build_widgets()
        self._set_running_ui(False)

        if not PYMODBUS_OK:
            self.start_btn.configure(state="disabled")
            self._append_log_line("!! " + PYMODBUS_ERR.replace("\n", " "), "warn")
            messagebox.showwarning("pymodbus version", PYMODBUS_ERR)

        # background pumps
        self.root.after(80, self._pump_log)
        self.root.after(350, self._refresh_table)
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    # ----- styling ----------------------------------------------------------
    def _build_style(self):
        style = ttk.Style()
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("Treeview", rowheight=22, font=("TkFixedFont", 10))
        style.configure("Treeview.Heading", font=("TkDefaultFont", 10, "bold"))
        style.configure("Status.TLabel", padding=4)

    # ----- widget construction ---------------------------------------------
    def _build_widgets(self):
        # --- config bar -----------------------------------------------------
        top = ttk.LabelFrame(self.root, text="Settings")
        top.pack(side="top", fill="x", padx=8, pady=(8, 4))

        ttk.Label(top, text="My IP").grid(row=0, column=0, padx=(8, 2), pady=6, sticky="e")
        self.host_var = tk.StringVar(value=DEFAULT_HOST)
        self.host_entry = ttk.Entry(top, textvariable=self.host_var, width=16)
        self.host_entry.grid(row=0, column=1, padx=2, pady=6)

        ttk.Label(top, text="Port").grid(row=0, column=2, padx=(12, 2), pady=6, sticky="e")
        self.port_var = tk.StringVar(value=str(DEFAULT_PORT))
        self.port_entry = ttk.Entry(top, textvariable=self.port_var, width=8)
        self.port_entry.grid(row=0, column=3, padx=2, pady=6)

        ttk.Label(top, text="Points/table").grid(row=0, column=4, padx=(12, 2), pady=6, sticky="e")
        self.count_var = tk.StringVar(value=str(DEFAULT_COUNT))
        self.count_entry = ttk.Entry(top, textvariable=self.count_var, width=8)
        self.count_entry.grid(row=0, column=5, padx=2, pady=6)

        self.start_btn = ttk.Button(top, text="Start", command=self._toggle_server, width=10)
        self.start_btn.grid(row=0, column=6, padx=(16, 8), pady=6)

        # status dot + text
        self.status_canvas = tk.Canvas(top, width=14, height=14, highlightthickness=0)
        self.status_canvas.grid(row=0, column=7, padx=(8, 2), pady=6)
        self._dot = self.status_canvas.create_oval(2, 2, 12, 12, fill="#c0392b", outline="")
        self.status_var = tk.StringVar(value="Stopped")
        ttk.Label(top, textvariable=self.status_var).grid(row=0, column=8, padx=(0, 10), pady=6, sticky="w")

        top.grid_columnconfigure(9, weight=1)
        ttk.Label(
            top,
            text=COMPANY,
            foreground="#666",
        ).grid(row=0, column=9, padx=10, pady=6, sticky="e")

        # second row: firewall option
        self.fw_check = ttk.Checkbutton(
            top, text="Open port in firewall on Start", variable=self._open_fw
        )
        self.fw_check.grid(row=1, column=0, columnspan=6, padx=(8, 2), pady=(0, 6), sticky="w")

        # --- main split: table (left) / log (right) -------------------------
        main = ttk.PanedWindow(self.root, orient="horizontal")
        main.pack(side="top", fill="both", expand=True, padx=8, pady=4)

        # left: registers/coils table + controls
        left = ttk.Frame(main)
        main.add(left, weight=3)

        ctrl = ttk.Frame(left)
        ctrl.pack(side="top", fill="x", pady=(0, 4))
        ttk.Label(ctrl, text="Object:").pack(side="left", padx=(0, 4))
        self.type_var = tk.StringVar(value=OBJ_LABELS[0])
        self.type_combo = ttk.Combobox(
            ctrl, textvariable=self.type_var, values=OBJ_LABELS,
            state="readonly", width=18,
        )
        self.type_combo.pack(side="left")
        self.type_combo.bind("<<ComboboxSelected>>", self._on_type_change)

        ttk.Button(ctrl, text="Zero", width=7, command=lambda: self._bulk_fill("zero")).pack(side="left", padx=(10, 2))
        ttk.Button(ctrl, text="Max", width=7, command=lambda: self._bulk_fill("max")).pack(side="left", padx=2)
        ttk.Button(ctrl, text="Random", width=8, command=lambda: self._bulk_fill("rand")).pack(side="left", padx=2)

        ttk.Label(ctrl, text="Go to:").pack(side="left", padx=(12, 2))
        self.goto_var = tk.StringVar()
        goto = ttk.Entry(ctrl, textvariable=self.goto_var, width=8)
        goto.pack(side="left")
        goto.bind("<Return>", self._on_goto)

        self.hint_var = tk.StringVar(value="Double-click a value to edit.")
        ttk.Label(left, textvariable=self.hint_var, foreground="#555").pack(side="bottom", fill="x")

        tree_wrap = ttk.Frame(left)
        tree_wrap.pack(side="top", fill="both", expand=True)
        self.tree = ttk.Treeview(tree_wrap, show="headings", selectmode="browse")
        vsb = ttk.Scrollbar(tree_wrap, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=vsb.set)
        self.tree.pack(side="left", fill="both", expand=True)
        vsb.pack(side="right", fill="y")
        self.tree.bind("<Double-1>", self._begin_edit)
        self._configure_columns("hr")

        # right: traffic log
        right = ttk.Frame(main)
        main.add(right, weight=2)
        logbar = ttk.Frame(right)
        logbar.pack(side="top", fill="x")
        ttk.Label(logbar, text="Traffic log").pack(side="left")
        ttk.Checkbutton(logbar, text="Autoscroll", variable=self._autoscroll).pack(side="right")
        ttk.Checkbutton(logbar, text="Pause", variable=self._log_paused).pack(side="right", padx=6)
        ttk.Button(logbar, text="Clear", width=7, command=self._clear_log).pack(side="right")

        self.log = tk.Text(right, wrap="none", height=10, font=("TkFixedFont", 9),
                           background="#101418", foreground="#d6d6d6", insertbackground="#d6d6d6")
        logsb = ttk.Scrollbar(right, orient="vertical", command=self.log.yview)
        self.log.configure(yscrollcommand=logsb.set, state="disabled")
        self.log.pack(side="left", fill="both", expand=True)
        logsb.pack(side="right", fill="y")
        self.log.tag_configure("read", foreground="#6cb6ff")
        self.log.tag_configure("write", foreground="#f2a541")
        self.log.tag_configure("info", foreground="#8fbf7f")
        self.log.tag_configure("warn", foreground="#ff6b6b")

        # status bar
        self.footer_var = tk.StringVar(value="Idle.")
        ttk.Label(self.root, textvariable=self.footer_var, style="Status.TLabel",
                  relief="sunken", anchor="w").pack(side="bottom", fill="x")

    # ----- table columns ----------------------------------------------------
    def _configure_columns(self, key):
        is_bit = KEY_INFO[key][2]
        if is_bit:
            cols = ("addr", "value")
            self.tree.configure(columns=cols)
            self.tree.heading("addr", text="Address")
            self.tree.heading("value", text="State")
            self.tree.column("addr", width=160, anchor="w")
            self.tree.column("value", width=120, anchor="center")
        else:
            cols = ("addr", "hex", "dec")
            self.tree.configure(columns=cols)
            self.tree.heading("addr", text="Address")
            self.tree.heading("hex", text="Hex")
            self.tree.heading("dec", text="Decimal")
            self.tree.column("addr", width=160, anchor="w")
            self.tree.column("hex", width=120, anchor="center")
            self.tree.column("dec", width=120, anchor="center")

    # ----- populate / refresh table ----------------------------------------
    def _addr_text(self, a):
        return f"{a}  (0x{a:04X})"

    def _row_values(self, key, addr, val):
        if KEY_INFO[key][2]:
            return (self._addr_text(addr), "ON" if val else "OFF")
        v = int(val) & 0xFFFF
        return (self._addr_text(addr), f"0x{v:04X}", str(v))

    def _rebuild_table(self):
        self.tree.delete(*self.tree.get_children())
        self._last_snapshot = {}
        key = self.current_key
        if not self.engine.running:
            return
        block = self.engine.blocks.get(key)
        if block is None:
            return
        vals = block.gui_get(0, self.count)
        for addr in range(self.count):
            v = vals[addr]
            self.tree.insert("", "end", iid=str(addr), values=self._row_values(key, addr, v))
            self._last_snapshot[addr] = bool(v) if KEY_INFO[key][2] else (int(v) & 0xFFFF)

    def _refresh_table(self):
        try:
            if self.engine.running and self._editor is None:
                key = self.current_key
                block = self.engine.blocks.get(key)
                if block is not None:
                    vals = block.gui_get(0, self.count)
                    is_bit = KEY_INFO[key][2]
                    for addr in range(self.count):
                        raw = vals[addr]
                        norm = bool(raw) if is_bit else (int(raw) & 0xFFFF)
                        if self._last_snapshot.get(addr) != norm:
                            self._last_snapshot[addr] = norm
                            iid = str(addr)
                            if self.tree.exists(iid):
                                self.tree.item(iid, values=self._row_values(key, addr, raw))
        finally:
            self.root.after(350, self._refresh_table)

    # ----- editing ----------------------------------------------------------
    def _begin_edit(self, event):
        if not self.engine.running:
            return
        if self._editor is not None:
            self._commit_edit()
        region = self.tree.identify("region", event.x, event.y)
        if region != "cell":
            return
        col = self.tree.identify_column(event.x)
        iid = self.tree.identify_row(event.y)
        if not iid:
            return
        key = self.current_key
        is_bit = KEY_INFO[key][2]
        value_col = "#2"  # both layouts: value/state is column 2
        if is_bit and col != value_col:
            return
        if not is_bit and col not in ("#2", "#3"):
            return

        addr = int(iid)
        block = self.engine.blocks[key]
        cur = block.gui_get(addr, 1)[0]
        if is_bit:
            block.gui_set(addr, [not bool(cur)])  # toggle on click
            self._last_snapshot[addr] = bool(not bool(cur))
            self.tree.item(iid, values=self._row_values(key, addr, not bool(cur)))
            self._log_edit(key, addr, not bool(cur))
            return

        # register: inline entry editor over the clicked cell
        x, y, w, h = self.tree.bbox(iid, col)
        edit_col = col
        init = f"0x{int(cur) & 0xFFFF:04X}" if edit_col == "#2" else str(int(cur) & 0xFFFF)
        self._editor = tk.Entry(self.tree)
        self._editor.insert(0, init)
        self._editor.select_range(0, "end")
        self._editor.place(x=x, y=y, width=w, height=h)
        self._editor.focus_set()
        self._editor_iid = iid
        self._editor_addr = addr
        self._editor.bind("<Return>", lambda e: self._commit_edit())
        self._editor.bind("<Escape>", lambda e: self._cancel_edit())
        self._editor.bind("<FocusOut>", lambda e: self._commit_edit())

    def _parse_register(self, text):
        text = text.strip().lower()
        if not text:
            raise ValueError("empty")
        base = 16 if text.startswith("0x") else 10
        v = int(text, base)
        if not (0 <= v <= 0xFFFF):
            raise ValueError("range")
        return v

    def _commit_edit(self):
        if self._editor is None:
            return
        text = self._editor.get()
        iid = self._editor_iid
        addr = self._editor_addr
        key = self.current_key
        try:
            v = self._parse_register(text)
        except ValueError:
            self.footer_var.set(f"Invalid value '{text}' — use 0..65535 or 0xFFFF.")
            self._cancel_edit()
            return
        block = self.engine.blocks[key]
        block.gui_set(addr, [v])
        self._last_snapshot[addr] = v
        self.tree.item(iid, values=self._row_values(key, addr, v))
        self._log_edit(key, addr, v)
        self._destroy_editor()

    def _cancel_edit(self):
        self._destroy_editor()

    def _destroy_editor(self):
        if self._editor is not None:
            self._editor.destroy()
            self._editor = None

    def _log_edit(self, key, addr, val):
        label = KEY_INFO[key][1]
        shown = ("ON" if val else "OFF") if KEY_INFO[key][2] else f"0x{int(val) & 0xFFFF:04X}"
        self.footer_var.set(f"Set {label}[{addr}] = {shown}")
        self._append_log_line(
            f"{time.strftime('%H:%M:%S')}  EDIT {key.upper()}[{addr:>5}] <- {shown}", "info"
        )

    # ----- bulk fill / goto -------------------------------------------------
    def _bulk_fill(self, mode):
        if not self.engine.running:
            return
        key = self.current_key
        is_bit = KEY_INFO[key][2]
        block = self.engine.blocks[key]
        if is_bit:
            if mode == "zero":
                vals = [False] * self.count
            elif mode == "max":
                vals = [True] * self.count
            else:
                vals = [bool(random.getrandbits(1)) for _ in range(self.count)]
        else:
            if mode == "zero":
                vals = [0] * self.count
            elif mode == "max":
                vals = [0xFFFF] * self.count
            else:
                vals = [random.randint(0, 0xFFFF) for _ in range(self.count)]
        block.gui_set(0, vals)
        self._rebuild_table()
        self.footer_var.set(f"{KEY_INFO[key][1]}: {mode} applied to {self.count} points.")

    def _on_goto(self, _event):
        text = self.goto_var.get().strip().lower()
        try:
            a = int(text, 16 if text.startswith("0x") else 10)
        except ValueError:
            return
        iid = str(a)
        if self.tree.exists(iid):
            self.tree.selection_set(iid)
            self.tree.see(iid)

    def _on_type_change(self, _event=None):
        self._destroy_editor()
        self.current_key = LABEL_TO_KEY[self.type_var.get()]
        self._configure_columns(self.current_key)
        self._rebuild_table()
        writable = KEY_INFO[self.current_key][3]
        note = "read/write for the master" if writable else "read-only for the master — set values here to simulate inputs"
        self.hint_var.set(f"{KEY_INFO[self.current_key][1]}: {note}. Double-click a value to edit.")

    # ----- server start/stop ------------------------------------------------
    def _toggle_server(self):
        if self.engine.running:
            self._stop_server()
        else:
            self._start_server()

    def _start_server(self):
        host = self.host_var.get().strip() or DEFAULT_HOST
        try:
            port = int(self.port_var.get())
            if not (1 <= port <= 65535):
                raise ValueError
        except ValueError:
            messagebox.showerror("Invalid port", "Port must be an integer 1..65535.")
            return
        try:
            count = int(self.count_var.get())
            if not (1 <= count <= 9999):
                raise ValueError
        except ValueError:
            messagebox.showerror("Invalid size", "Points/table must be an integer 1..9999.")
            return
        self.count = count
        try:
            self.engine.start(host, port, count)
        except Exception as exc:  # noqa: BLE001
            msg = self._describe_bind_error(exc, host, port)
            messagebox.showerror("Could not start server", msg)
            self._append_log_line(
                f"{time.strftime('%H:%M:%S')}  !! start failed: {msg}", "warn")
            return
        self._set_running_ui(True)
        self._rebuild_table()
        self._on_type_change()
        msg = f"Listening on {host}:{port}  ({count} points/table, unit id any)"
        self.status_var.set(f"Running · {host}:{port}")
        self.footer_var.set(msg)
        self._append_log_line(f"{time.strftime('%H:%M:%S')}  == server started: {msg}", "info")
        # report reachability + make sure the OS firewall isn't blocking us
        self._post_start_network_report(host, port)

    def _stop_server(self):
        self._destroy_editor()
        self.engine.stop()
        self._set_running_ui(False)
        self.tree.delete(*self.tree.get_children())
        self._last_snapshot = {}
        self.status_var.set("Stopped")
        self.footer_var.set("Server stopped.")
        self._append_log_line(f"{time.strftime('%H:%M:%S')}  == server stopped", "info")

    def _set_running_ui(self, running):
        self.status_canvas.itemconfigure(self._dot, fill="#27ae60" if running else "#c0392b")
        self.start_btn.configure(text="Stop" if running else "Start")
        state = "disabled" if running else "normal"
        for w in (self.host_entry, self.port_entry, self.count_entry):
            w.configure(state=state)
        if not PYMODBUS_OK:
            self.start_btn.configure(state="disabled")

    # ----- network reachability + firewall ----------------------------------
    def _post_start_network_report(self, host, port):
        """Tell the operator exactly where a remote master should connect, and
        warn loudly if we're bound somewhere only this PC can reach."""
        ts = time.strftime("%H:%M:%S")
        if host in ANY_HOSTS:
            ips = local_ipv4s()
            if ips:
                self._append_log_line(
                    f"{ts}  net: reachable from the LAN at "
                    + ", ".join(f"{ip}:{port}" for ip in ips), "info")
            else:
                self._append_log_line(
                    f"{ts}  net: bound to all interfaces on port {port} "
                    f"(couldn't detect a LAN IP — check your network).", "warn")
        elif host in LOOPBACK_HOSTS:
            self._append_log_line(
                f"{ts}  net: WARNING bound to loopback ({host}) — only THIS "
                f"machine can connect. Set 'My IP' to 0.0.0.0 (or this PC's "
                f"LAN IP) so other systems can reach it.", "warn")
        else:
            self._append_log_line(
                f"{ts}  net: bound to {host}:{port} (only that interface).", "info")

        # netsh can be slow; run off the UI thread and log via the queue.
        if self._open_fw.get():
            threading.Thread(target=self._firewall_worker, args=(port,),
                             daemon=True).start()
        else:
            self._append_log_line(
                f"{ts}  firewall: auto-open disabled — if a remote master "
                f"can't connect, allow inbound TCP {port} in your firewall.",
                "info")

    def _firewall_worker(self, port):
        """Runs in a background thread. Ensures the OS firewall allows the
        listening port; logs the result (and the fix) via the log queue."""
        def log(text, tag):
            self.log_q.put((f"{time.strftime('%H:%M:%S')}  {text}", tag))

        if not IS_WINDOWS:
            log(f"firewall: non-Windows host — if remote clients can't "
                f"connect, open inbound TCP {port} in your OS firewall "
                f"(e.g. sudo ufw allow {port}/tcp).", "info")
            return

        try:
            if firewall_rule_exists(port):
                log(f"firewall: port {port} already allowed "
                    f"(rule '{firewall_rule_name(port)}').", "info")
                return
        except Exception:
            pass

        if not is_admin():
            log(f"firewall: port {port} is NOT open and I can't add the rule "
                f"(app is not elevated). Run the exe as administrator, or run "
                f"this once in an admin Command Prompt:", "warn")
            log(f'  netsh advfirewall firewall add rule '
                f'name={firewall_rule_name(port)} dir=in action=allow '
                f'protocol=TCP localport={port}', "warn")
            return

        ok, detail = firewall_add_rule(port)
        if ok:
            log(f"firewall: opened inbound TCP {port} "
                f"(rule '{firewall_rule_name(port)}') — remote masters can "
                f"connect now.", "info")
        else:
            log(f"firewall: could NOT open port {port} — {detail}. Add the "
                f"rule manually if a remote master can't connect.", "warn")

    def _describe_bind_error(self, exc, host, port):
        """Turn a raw socket/bind exception into a plain, actionable message."""
        win = getattr(exc, "winerror", None)
        err = getattr(exc, "errno", None)
        # Access denied
        if win == 10013 or err == 13:
            if port < 1024:
                return (f"Access denied binding port {port}. Ports below 1024 "
                        f"need administrator rights — run the app as admin, or "
                        f"use a high port like 5020.")
            return (f"Access denied binding port {port}. A firewall or security "
                    f"policy is blocking the listener — run as admin / allow the "
                    f"app, then retry.")
        # Address already in use
        if win == 10048 or err in (48, 98):
            return (f"Port {port} is already in use (maybe a previous instance "
                    f"is still running). Close it or choose another port.")
        # Address not available on this machine
        if win == 10049 or err in (49, 99):
            return (f"'{host}' is not an address on this machine. Use 0.0.0.0 "
                    f"(all interfaces), 127.0.0.1, or one of this PC's own IPs.")
        return str(exc)

    # ----- traffic log ------------------------------------------------------
    def _sink(self, op, kind, addr, values):
        """Called from the SERVER thread. Must be thread-safe -> just enqueue."""
        ts = time.strftime("%H:%M:%S")
        if KEY_INFO.get(kind, (None, None, False))[2]:
            shown = "[" + ",".join("1" if v else "0" for v in values) + "]"
        else:
            shown = "[" + ",".join(f"0x{int(v) & 0xFFFF:04X}" for v in values) + "]"
        arrow = "->" if op == "READ" else "<-"
        tag = "read" if op == "READ" else "write"
        line = f"{ts}  {op:5} {kind.upper()}[{addr:>5}] x{len(values)} {arrow} {shown}"
        self.log_q.put((line, tag))

    def _pump_log(self):
        try:
            drained = 0
            while drained < 200:
                line, tag = self.log_q.get_nowait()
                if not self._log_paused.get():
                    self._append_log_line(line, tag)
                drained += 1
        except queue.Empty:
            pass
        finally:
            self.root.after(80, self._pump_log)

    def _append_log_line(self, line, tag="read"):
        self.log.configure(state="normal")
        self.log.insert("end", line + "\n", tag)
        # trim
        line_count = int(self.log.index("end-1c").split(".")[0])
        if line_count > MAX_LOG_LINES:
            self.log.delete("1.0", f"{line_count - MAX_LOG_LINES}.0")
        if self._autoscroll.get():
            self.log.see("end")
        self.log.configure(state="disabled")

    def _clear_log(self):
        self.log.configure(state="normal")
        self.log.delete("1.0", "end")
        self.log.configure(state="disabled")

    # ----- shutdown ---------------------------------------------------------
    def _on_close(self):
        try:
            if self.engine.running:
                self.engine.stop()
        finally:
            self.root.destroy()


def main():
    root = tk.Tk()
    SlaveApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
    