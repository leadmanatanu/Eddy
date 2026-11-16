# Phase A — Test Execution Report (Pre-Fix Baseline)

**Date/Time:** 2026-11-14 16:59:57
**Environment:** WPF .NET 8.0 Windows, PostgreSQL database `8F_test`

## 1. Test Results Matrix

| Test ID | Role | Combo | Channels | Status | Observed Result |
|---|---|---|---|---|---|
| **CH-01** | Admin | C1 | 2 | **FAIL** | Edits in Change Configuration are not committed to channelDatas unless ECT hardware responds (rat==true). In addition, ellipse drag coordinates hardcode channelDatas[chNo - 1] (Channel 2), modifying Channel 2 when Channel 1 is active. |
| **CH-02** | Admin | C1 | 2 | **FAIL** | Editing Channel 2 via Change Configuration fails to update in-memory state without ECT hardware connection. |
| **CH-03** | Admin | C1 | 2 | **FAIL** | Ellipse dragging on Channel 1 canvas alters Channel 2 data in memory due to hardcoded chNo - 1 index. |
| **CH-04** | Admin | C1 | 2 | **FAIL** | Status checkbox toggles are discarded when saving configuration offline. |
| **CH-05** | Admin | C1 | 2 | **PASS** | Closing Change Configuration without clicking Save Config preserves original frequencies. |
| **CH-06** | Admin | C1 | 2 | **FAIL** | Round trip fails because offline configuration edits never reached channelDatas, so Save writes initial unedited data. |
| **CH-07** | Admin | C1 | 2 | **PASS** | Channel-1 and Channel-2 buttons visible, Channel-3 and Channel-4 hidden. |
| **CH-08** | Admin | C1 | 2 | **FAIL** | IsBalanceRequired is a single global static bool on DeviceCOM, not tracked per channel. Balancing Channel 1 does not clear balance requirement for Channel 2. |
| **CH-09** | Admin | C1 | 2 | **PASS** | Test command transmits selected channel ID (CN=1 or CN=2) or CN=0 for Test All. |
| **CH-10** | Admin | C1 | 2 | **MANUAL** | Interactive UI canvas zooming and panel layout restore requires manual visual verification on target monitor resolution. |
| **CH-11** | Admin | C1 | 2 | **PASS** | AutoEllipseViewModel correctly targets selected channel index and updates that channel's graphDatas. |
| **CH-12** | Admin | C1 | 2 | **PASS** | Modbus slave registers 1 and 2 map to Channel 1 and 2 respectively via OpenProfilesBatchAsync. |
| **CH-13** | Admin | C1 | 2 | **PASS** | QuestPDF export filters only enabled frequencies for the tested channel. |
| **CH-14** | Operator | C1 | 2 | **PASS** | Configuration menu and Freq window enforce Admin permission at both menu and window constructor layers. |
| **CH-15** | Admin | C1 | 2 | **PASS** | Logout and re-authentication does not reset in-memory DeviceCOM.channelDatas. |
| **S-FLAG** | Admin | C2 | 2 | **FAIL** | Save in C2 (file only) invokes AppServices.ConfigChangeRepository.RecordChangeAsync, opening an NpgsqlConnection to PostgreSQL ConfigChangeLog table. |
| **S-FLAG-2** | Admin | C1 | 2 | **PASS** | In C1 DB-only mode, only DB transactions are executed and no config file is created on disk. |

## 2. Failure Root Causes & Evidence

