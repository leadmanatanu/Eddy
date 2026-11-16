# 8F — Diagnosis Report: Open Channel Selection, Modbus Slave Hang, and Open-Modbus Coupling

## 1. Executive Summary
This diagnosis investigates the three critical issues reported in `8F.sln`:
1. **Open does not load into the selected channel:** Open operations (from Database, File, Import) forced selection to Channel 1, resetting/overwriting channel state across all channels.
2. **Modbus slave hang on start and operation:** Starting or operating the Modbus slave causes UI freezes and deadlocks.
3. **Menu Open and Modbus are disconnected:** Menu Open does not update Modbus registers, Modbus master writes use a divergent code path, and concurrent writes cause race conditions and deadlocks.

---

## 2. Root Cause Analysis

### 2.1 Problem 1: Open Does Not Load into Selected Channel & Forces Channel 1

#### Evidence & Call Sites:
- **Forced Channel 1 Activation:**
  - `ViewModels/MenuItemViewModel.cs` line 976:
    ```csharp
    mainWindow.SelectCh1();
    ```
    This method (`Views/MainWindow.xaml.cs` line 2238) explicitly sets `c.IsSeleted = (c.Id == 1)`, recolors `btnCh1` green and `btnCh2` dark gray, and forces Channel 1 active regardless of which channel the operator had selected.
  - `Views/MainWindow.xaml.cs` line 3414 in `ManualLoadProfileForEquivalenceTestAsync`:
    ```csharp
    SelectCh1();
    ```
- **Whole-Machine Overwrite Instead of Target-Channel Replacement:**
  - `ViewModels/MenuItemViewModel.cs` lines 924–967:
    The mapping loop iterates over all 4 channels (`for (int chNum = 1; chNum <= 4; chNum++)`). If incoming data does not contain a channel, it resets that channel to fresh default data (`IniGdata()`). Thus, opening a single-channel profile into Channel 2 wiped Channel 1 back to empty defaults!
  - `DeviceCOM.ChannelBalanceRequired` line 941 & 964 was set to `true` for all channels rather than only the target channel.
  - `MainWindow.ImplementChanges(0)` line 992 was invoked with `targetChannelId = null`, transmitting all channels rather than just the target channel.
- **Untracked Open Channel in `CurrentProfileState`:**
  - `Models/CurrentProfileState.cs` lines 5–23 only stored `ProfileId`, `Name`, and `FilePath`. It did not track which channel the profile was opened into. When the user later clicked **Save**, the system could not determine which channel was bound to which profile.

---

### 2.2 Problem 2: Modbus Slave Hangs on Start & Operation

#### Exact Root Causes (File & Line Numbers):

1. **Deadlock Between Background Consumer and UI Thread via `Dispatcher.Invoke`:**
   - **File:** `Views/MainWindow.xaml.cs` lines 3295–3298 in `OpenProfilesBatchAsync`:
     ```csharp
     var implementTask = Task.Run(() =>
     {
         return Dispatcher.Invoke(() => ImplementChanges(0, targetCh));
     });
     ```
   - **File:** `Views/ModbusTestPanel.xaml.cs` lines 167–173:
     ```csharp
     _modbusService.WriteRegister(address, val);
     ...
     if (Owner is MainWindow mainWin)
     {
         await mainWin.OpenProfileForChannelAsync(channelIndex, val, isUnattended: false);
     }
     ```
   - **Mechanism:**
     When a write occurs from `ModbusTestPanel`:
     - Line 167 calls `_modbusService.WriteRegister(address, val)`, queuing an item to `_profileChannel`. The background task `ProcessProfileWritesAsync` in `ModbusSlaveService.cs` (line 767) picks it up and calls `OpenProfilesBatchAsync`, acquiring `_profileLoadSemaphore` (line 3156).
     - Meanwhile, line 172 on the UI thread calls `mainWin.OpenProfileForChannelAsync(...)` $\rightarrow$ `OpenProfilesBatchAsync`, which blocks waiting for `_profileLoadSemaphore`.
     - The background worker holding the semaphore reaches line 3297 and executes synchronous `Dispatcher.Invoke(...)` to call back to the UI thread.
     - **Deadlock:** The UI thread is blocked waiting for `_profileLoadSemaphore`; the background task holding the semaphore is blocked waiting for the UI thread via `Dispatcher.Invoke`. The whole application freezes permanently.

2. **Synchronous Network & System Work on the UI Thread During Start:**
   - **File:** `Services/Implementations/ModbusSlaveService.cs` line 270:
     `var lanIps = GetLocalIPv4Addresses();` calls `NetworkInterface.GetAllNetworkInterfaces()` synchronously on the UI thread during `Start()`. On machines with disconnected network adapters, VPNs, or slow WMI/DNS queries, this call blocks the calling thread for several seconds.
   - **File:** `Services/Implementations/ModbusSlaveService.cs` lines 266–267:
     `_tcpListener = new TcpListener(bindIp, Port); _tcpListener.Start();` executes synchronously. If the port is in use or blocked, it throws synchronously.
   - **File:** `Views/MainWindow.xaml.cs` line 283:
     `modbusSlaveService.Start(...)` is invoked synchronously inside the `MainWindow` constructor (`public MainWindow()`), freezing application launch if any binding latency occurs.

