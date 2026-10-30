-- ============================================================================
-- Seed Mock Data for Scenarios A, B, C, D, and E
-- Project: 8F Multi-Frequency Eddy Current Inspection System
-- Target Table: public."Logs"
-- ============================================================================

-- Clean up any prior test runs for these specific scenario batch names
DELETE FROM public."Logs" WHERE "BatchName" IN (
    'BATCH-SCENARIO-A',
    'BATCH-SCENARIO-B',
    'BATCH-SCENARIO-C',
    'BATCH-SCENARIO-D',
    'BATCH-SCENARIO-E'
);

-- ----------------------------------------------------------------------------
-- SCENARIO A: All Channels ON (D1 to D8 Enabled)
-- ----------------------------------------------------------------------------
-- Record 1: PASS, all 8 channels enabled, all R=1
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":true,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":true,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-A","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-A', 'A-001', 1, '2026-10-15 09:00:00',
    '[
        {"FN":1,"R":1,"X":14,"Y":22},
        {"FN":2,"R":1,"X":-9,"Y":16},
        {"FN":3,"R":1,"X":5,"Y":-12},
        {"FN":4,"R":1,"X":18,"Y":25},
        {"FN":5,"R":1,"X":-11,"Y":19},
        {"FN":6,"R":1,"X":8,"Y":-7},
        {"FN":7,"R":1,"X":15,"Y":28},
        {"FN":8,"R":1,"X":-4,"Y":10}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":14,"Y":22},
        {"FN":2,"R":1,"X":-9,"Y":16},
        {"FN":3,"R":1,"X":5,"Y":-12},
        {"FN":4,"R":1,"X":18,"Y":25},
        {"FN":5,"R":1,"X":-11,"Y":19},
        {"FN":6,"R":1,"X":8,"Y":-7},
        {"FN":7,"R":1,"X":15,"Y":28},
        {"FN":8,"R":1,"X":-4,"Y":10}
    ]'::jsonb
);

-- Record 2: FAIL, all 8 channels enabled, FN 3 and FN 7 failed (R=0)
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    false,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":true,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":true,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-A","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-A', 'A-002', 1, '2026-10-15 09:02:00',
    '[
        {"FN":1,"R":1,"X":12,"Y":18},
        {"FN":2,"R":1,"X":-7,"Y":14},
        {"FN":3,"R":0,"X":38,"Y":-42},
        {"FN":4,"R":1,"X":16,"Y":21},
        {"FN":5,"R":1,"X":-10,"Y":17},
        {"FN":6,"R":1,"X":7,"Y":-5},
        {"FN":7,"R":0,"X":45,"Y":32},
        {"FN":8,"R":1,"X":-3,"Y":9}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":12,"Y":18},
        {"FN":2,"R":1,"X":-7,"Y":14},
        {"FN":3,"R":0,"X":38,"Y":-42},
        {"FN":4,"R":1,"X":16,"Y":21},
        {"FN":5,"R":1,"X":-10,"Y":17},
        {"FN":6,"R":1,"X":7,"Y":-5},
        {"FN":7,"R":0,"X":45,"Y":32},
        {"FN":8,"R":1,"X":-3,"Y":9}
    ]'::jsonb
);

-- ----------------------------------------------------------------------------
-- SCENARIO B: Mixed ON/OFF (D1, D3, D5, D7 ON; D2, D4, D6, D8 OFF)
-- ----------------------------------------------------------------------------
-- Record 1: PASS, enabled channels all R=1
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":false,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":false,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":false,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":false,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-B","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-B', 'B-001', 1, '2026-10-15 09:10:00',
    '[
        {"FN":1,"R":1,"X":11,"Y":19},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":6,"Y":-10},
        {"FN":4,"R":1,"X":-99,"Y":99},
        {"FN":5,"R":1,"X":-8,"Y":15},
        {"FN":6,"R":1,"X":-99,"Y":99},
        {"FN":7,"R":1,"X":14,"Y":26},
        {"FN":8,"R":1,"X":-99,"Y":99}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":11,"Y":19},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":6,"Y":-10},
        {"FN":4,"R":1,"X":-99,"Y":99},
        {"FN":5,"R":1,"X":-8,"Y":15},
        {"FN":6,"R":1,"X":-99,"Y":99},
        {"FN":7,"R":1,"X":14,"Y":26},
        {"FN":8,"R":1,"X":-99,"Y":99}
    ]'::jsonb
);

