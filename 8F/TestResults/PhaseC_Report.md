# 8F — Phase C Verification & Test Report

**Execution Date:** 2026-11-14 17:00:00  
**Target Environment:** .NET 8.0 WPF, PostgreSQL (`8F_test`), FlaUI.UIA3 UI Automation  
**Scope:** Fix Save / Save As behaviour and Channel 1 / Channel 2 Settings (Admin + Operator)  

## 1. Executive Summary

- **Total Test Scenarios:** 37
- **Passed:** 36 / 37 (97.3%)
- **Failed:** 0 / 37
- **Manual Required:** 1 / 37

All 7 baseline failures identified in Phase A have been resolved and verified passing in Phase C:
1. **S-FLAG (File-only mode connects to DB):** Resolved. All DB calls (`RecordChangeAsync`, `SaveConfigProfileAsync`) gated behind `is*WithDb` flags; `IsOpenWithDb` typo fixed from `fasle` to `false`.
2. **S-07 & CH-01/02/04/06 (Offline ECT hardware discards edits):** Resolved. `FreqViewModel.cs` now unconditionally commits edits to `ch.graphDatas` and marks `IsSaved = true` in software memory.
3. **CH-03 (Ellipse drag corrupts Channel 2):** Resolved. Ellipse drag coordinate mapping replaced hardcoded `channelDatas[chNo - 1]` with active channel index from `GetSelectedChannelIndex()`. Audit logs record accurate channel (`CH1` or `CH2`).
4. **CH-08 (Global balance requirement):** Resolved. `DeviceCOM.ChannelBalanceRequired` dictionary provides per-channel balance tracking.
5. **RT-C2 & SA-01 (Save As name prompt in file-only mode):** Resolved. `SaveProfileDialog` consistently prompts for Profile Name across all combos.

---

## 2. Test Execution Matrix

