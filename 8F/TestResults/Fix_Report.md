# 8F — Fix Report: Open into Selected Channel, Modbus Slave Stability & Unified Profile Load Service

## 1. Executive Summary
This report documents the resolution of the three major operational issues in the 8F WPF / .NET 8.0 solution:
1. **Open must load into whichever channel is selected:** Removed the forced `SelectCh1()` on Open. When Channel 2 is selected, Open (DB, file, Import) loads into Channel 2 and stays on Channel 2; when Channel 1 is selected, it loads into Channel 1 and stays on Channel 1. Only the target channel's data is replaced; the other channel is completely untouched.
2. **Modbus TCP Slave hang on start:** Eliminated UI-thread deadlocks, thread-pool starvation, and blocking synchronous calls. `Start()` and `StartAsync()` are non-blocking, idempotent, background tasks with full cancellation support and clean socket lifecycle management (zero socket leaks on stop, read timeout on inactive clients).
3. **Connect Menu Open and Modbus (unified load pipeline):** Built `IProfileLoadService` as the single shared engine for all profile loads (Menu Open DB, Menu Open file, Menu Import, Modbus Test Panel "Write Reg", and PLC Master writes). Serialized via `SemaphoreSlim(1, 1)`, bidirectional synchronization keeps Modbus holding registers in sync without circular feedback loops.

All 23 automated tests (`OP-01`–`OP-08`, `MB-01`–`MB-07`, `LK-01`–`LK-08`) and all 12 prior channel tests (`CS-01`–`CS-12`) pass 100%. The solution builds with **0 warnings and 0 errors**.

---

## 2. Architecture & Data Flow

```
                      +-----------------------------+
                      |   Modbus TCP Master (PLC)   |
                      +--------------+--------------+
                                     |
                               FC 06 / FC 16
                                     v
                      +-----------------------------+
                      |     ModbusSlaveService      |
                      |  - Immediate Modbus echo    |
                      |  - Background dispatch      |
                      +--------------+--------------+
                                     |
+-------------------+                |                 +--------------------+
|  Menu Open DB /   |                |                 |  ModbusTestPanel   |
|  File / Import    |                |                 |  "Write Reg" UI    |
+---------+---------+                |                 +---------+----------+
          |                          |                           |
          +--------------------+     |     +---------------------+
                               |     |     |
                               v     v     v
                  +-----------------------------------+
                  |        IProfileLoadService        |
                  |     (Thread-safe, Serialized)     |
                  +-----------------+-----------------+
                                    |
            +-----------------------+-----------------------+
            |                                               |
            v                                               v
+-------------------------+                     +-------------------------+
|    Target Channel Only  |                     |  CurrentProfileState    |
| - Target Data Replaced  |                     | - Per-channel ProfileId |
| - Untouched Ch Intact   |                     | - Per-channel FilePath  |
| - Balance Flagged Only  |                     +------------+------------+
| - Target Instrument Push|                                  |
|   ImplementChanges(0,ch)|                     +------------v------------+
+-------------------------+                     | Modbus Holding Register |
                                                |  WriteRegisterSilent()  |
                                                +-------------------------+
```

---

## 3. Detailed Root Causes & Fixes

### 3.1 Open into Selected Channel
- **Root Cause:**
  - `MainWindow.SelectCh1()` was explicitly invoked at the end of `MenuItemViewModel.ApplyChannelDataWithMapping()`, resetting user selection back to Channel 1 regardless of which channel the user was actively inspecting.
  - Previous open logic wiped all non-matching channel slots with `IniGdata()`, destroying the unselected channel's configuration.
- **Fix:**
  - Removed `SelectCh1()` from the load pipeline. Selection is determined dynamically via `MainWindow.GetSelectedChannelNumber()`.
  - Implemented channel resolution rules in `ProfileLoadService.SelectSourceChannel()`:
    1. Single saved channel in profile $\rightarrow$ use it.
    2. Multiple saved channels $\rightarrow$ match target channel number if present.
    3. No exact match $\rightarrow$ fallback to lowest-numbered channel in profile.
    4. Interactive menu open allows manual remapping via `ChannelRemappingWindow` (pre-populated with default target channel).
  - Only the target channel `DeviceCOM.channelDatas[targetIndex]` is overwritten. The other channel's memory, UI graphs, balance status, and calibration remain 100% untouched.
  - Hardware push invokes `mainWin.ImplementChanges(0, targetChannel)` targeting only the loaded channel.
  - Balance requirement flagged strictly on `mainWin.isBalance[targetIndex] = false`.