-- Record 2: FAIL, enabled channel D5 failed (R=0); disabled channel D4 also has raw R=0 (must remain N/A, not highlighted)
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    false,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":false,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":false,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":false,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":false,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-B","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-B', 'B-002', 1, '2026-10-15 09:12:00',
    '[
        {"FN":1,"R":1,"X":10,"Y":17},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":7,"Y":-11},
        {"FN":4,"R":0,"X":-99,"Y":99},
        {"FN":5,"R":0,"X":52,"Y":24},
        {"FN":6,"R":1,"X":-99,"Y":99},
        {"FN":7,"R":1,"X":13,"Y":27},
        {"FN":8,"R":1,"X":-99,"Y":99}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":10,"Y":17},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":7,"Y":-11},
        {"FN":4,"R":0,"X":-99,"Y":99},
        {"FN":5,"R":0,"X":52,"Y":24},
        {"FN":6,"R":1,"X":-99,"Y":99},
        {"FN":7,"R":1,"X":13,"Y":27},
        {"FN":8,"R":1,"X":-99,"Y":99}
    ]'::jsonb
);

-- ----------------------------------------------------------------------------
-- SCENARIO C: All Channels OFF (D1 to D8 Disabled)
-- ----------------------------------------------------------------------------
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":false,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":false,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":false,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":false,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":false,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":false,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":false,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":false,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-C","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-C', 'C-001', 1, '2026-10-15 09:20:00',
    '[
        {"FN":1,"R":1,"X":10,"Y":10},
        {"FN":2,"R":1,"X":20,"Y":20},
        {"FN":3,"R":1,"X":30,"Y":30},
        {"FN":4,"R":1,"X":40,"Y":40},
        {"FN":5,"R":1,"X":50,"Y":50},
        {"FN":6,"R":1,"X":60,"Y":60},
        {"FN":7,"R":1,"X":70,"Y":70},
        {"FN":8,"R":1,"X":80,"Y":80}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":10,"Y":10},
        {"FN":2,"R":1,"X":20,"Y":20},
        {"FN":3,"R":1,"X":30,"Y":30},
        {"FN":4,"R":1,"X":40,"Y":40},
        {"FN":5,"R":1,"X":50,"Y":50},
        {"FN":6,"R":1,"X":60,"Y":60},
        {"FN":7,"R":1,"X":70,"Y":70},
        {"FN":8,"R":1,"X":80,"Y":80}
    ]'::jsonb
);

-- ----------------------------------------------------------------------------
-- SCENARIO D: Configuration Change Over Time (Historical Snapshot Preservation)
-- ----------------------------------------------------------------------------
-- Event 1 (10:00): Configuration 1 where ALL channels are ON
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":true,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":true,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-D","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-D', 'D-001', 1, '2026-10-15 10:00:00',
    '[
        {"FN":1,"R":1,"X":14,"Y":22},
        {"FN":2,"R":1,"X":-9,"Y":16},
        {"FN":3,"R":1,"X":5,"Y":-12},
        {"FN":4,"R":1,"X":18,"Y":25},
        {"FN":5,"R":1,"X":-11,"Y":19},
        {"FN":6,"R":1,"X":8,"Y":-7},
        {"FN":7,"R":1,"X":15,"Y":28},
        {"FN":8,"R":1,"X":-4,"Y":10}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":14,"Y":22},
        {"FN":2,"R":1,"X":-9,"Y":16},
        {"FN":3,"R":1,"X":5,"Y":-12},
        {"FN":4,"R":1,"X":18,"Y":25},
        {"FN":5,"R":1,"X":-11,"Y":19},
        {"FN":6,"R":1,"X":8,"Y":-7},
        {"FN":7,"R":1,"X":15,"Y":28},
        {"FN":8,"R":1,"X":-4,"Y":10}
    ]'::jsonb
);