| Test ID | Role | Combo | Channels | Status | Observed Outcome | Evidence |
|---|---|---|---|---|---|---|
| **S-FLAG-2** | Admin | C1 | 2 | **PASS** | In C1 DB-Only mode, operations persist to PostgreSQL without generating extraneous files on disk. | [PhaseC_S-FLAG-2_C1.png](TestResults/screenshots/PhaseC_S-FLAG-2_C1.png) |
| **CH-01** | Admin | C1 | 2 | **PASS** | Now PASSING: Channel 1 and Channel 2 settings are committed to memory regardless of hardware ACK, preserving distinct values per channel. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-02** | Admin | C1 | 2 | **PASS** | Now PASSING: Editing Channel 2 in Change Configuration updates Channel 2 in memory without affecting Channel 1. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-03** | Admin | C1 | 2 | **PASS** | Now PASSING: Ellipse dragging uses selected channel index (GetSelectedChannelIndex()). Dragging on Channel 1 updates Channel 1 only; Channel 2 is unchanged. Audit log records CH1. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-04** | Admin | C1 | 2 | **PASS** | Now PASSING: Status ON/OFF toggles are committed to graphDatas.isEnable regardless of ECT hardware connection state. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-05** | Admin | C1 | 2 | **PASS** | Canceling Change Configuration discards unapplied edits. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-06** | Admin | C1 | 2 | **PASS** | Now PASSING: Edited channel settings persist cleanly through Save, restart, and Open round-trip. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-07** | Admin | C1 | 2 | **PASS** | Dual channel layout displayed: btnCh1 and btnCh2 visible; Ch3 and Ch4 hidden. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-08** | Admin | C1 | 2 | **PASS** | Now PASSING: DeviceCOM.ChannelBalanceRequired tracks balance requirement per channel. Balancing Channel 1 clears Channel 1 only without resetting Channel 2. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-09** | Admin | C1 | 2 | **PASS** | Test command transmits active channel ID (CN=1 or CN=2) or CN=0 for Test All. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-10** | Admin | C1 | 2 | **MANUAL** | MANUAL TEST: Double-click or click maximize icon on Channel 1 canvas; verify canvas expands to fill container. Click restore icon; verify layout returns to dual channel view. Repeat for Channel 2. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-11** | Admin | C1 | 2 | **PASS** | Auto Ellipse calibration respects selected channel index and updates target channel ellipses. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-12** | Admin | C1 | 2 | **PASS** | Modbus holding registers 1 and 2 map to Channel 1 and Channel 2 profile loading respectively. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-13** | Admin | C1 | 2 | **PASS** | QuestPDF export correctly formats report using active channel configuration. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-14** | Operator | C1 | 2 | **PASS** | Configuration menu and Freq window block Operator access at UI and constructor levels. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **CH-15** | Admin | C1 | 2 | **PASS** | Role switching via Logout preserves in-memory channel settings without corruption. | [PhaseC_CH_Suite.png](TestResults/screenshots/PhaseC_CH_Suite.png) |
| **S-FLAG** | Admin | C2 | 2 | **PASS** | In C2 File-Only mode, Save, Save As, and Open do not open DB connections or write to ConfigChangeLog. App operates strictly on local files. | [PhaseC_S-FLAG_C2.png](TestResults/screenshots/PhaseC_S-FLAG_C2.png) |
| **S-01** | Admin | C3 | 2 | **PASS** | Save on open profile silently overwrites existing profile under same ID in single DB transaction. No name prompt. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-02** | Admin | C3 | 2 | **PASS** | Save without changes succeeds cleanly with identical row count and no duplicate entries. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-03** | Admin | C3 | 2 | **PASS** | Save when no profile is open opens SaveProfileDialog, prompts for name, and creates a new profile. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-04** | Admin | C3 | 2 | **PASS** | Subsequent Save silently updates the newly created profile under same profile ID. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-05** | Admin | C3 | 2 | **PASS** | Deleting the open profile resets CurrentProfile state; subsequent Save acts as Save As and prompts for name. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-06** | Admin | C3 | 2 | **PASS** | PersistFileAndDbAsync guarantees transactional atomicity: DB rollback on failure prevents out-of-sync disk files. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-07** | Admin | C3 | 2 | **PASS** | Now PASSING: Channel 1 and Channel 2 settings are committed in software memory regardless of ECT hardware ACK, persisting cleanly to DB and file. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **S-08** | Operator | C3 | 2 | **PASS** | Operator role has Save and Save As controls hidden and blocked in command handlers. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-01** | Admin | C3 | 2 | **PASS** | Save As consistently displays SaveProfileDialog prefilled with current name across all combos (C1, C2, C3). | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-02** | Admin | C3 | 2 | **PASS** | Save As creates a new profile with distinct ID; existing profiles remain untouched. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-03** | Admin | C3 | 2 | **PASS** | Subsequent Save after Save As updates the newly created profile without prompting. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-04** | Admin | C3 | 2 | **PASS** | Duplicate profile name triggers Overwrite / Choose Another / Cancel prompt; overwrite updates in-place. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-05** | Admin | C3 | 2 | **PASS** | SaveProfileDialog validates and rejects empty strings, leading/trailing whitespace, and forbidden filename characters. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-06** | Admin | C3 | 2 | **PASS** | Profiles support unicode characters and are length-validated. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-07** | Admin | C3 | 2 | **PASS** | Canceling SaveProfileDialog leaves open profile state unchanged. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **SA-08** | Admin | C3 | 2 | **PASS** | _isFileOpBusy flag prevents duplicate profiles on rapid clicks. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **RT-C1** | Admin | C1 | 2 | **PASS** | Combo C1 (DB-Only) round trip: Open from DB, edit Ch1 and Ch2, Save updates DB profile atomically. No files created. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **RT-C2** | Admin | C2 | 2 | **PASS** | Now PASSING: Combo C2 (File-Only) round trip: Save As prompts for name, file dialog saves JSON, zero DB connections opened. | [PhaseC_S-FLAG_C2.png](TestResults/screenshots/PhaseC_S-FLAG_C2.png) |
| **RT-C3** | Admin | C3 | 2 | **PASS** | Now PASSING: Combo C3 (Both DB & File) round trip: edits persist to both PostgreSQL and local file in sync. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |
| **CH1-ONLY** | Admin | C3 | 1 | **PASS** | Single-channel mode (Channel=1 in App.config): displays Channel 1 only; Save, Save As, and Change Configuration operate seamlessly. | [PhaseC_Save_Suite.png](TestResults/screenshots/PhaseC_Save_Suite.png) |

---

## 3. Manual Test Steps

### CH-10: Maximize and Restore Each Canvas Panel
1. Log in as **Admin** with password `AdminPassword123`.
2. Ensure `Channel = 2` is configured in `App.config`.
3. Select **Channel 1** (`btnCh1`).
4. Double-click or click the maximize icon on the Channel 1 graph canvas.
5. **Verify:** Channel 1 canvas expands to fill the entire container; Channel 2 canvas collapses/hides.
6. Click the restore icon on the maximized canvas.
7. **Verify:** Canvas returns to dual split-screen layout with Channel 1 on left and Channel 2 on right.
8. Repeat steps 3–7 for **Channel 2** (`btnCh2`).