### 3.2 Modbus TCP Slave Hang & Socket Leaks
- **Root Cause:**
  - Legacy code used synchronous `Dispatcher.Invoke` and `.Wait()` inside network loops, creating deadlocks when network frames arrived while the UI dispatcher was busy.
  - `TcpListener.Stop()` left child sockets unclosed in `CLOSE_WAIT` state, leading to socket exhaustion (`WSAEADDRINUSE`) on restart.
  - `Start()` was blocking and not re-entrant.
  - Absence of read timeouts allowed hung or idle clients to tie up server workers indefinitely.
- **Fix:**
  - `ModbusSlaveService.Start()` delegates to `StartAsync()`, protected by `SemaphoreSlim(1, 1)` for non-blocking idempotence.
  - Client tracking via `ConcurrentDictionary<TcpClient, byte> _activeClientsMap`. `StopAsync()` iterates through all connected clients, disposes their streams, and closes the sockets before calling `TcpListener.Stop()`.
  - Sockets configured with `NoDelay = true` and `LingerState = new LingerOption(false, 0)`.
  - `ReadExactAsync()` enforces a 5-second `CancellationTokenSource` timeout. Inactive or stalling clients are disconnected automatically without impacting server uptime or other connections.
  - Incoming FC 06 / FC 16 writes reply immediately to the Modbus master before dispatching the profile load in the background, preventing Modbus master timeout retries.

### 3.3 Unified Profile Load Pipeline & Open $\leftrightarrow$ Modbus Link
- **Root Cause:**
  - Menu Open and Modbus write handling were isolated with zero shared state.
  - Modbus writes tried to manipulate the UI selection directly, interfering with operator workflow.
  - No rollback or verification existed if an unknown profile ID was received over Modbus.
- **Fix:**
  - Created `IProfileLoadService` and `ProfileLoadService` as the single shared entry point for all profile loading operations.
  - Menu Open / Import calls `service.LoadFromDbAsync()` or `service.LoadFromChannelsAsync()`, which silently updates the Modbus holding register via `WriteRegisterSilent()`, preventing echo/feedback loops.
  - Modbus Master write triggers `service.LoadFromDbAsync(profileId, targetChannel, ProfileLoadSource.ModbusMaster)`.
  - Background Modbus load executes without altering the UI channel selection button.
  - If the profile ID is invalid or not found in the database, the holding register is safely reverted to the last known valid profile ID (`CurrentProfile.GetForChannel(targetChannel).ProfileId` or `0`).
  - Writing Profile ID `0` is treated as a no-op; no profile is loaded and the current configuration remains intact.

---

## 4. Key Code Modifications

| File | Changes Made |
|---|---|
| `Services/Interfaces/IProfileLoadService.cs` | Defined `IProfileLoadService`, `ProfileLoadSource`, and `ProfileLoadResult`. |
| `Services/Implementations/ProfileLoadService.cs` | Core implementation: target channel isolation, selection preservation, safe system settings application, instrument push, target balance flag, silent Modbus register updates, error fallback. |
| `Models/CurrentProfileState.cs` | Added per-channel tracking: `GetForChannel(ch)`, `SetForChannel(ch, id, name, path)`, `SwitchActiveChannel(ch)`. |
| `Services/Implementations/ModbusSlaveService.cs` | Added `_explicitChannelCount` field, non-blocking `StartAsync`/`StopAsync`, client tracking map, 5s socket read timeout, immediate FC 06/16 response + background `IProfileLoadService` dispatch. |
| `ViewModels/MenuItemViewModel.cs` | Routed `Open` (DB/file) and `Import` through `IProfileLoadService`. Removed `SelectCh1()` and deprecated `ApplyChannelDataWithMapping()`. |
| `Views/MainWindow.xaml.cs` | Added `GetSelectedChannelNumber()`, `ApplySafeSystemSettings()`, and safe defaults for missing configuration settings (`BaudRate`, `PortName`). Made `modeApp` and `UpdateConfigProfileDisplay()` public. |
| `Views/ChannelRemappingWindow.xaml.cs` | Added `defaultTargetChannel` support for interactive open remapping. |
| `Views/ModbusTestPanel.xaml` & `.cs` | Added Start/Stop Server controls and routed "Write Reg" through `IProfileLoadService`. |
| `Auth/AppServices.cs` | Registered singleton `IProfileLoadService`. |

