-- ====================================================================
-- PostgreSQL Database & Table Setup Script for EddyFaster
-- Application: EddyFaster (Eddy Current Inspection System)
-- Target Database: Eddy
-- Target Table: public."Logs"
-- Default Connection: Host=localhost;Port=5432;Username=postgres;Password=ary123;Database=Eddy
-- ====================================================================

-- 1. Create Database 'Eddy' (Run separately if connecting to postgres maintenance db)
-- CREATE DATABASE "Eddy" WITH OWNER = postgres ENCODING = 'UTF8';

-- 2. Create "Logs" Table
CREATE TABLE IF NOT EXISTS public."Logs"
(
    "Id"            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ChId"          INTEGER NOT NULL DEFAULT 1,
    "Result"        BOOLEAN NOT NULL DEFAULT TRUE,
    "TimeStamp"     TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "FDData"        JSON,
    "PartData"      JSON,
    "PartName"      TEXT,
    "BatchName"     TEXT,
    "SrNo"          TEXT,
    "BatchNo"       BIGINT DEFAULT 0,
    "Ch1Result"     BOOLEAN,
    "Ch2Result"     BOOLEAN,
    "Ch3Result"     BOOLEAN,
    "Ch4Result"     BOOLEAN
)
TABLESPACE pg_default;

ALTER TABLE IF EXISTS public."Logs" OWNER TO postgres;

-- 3. Idempotent Column Additions (for existing databases)
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "ChId" INTEGER NOT NULL DEFAULT 1;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "Result" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "TimeStamp" TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "FDData" JSON;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "PartData" JSON;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "PartName" TEXT;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "BatchName" TEXT;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "SrNo" TEXT;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "BatchNo" BIGINT DEFAULT 0;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "Ch1Result" BOOLEAN;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "Ch2Result" BOOLEAN;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "Ch3Result" BOOLEAN;
ALTER TABLE public."Logs" ADD COLUMN IF NOT EXISTS "Ch4Result" BOOLEAN;

-- 4. Column Documentation
COMMENT ON TABLE  public."Logs"               IS 'Inspection logs table for EddyFaster application';
COMMENT ON COLUMN public."Logs"."Id"          IS 'Auto-incrementing surrogate primary key';
COMMENT ON COLUMN public."Logs"."ChId"        IS 'Channel Identifier (1, 2, 3, or 4)';
COMMENT ON COLUMN public."Logs"."Result"      IS 'Overall test result (true = OK, false = Not OK)';
COMMENT ON COLUMN public."Logs"."TimeStamp"   IS 'Inspection timestamp';
COMMENT ON COLUMN public."Logs"."FDData"      IS 'Frequency / Graph sensor measurement data (JSON)';
COMMENT ON COLUMN public."Logs"."PartData"    IS 'Part configuration snapshot data (JSON)';
COMMENT ON COLUMN public."Logs"."PartName"    IS 'Name of inspected part';
COMMENT ON COLUMN public."Logs"."BatchName"   IS 'Production batch name';
COMMENT ON COLUMN public."Logs"."SrNo"        IS 'Serial number / barcode of component';
COMMENT ON COLUMN public."Logs"."BatchNo"     IS 'Batch sequential counter';
COMMENT ON COLUMN public."Logs"."Ch1Result"   IS 'Channel 1 individual evaluation status';
COMMENT ON COLUMN public."Logs"."Ch2Result"   IS 'Channel 2 individual evaluation status';
COMMENT ON COLUMN public."Logs"."Ch3Result"   IS 'Channel 3 individual evaluation status';
COMMENT ON COLUMN public."Logs"."Ch4Result"   IS 'Channel 4 individual evaluation status';

-- 5. Performance Indexes (Optimized for EddyFaster queries in Logs.xaml & LogsAll.xaml)
CREATE INDEX IF NOT EXISTS "IX_Logs_TimeStamp" 
    ON public."Logs" ("TimeStamp" DESC);

CREATE INDEX IF NOT EXISTS "IX_Logs_BatchName" 
    ON public."Logs" ("BatchName");

CREATE INDEX IF NOT EXISTS "IX_Logs_SrNo" 
    ON public."Logs" ("SrNo");

CREATE INDEX IF NOT EXISTS "IX_Logs_BatchName_BatchNo" 
    ON public."Logs" ("BatchName", "BatchNo");

CREATE INDEX IF NOT EXISTS "IX_Logs_Result" 
    ON public."Logs" ("Result");