-- Event 2 (11:30): Configuration 2 where operator disabled D2 and D4
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":false,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":false,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-D","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-D', 'D-002', 1, '2026-10-15 11:30:00',
    '[
        {"FN":1,"R":1,"X":16,"Y":23},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":6,"Y":-11},
        {"FN":4,"R":1,"X":-99,"Y":99},
        {"FN":5,"R":1,"X":-10,"Y":20},
        {"FN":6,"R":1,"X":9,"Y":-6},
        {"FN":7,"R":1,"X":16,"Y":29},
        {"FN":8,"R":1,"X":-3,"Y":11}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":16,"Y":23},
        {"FN":2,"R":1,"X":-99,"Y":99},
        {"FN":3,"R":1,"X":6,"Y":-11},
        {"FN":4,"R":1,"X":-99,"Y":99},
        {"FN":5,"R":1,"X":-10,"Y":20},
        {"FN":6,"R":1,"X":9,"Y":-6},
        {"FN":7,"R":1,"X":16,"Y":29},
        {"FN":8,"R":1,"X":-3,"Y":11}
    ]'::jsonb
);

-- ----------------------------------------------------------------------------
-- SCENARIO E: Missing or Invalid Frequency Data
-- ----------------------------------------------------------------------------
-- Record 1: Only partial frequencies supplied (FN 1, 2, 5 present; 3, 4, 6, 7, 8 missing)
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    false,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":true,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":true,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-E","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-E', 'E-001', 1, '2026-10-15 12:00:00',
    '[
        {"FN":1,"R":1,"X":10,"Y":15},
        {"FN":2,"R":0,"X":35,"Y":-20},
        {"FN":5,"R":1,"X":-8,"Y":12}
    ]'::jsonb,
    '[
        {"FN":1,"R":1,"X":10,"Y":15},
        {"FN":2,"R":0,"X":35,"Y":-20},
        {"FN":5,"R":1,"X":-8,"Y":12}
    ]'::jsonb
);

-- Record 2: Empty frequency payload []
INSERT INTO public."Logs" (
    "ChId", "Result", "FDData", "PartData", "PartName", "BatchName", "SrNo", "BatchNo", "TimeStamp", "FrequencyData", "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":100,"gain":12,"phase":45,"isEnable":true,"height":20.0,"width":15.0,"ex":25.0,"ey":25.0,"angel":0.0},
        {"Id":2,"Name":"D2","freq":200,"gain":14,"phase":60,"isEnable":true,"height":22.0,"width":16.0,"ex":28.0,"ey":28.0,"angel":15.0},
        {"Id":3,"Name":"D3","freq":300,"gain":16,"phase":90,"isEnable":true,"height":25.0,"width":18.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":18,"phase":120,"isEnable":true,"height":28.0,"width":20.0,"ex":32.0,"ey":32.0,"angel":45.0},
        {"Id":5,"Name":"D5","freq":500,"gain":20,"phase":135,"isEnable":true,"height":30.0,"width":22.0,"ex":35.0,"ey":35.0,"angel":60.0},
        {"Id":6,"Name":"D6","freq":600,"gain":22,"phase":150,"isEnable":true,"height":32.0,"width":24.0,"ex":38.0,"ey":38.0,"angel":75.0},
        {"Id":7,"Name":"D7","freq":700,"gain":24,"phase":180,"isEnable":true,"height":35.0,"width":25.0,"ex":40.0,"ey":40.0,"angel":90.0},
        {"Id":8,"Name":"D8","freq":800,"gain":26,"phase":210,"isEnable":true,"height":38.0,"width":28.0,"ex":42.0,"ey":42.0,"angel":105.0}
    ]'::json,
    '{"Name":"Shaft-ECT-40","Grade":"EN8D","BatchName":"BATCH-SCENARIO-E","CheckedBy":"Rajesh K","CompanyName":"AutoForge India Ltd"}'::json,
    'Shaft-ECT-40', 'BATCH-SCENARIO-E', 'E-002', 1, '2026-10-15 12:05:00',
    '[]'::jsonb,
    '[]'::jsonb
);