---

## 5. Automated Verification Results

### 5.1 Open into Selected Channel Tests (`OP-01` through `OP-08`)

| Test ID | Method / Purpose | Result | Details |
|---|---|:---:|---|
| **OP-01** | `OP_01_OpenDb_IntoChannel1_WhenChannel1Selected_LeavesChannel2Untouched` | **PASS** | Ch1 selected $\rightarrow$ Ch1 updated to profile A data; Ch2 data, balance, and graphs completely untouched. |
| **OP-02** | `OP_02_OpenDb_IntoChannel2_WhenChannel2Selected_LeavesChannel1Untouched` | **PASS** | Ch2 selected $\rightarrow$ Ch2 updated to profile B data; Ch1 data, balance, and graphs completely untouched. |
| **OP-03** | `OP_03_OpenFile_IntoChannel1_WhenChannel1Selected_StaysOnChannel1` | **PASS** | File open with Ch1 selected remains on Ch1 before and after load. |
| **OP-04** | `OP_04_OpenFile_IntoChannel2_WhenChannel2Selected_StaysOnChannel2` | **PASS** | File open with Ch2 selected remains on Ch2 before and after load. |
| **OP-05** | `OP_05_HelperRule1_SingleSavedChannel_ReturnsThatChannel` | **PASS** | Rule 1: Single saved channel in profile maps to target channel regardless of original ID. |
| **OP-06** | `OP_06_HelperRule2_MatchingChannelId_ReturnsMatchingChannel` | **PASS** | Rule 2: Multi-channel profile selects matching channel number. |
| **OP-07** | `OP_07_HelperRule3_NoMatch_ReturnsLowestNumberedChannel` | **PASS** | Rule 3: No exact match selects lowest numbered saved channel. |
| **OP-08** | `OP_08_SystemSettings_PreservesRunningChannelCountAndAppMode` | **PASS** | System settings load warns on mismatch and preserves running `ChannelCount` and `AppMode`. |

### 5.2 Modbus TCP Slave Tests (`MB-01` through `MB-07`)

| Test ID | Method / Purpose | Result | Details |
|---|---|:---:|---|
| **MB-01** | `MB_01_Start_IsNonBlockingAndStartsQuickly` | **PASS** | `StartAsync()` returns in 14ms; does not block UI thread. |
| **MB-02** | `MB_02_Stop_CleanlyStopsListenerAndDisconnectsActiveClients` | **PASS** | `StopAsync()` cleanly terminates listener and closes all active client sockets. |
| **MB-03** | `MB_03_StartStopStart_SequenceSucceedsWithoutAddressAlreadyInUse` | **PASS** | Fast restart cycle succeeds on same port; no `WSAEADDRINUSE` errors. |
| **MB-04** | `MB_04_FunctionCodes_FC03_FC06_FC16_Succeed_UnsupportedReturnsException01` | **PASS** | FC03, FC06, FC16 succeed; unsupported FC05 returns Modbus Exception Code 01. |
| **MB-05** | `MB_05_MalformedPackets_HandledWithoutCrashingServer` | **PASS** | Garbage packet handled gracefully; server remains alive and responsive. |
| **MB-06** | `MB_06_InactiveClient_DisconnectsAfterReadTimeout` | **PASS** | Partial-frame stall triggers 5s read timeout; client is cleanly disconnected. |
| **MB-07** | `MB_07_ConcurrentClients_HandledThreadSafely` | **PASS** | 5 concurrent TCP clients querying registers simultaneously with zero exceptions. |

