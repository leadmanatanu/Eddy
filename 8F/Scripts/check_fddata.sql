SELECT "Id", "BatchName", "ChId", "FDData"::text
FROM public."Logs"
WHERE "BatchName" = 'BATCH-2026-MOCK'
LIMIT 1;
