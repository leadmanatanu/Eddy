-- ============================================================================
-- SQL Script: Insert Log Records where Channel D1 is ON and D2 to D8 are OFF
-- Target Table: public."Logs"
-- Database: EddyShorter
-- ============================================================================

-- Optional: Clean up prior records for this specific batch before re-running
DELETE FROM public."Logs" WHERE "BatchName" = 'BATCH-D1-ONLY';

-- ----------------------------------------------------------------------------
-- Record 1: PASS (D1 ON, Coordinates: (15, 24), Result: PASS)
-- ----------------------------------------------------------------------------
INSERT INTO public."Logs" (
    "ChId",
    "Result",
    "FDData",
    "PartData",
    "PartName",
    "BatchName",
    "SrNo",
    "BatchNo",
    "TimeStamp",
    "FrequencyData",
    "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":400,"gain":10,"phase":0,"isEnable":true,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":2,"Name":"D2","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":3,"Name":"D3","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":5,"Name":"D5","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":6,"Name":"D6","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":7,"Name":"D7","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":8,"Name":"D8","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0}
    ]'::json,
    '{"ProductionOrder":"PO-D1-TEST","MachineNumber":"M1","PartNumber":"P1","PartFamily":"F1","BatchName":"I","CheckedBy":"Operator A"}'::json,
    'P1',
    'BATCH-D1-ONLY',
    'D1-001',
    1,
    '2026-10-15 10:00:00',
    '[{"FN":1,"R":1,"X":15,"Y":24}]'::jsonb,
    '[{"FN":1,"R":1,"X":15,"Y":24}]'::jsonb
);

-- ----------------------------------------------------------------------------
-- Record 2: PASS (D1 ON, Coordinates: (18, 28), Result: PASS)
-- ----------------------------------------------------------------------------
INSERT INTO public."Logs" (
    "ChId",
    "Result",
    "FDData",
    "PartData",
    "PartName",
    "BatchName",
    "SrNo",
    "BatchNo",
    "TimeStamp",
    "FrequencyData",
    "ResultData"
) VALUES (
    1,
    true,
    '[
        {"Id":1,"Name":"D1","freq":400,"gain":10,"phase":0,"isEnable":true,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":2,"Name":"D2","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":3,"Name":"D3","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":5,"Name":"D5","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":6,"Name":"D6","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":7,"Name":"D7","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":8,"Name":"D8","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0}
    ]'::json,
    '{"ProductionOrder":"PO-D1-TEST","MachineNumber":"M1","PartNumber":"P1","PartFamily":"F1","BatchName":"I","CheckedBy":"Operator A"}'::json,
    'P1',
    'BATCH-D1-ONLY',
    'D1-002',
    1,
    '2026-10-15 10:05:00',
    '[{"FN":1,"R":1,"X":18,"Y":28}]'::jsonb,
    '[{"FN":1,"R":1,"X":18,"Y":28}]'::jsonb
);

-- ----------------------------------------------------------------------------
-- Record 3: FAIL (D1 ON, Coordinates: (75, 82), Result: FAIL)
-- ----------------------------------------------------------------------------
INSERT INTO public."Logs" (
    "ChId",
    "Result",
    "FDData",
    "PartData",
    "PartName",
    "BatchName",
    "SrNo",
    "BatchNo",
    "TimeStamp",
    "FrequencyData",
    "ResultData"
) VALUES (
    1,
    false,
    '[
        {"Id":1,"Name":"D1","freq":400,"gain":10,"phase":0,"isEnable":true,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":2,"Name":"D2","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":3,"Name":"D3","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":4,"Name":"D4","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":5,"Name":"D5","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":6,"Name":"D6","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":7,"Name":"D7","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0},
        {"Id":8,"Name":"D8","freq":400,"gain":10,"phase":0,"isEnable":false,"height":120.0,"width":120.0,"ex":30.0,"ey":30.0,"angel":30.0}
    ]'::json,
    '{"ProductionOrder":"PO-D1-TEST","MachineNumber":"M1","PartNumber":"P1","PartFamily":"F1","BatchName":"I","CheckedBy":"Operator A"}'::json,
    'P1',
    'BATCH-D1-ONLY',
    'D1-003',
    1,
    '2026-10-15 10:10:00',
    '[{"FN":1,"R":0,"X":75,"Y":82}]'::jsonb,
    '[{"FN":1,"R":0,"X":75,"Y":82}]'::jsonb
);