### 5.3 Open $\leftrightarrow$ Modbus Link Tests (`LK-01` through `LK-08`)

| Test ID | Method / Purpose | Result | Details |
|---|---|:---:|---|
| **LK-01** | `LK_01_MenuOpenDb_UpdatesTargetChannelModbusRegisterSilently` | **PASS** | Menu DB load into Ch1 updates holding register 1; register 2 untouched. |
| **LK-02** | `LK_02_MenuImport_UpdatesTargetChannelModbusRegisterSilently` | **PASS** | Menu Import into Ch2 updates holding register 2 silently without re-trigger. |
| **LK-03** | `LK_03_MasterWrite_FC06_Address1_LoadsChannel1_WithoutChangingUiSelection` | **PASS** | Master writes Reg 1 $\rightarrow$ loads Ch1 while UI selection remains on Ch2. |
| **LK-04** | `LK_04_MasterWrite_FC06_Address2_LoadsChannel2_WithoutChangingUiSelection` | **PASS** | Master writes Reg 2 $\rightarrow$ loads Ch2 while UI selection remains on Ch1. |
| **LK-05** | `LK_05_MasterWrite_ProfileId0_TriggersNoLoad` | **PASS** | Master writes ID 0 $\rightarrow$ ignored as no-op; channel configuration unchanged. |
| **LK-06** | `LK_06_MasterWrite_UnknownProfileId_RestoresRegisterToLastGoodId` | **PASS** | Non-existent profile ID logged and reverted to previous valid profile ID. |
| **LK-07** | `LK_07_ModbusTestPanel_WriteReg_LoadsCleanlyWithoutDeadlock` | **PASS** | ModbusTestPanel UI "Write Reg" button executes cleanly without dispatcher deadlock. |
| **LK-08** | `LK_08_NewProfile_ResetsHoldingRegistersToZero` | **PASS** | Resetting/clearing profile sets holding registers to 0. |

**Test Suite Summary: 23 passed, 0 failed (100% pass rate).**

---

## 6. Manual Checklist for Real Machine & Real PLC

Execute the following steps when commissioning with the physical ECT instrument and PLC master:

1. **Verify Modbus Server Startup:**
   - Launch `8F.exe`. Ensure `IsModbusServerEnable = true` in `App.config` (or start via `Settings -> Modbus Test Panel`).
   - Confirm server starts immediately without freezing the splash screen or main window.
   - Verify heartbeat incrementing in Modbus Test Panel.

2. **Test Channel 1 vs Channel 2 Menu Open:**
   - Click the **Channel 2** button on the main toolbar (Channel 2 is highlighted).
   - Go to **Configuration -> Open from Database**.
   - Select a profile and click **Select Profile**.
   - **Verify:** The application **stays on Channel 2** (Channel 2 button remains highlighted).
   - **Verify:** Channel 2 frequencies, ellipses, and gain update to the profile values.
   - Click the **Channel 1** button: verify Channel 1 settings are completely unchanged.
   - Switch back to **Channel 1** and open another profile: verify Channel 1 updates and app stays on Channel 1.

3. **Test PLC Master Writes (External Trigger):**
   - Keep **Channel 2** selected on screen.
   - From the PLC master (or Modbus simulator), write valid Profile ID `N` to Holding Register `1` (Address `0x0001` for Channel 1).
   - **Verify:** PLC receives immediate write confirmation.
   - **Verify:** UI selection remains on Channel 2 without jumping.
   - Switch to Channel 1: verify Channel 1 now reflects Profile `N`.
   - From the PLC, write an invalid/non-existent Profile ID `9999` to Holding Register `1`.
   - **Verify:** Holding Register `1` reverts to the last valid ID (`N`), and an error is recorded in the change log.

4. **Verify Balance and Hardware Push:**
   - After a profile loads into Channel 1 via menu or Modbus, confirm the Channel 1 balance indicator indicates balance required.
   - Verify Channel 2 balance status was not invalidated.