3. **Socket Leaks and Incomplete Shutdown in `Stop()`:**
   - **File:** `Services/Implementations/ModbusSlaveService.cs` lines 336–364:
     `_tcpListener?.Stop()` stops listening for *new* incoming connections, but does not close or abort *existing* connected client streams (`TcpClient`). Active client connection tasks remain alive in `HandleClientAsync`, holding socket descriptors open. Calling `Start()` again immediately afterwards results in `SocketError.AddressAlreadyInUse`.

4. **Missing Read Timeout on Client Stream:**
   - **File:** `Services/Implementations/ModbusSlaveService.cs` lines 511–521 (`ReadExactAsync`):
     `stream.ReadAsync` has no socket timeout (`ReceiveTimeout` is ignored on `NetworkStream.ReadAsync`). A client connecting and sending partial headers or going silent holds the connection open indefinitely.

5. **Lack of Idempotency & Thread Safety in State Transitions:**
   - `Start()` and `Stop()` have no atomic state guard (`Starting`, `Running`, `Faulted`, `Stopped`). Rapid double-clicking or calling Start while already running causes race conditions.

---

### 2.3 Problem 3: Disconnected Menu Open and Modbus

#### Exact Root Causes:
1. **Divergent Code Paths:**
   - Menu Open (File/DB) goes through `MenuItemViewModel.ApplyChannelDataWithMapping`.
   - Modbus register writes go through `MainWindow.OpenProfilesBatchAsync`.
   - The two implementations have completely different remapping rules, different UI update behaviors, and do not synchronize with each other.
2. **Missing Open $\rightarrow$ Modbus Synchronization:**
   - When a user opens a profile via the menu, holding registers 1..4 in `ModbusSlaveService` are never updated. A PLC polling Modbus continues reading stale profile IDs or 0.
3. **No Modbus $\rightarrow$ Menu Synchronization:**
   - When a PLC writes a profile ID to a register, the register is updated before the profile is verified or loaded. If the profile ID does not exist in the database, the register retains the invalid ID and the UI is left in an inconsistent state.
4. **Immediate Ack Requirement:**
   - A PLC master writing via FC 6 or FC 16 expects an immediate response. If the profile load is awaited synchronously before returning the Modbus response, PLC communication timeouts (typically 1000 ms) will trigger faults on large configurations.

---

## 3. Planned Architecture & Fix Strategy

### 3.1 Single Unified Profile Load Service (`IProfileLoadService`)
Introduce `_8F.Services.IProfileLoadService` and `ProfileLoadService`:
- Single entry point for all profile load requests:
  - Menu Open from Database
  - Menu Open from File
  - Menu Import
  - Modbus Diagnostic Panel "Write Reg"
  - Modbus Master PLC write (FC 6 / FC 16)
- Thread-safe serialization using `SemaphoreSlim(1, 1)`.
- Fully asynchronous execution; all UI mutations dispatched cleanly via `Dispatcher.InvokeAsync` without synchronous `Dispatcher.Invoke` deadlocks.

### 3.2 Target Channel Selection Rule
Centralized in `IProfileLoadService.SelectSourceChannel(availableChannels, targetChannelNumber)`:
1. If exactly 1 channel in profile: use it.
2. If multiple channels: use the channel matching `targetChannelNumber`.
3. If no match: use the lowest-numbered channel (`OrderBy(c => c.Id).First()`).
4. Interactive menu: retain `ChannelRemappingWindow` when multiple channels exist in source, pre-selecting `targetChannelNumber`.

### 3.3 Target-Only Application & State Preservation
- Replace only `DeviceCOM.channelDatas[targetChannel - 1]`.
- Keep other channels completely untouched (no reset, no graph clear, no balance flag change).
- Set `DeviceCOM.ChannelBalanceRequired[targetChannel] = true`.
- Transmit only `targetChannel` to instrument (`ImplementChanges(0, targetChannel)`).
- Update `CurrentProfileState` with `(profileId, name, filePath, targetChannel)`.
- Update Modbus holding register silently (`WriteRegisterSilent`) to prevent recursive load triggers.

### 3.4 Modbus Slave Hardening
- Asynchronous non-blocking `Start()` / `StartAsync()` and `Stop()` with states: `Stopped`, `Starting`, `Running`, `Faulted`.
- Track active `TcpClient` instances in `ConcurrentDictionary<TcpClient, byte>` and forcefully close all client streams on `Stop()`.
- Add 5-second cancellation timeout to `ReadExactAsync`.
- Add Start and Stop buttons with live state display to `ModbusTestPanel.xaml`.
- On Modbus write: echo FC 6 / FC 16 immediately to master; dispatch load via `IProfileLoadService` in background; if load fails or ID unknown, restore register to last good ID.