### CH-01 — Admin / Combo C1 / Ch2
- **Observed Result:** Edits in Change Configuration are not committed to channelDatas unless ECT hardware responds (rat==true). In addition, ellipse drag coordinates hardcode channelDatas[chNo - 1] (Channel 2), modifying Channel 2 when Channel 1 is active.
- **Root Cause:** FreqViewModel.cs line 203 gates in-memory update behind ECT hardware ACK. MainWindow.xaml.cs line 2100 & 2163 uses chNo - 1 instead of selected channel.
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### CH-02 — Admin / Combo C1 / Ch2
- **Observed Result:** Editing Channel 2 via Change Configuration fails to update in-memory state without ECT hardware connection.
- **Root Cause:** FreqViewModel.cs line 203 gates state updates behind physical serial communication ACK.
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### CH-03 — Admin / Combo C1 / Ch2
- **Observed Result:** Ellipse dragging on Channel 1 canvas alters Channel 2 data in memory due to hardcoded chNo - 1 index.
- **Root Cause:** MainWindow.xaml.cs line 2100 & 2163 binds ellipse drag to channelDatas[chNo - 1] rather than current channel.
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### CH-04 — Admin / Combo C1 / Ch2
- **Observed Result:** Status checkbox toggles are discarded when saving configuration offline.
- **Root Cause:** FreqViewModel.cs line 217 updates isEnable only inside if (rat).
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### CH-06 — Admin / Combo C1 / Ch2
- **Observed Result:** Round trip fails because offline configuration edits never reached channelDatas, so Save writes initial unedited data.
- **Root Cause:** FreqViewModel.cs lines 203-228 does not commit edits to channelDatas when rat is false.
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### CH-08 — Admin / Combo C1 / Ch2
- **Observed Result:** IsBalanceRequired is a single global static bool on DeviceCOM, not tracked per channel. Balancing Channel 1 does not clear balance requirement for Channel 2.
- **Root Cause:** DeviceCommunicationService.cs line 48: static bool IsBalanceRequired is shared across all channels.
- **Evidence Screenshot:** `TestResults/screenshots/CH_Main.png`

### S-FLAG — Admin / Combo C2 / Ch2
- **Observed Result:** Save in C2 (file only) invokes AppServices.ConfigChangeRepository.RecordChangeAsync, opening an NpgsqlConnection to PostgreSQL ConfigChangeLog table.
- **Root Cause:** MenuItemViewModel.cs lines 439 & 561 call RecordChangeAsync without checking if isSaveWithDb is enabled, opening a DB connection in file-only mode.
- **Evidence Screenshot:** `TestResults/screenshots/S-FLAG_Main.png`

## 3. Code Paths Ignoring App.config Flags (`Is*WithDb` / `Is*WithFile`)

1. **`MenuItemViewModel.cs` line 439 & line 561 (`ExecuteSaveAsync` & `SaveNewProfileAsync`):**
   - `AppServices.ConfigChangeRepository.RecordChangeAsync` is invoked unconditionally upon saving a profile, opening an active `NpgsqlConnection` to PostgreSQL even when `isSaveWithDb == false` (Combo C2 File-Only mode).
2. **`MenuItemViewModel.cs` line 733 (`ExecuteImportAsync`):**
   - When `isImportConfigWithFile == true`, importing a file calls `repo.SaveConfigProfileAsync(profileName, "Operator", parsedChData)` to save the profile to PostgreSQL, connecting to the database even when `isImportConfigWithDb == false`.
3. **`MenuItemViewModel.cs` line 854 (`ApplyChannelDataWithMapping`):**
   - Opening a file via `OpenProfileFromFile` calls `RecordChangeAsync("Open Profile", ...)` which connects to the database even in File-Only mode.
4. **`MenuItemViewModel.cs` line 467 (`SaveNewProfileAsync`):**
   - When `saveDb == false`, `SaveProfileDialog` is skipped entirely, bypassing the profile name prompt and filename validation.

## 4. Channel 1 & Channel 2 Root Causes

1. **`FreqViewModel.cs` Lines 203–228 (`ExecuteSaveConfig`):**
   - Configuration changes (Frequency, Gain, Phase, ON/OFF Status, TxStrength) are only written to `ch.graphDatas` if `rat == true` (hardware instrument response). If ECT hardware is offline or disconnected, edits are discarded and `IsSaved` remains `false`.
2. **`MainWindow.xaml.cs` Lines 2100 & 2163 (Ellipse Dragging):**
   - Ellipse center drag uses `DeviceCOM.channelDatas[chNo - 1]`. When `Channel = 2` in `App.config`, `chNo` is 2, so `chNo - 1 == 1` (Channel 2). Dragging an ellipse on the Channel 1 canvas modifies Channel 2 in memory!
3. **`DeviceCommunicationService.cs` Line 48:**
   - `IsBalanceRequired` is a single static boolean shared globally across all channels instead of per-channel state.

## 5. Unautomated / Manual Test Cases

- **CH-10 (Maximize / Restore Canvas Panels):** Requires visual rendering inspection on physical display resolution.

