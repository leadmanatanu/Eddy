-- ============================================================================
-- EDDY SHORTER DATABASE (EddyShorter) — COMPLETE DDL & SEED SCRIPT
-- Application: 8F Eddy Current Testing (ECT) Inspection System
-- Database Name: EddyShorter
-- Target RDBMS: PostgreSQL 12+
-- Description: Complete schema creation script including inspection logs,
--              auto-ellipse calibration audit trails, configuration persistence,
--              master data tables, and initial seed values.
-- ============================================================================

-- Create Database (Run separately if creating fresh database):
-- CREATE DATABASE "EddyShorter" WITH OWNER = postgres ENCODING = 'UTF8';

-- Connect to EddyShorter database before running the script below:
-- \c "EddyShorter"

-- ============================================================================
-- 1. MASTER DATA: OPERATORS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."Operators"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "OperatorName" character varying(100) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true
);

ALTER TABLE IF EXISTS public."Operators" OWNER to postgres;


-- ============================================================================
-- 2. MASTER DATA: PART FAMILIES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."PartFamilies"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "FamilyName" character varying(100) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true
);

ALTER TABLE IF EXISTS public."PartFamilies" OWNER to postgres;


-- ============================================================================
-- 3. MASTER DATA: PARTS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."Parts"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "PartFamilyId" integer NOT NULL,
    "PartNumber" character varying(100) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    CONSTRAINT "FK_Parts_PartFamilies" FOREIGN KEY ("PartFamilyId")
        REFERENCES public."PartFamilies" ("Id") MATCH SIMPLE
        ON UPDATE NO ACTION ON DELETE CASCADE
);

ALTER TABLE IF EXISTS public."Parts" OWNER to postgres;


-- ============================================================================
-- 4. PRODUCTION INSPECTION LOGS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."Logs"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ChId" integer NOT NULL,
    "Result" boolean NOT NULL,
    "TimeStamp" timestamp without time zone,
    "FDData" json,
    "PartData" json,
    "PartName" text,
    "BatchName" text,
    "SrNo" text,
    "BatchNo" bigint,
    "Ch1Result" boolean,
    "Ch2Result" boolean,
    "Ch3Result" boolean,
    "Ch4Result" boolean,
    "FrequencyData" jsonb,
    "ResultData" jsonb
);

ALTER TABLE IF EXISTS public."Logs" OWNER to postgres;


-- ============================================================================
-- 5. AUTO ELLIPSE CALIBRATION: RAW TEST RUNS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."AutoEllipseTests"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ChId" integer NOT NULL,
    "TestNumber" integer NOT NULL,
    "TimeStamp" timestamp without time zone NOT NULL DEFAULT now(),
    "OperatorName" text,
    "FrequencyValues" json NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

ALTER TABLE IF EXISTS public."AutoEllipseTests" OWNER to postgres;


-- ============================================================================
-- 6. AUTO ELLIPSE CALIBRATION: AUDIT TRAIL RESULTS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."AutoEllipseResults"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ChId" integer NOT NULL,
    "Frequency" text NOT NULL,
    "TimeStamp" timestamp without time zone NOT NULL DEFAULT now(),
    "SelectedTestIds" json NOT NULL,
    "ComputedCenterX" numeric,
    "ComputedCenterY" numeric,
    "ComputedWidth" numeric,
    "ComputedHeight" numeric,
    "ComputedRotationAngle" numeric,
    "SampleCount" integer NOT NULL
);

ALTER TABLE IF EXISTS public."AutoEllipseResults" OWNER to postgres;


-- ============================================================================
-- 7. CONFIGURATION PERSISTENCE: PROFILES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."ConfigProfiles"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Name" text NOT NULL,
    "OperatorName" text,
    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp without time zone NOT NULL DEFAULT now(),
    "SystemSettingsJson" text
);

ALTER TABLE public."ConfigProfiles" ADD COLUMN IF NOT EXISTS "SystemSettingsJson" text;

ALTER TABLE IF EXISTS public."ConfigProfiles" OWNER to postgres;


-- ============================================================================
-- 8. CONFIGURATION PERSISTENCE: CHANNELS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."ConfigChannels"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ConfigProfileId" integer NOT NULL REFERENCES public."ConfigProfiles"("Id") ON DELETE CASCADE,
    "ChannelNumber" integer NOT NULL,
    "IsSelected" boolean NOT NULL DEFAULT false,
    "TxStrength" numeric
);

ALTER TABLE IF EXISTS public."ConfigChannels" OWNER to postgres;


-- ============================================================================
-- 9. CONFIGURATION PERSISTENCE: FREQUENCIES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."ConfigFrequencies"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ConfigChannelId" integer NOT NULL REFERENCES public."ConfigChannels"("Id") ON DELETE CASCADE,
    "FrequencyNumber" integer NOT NULL,
    "Name" text NOT NULL,
    "Freq" numeric,
    "Gain" numeric,
    "Phase" numeric,
    "IsEnable" boolean NOT NULL DEFAULT true,
    "Strength" numeric,
    "PostGain" numeric,
    -- Top-level default ellipse parameters
    "Height" numeric,
    "Width" numeric,
    "Ex" numeric,
    "Ey" numeric,
    "Angel" numeric,
    -- Overlay ellipse parameters
    "HeightO" numeric,
    "WidthO" numeric,
    "ExO" numeric,
    "EyO" numeric,
    "AngelO" numeric
);

ALTER TABLE IF EXISTS public."ConfigFrequencies" OWNER to postgres;


-- ============================================================================
-- 10. CONFIGURATION PERSISTENCE: ELLIPSES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public."ConfigEllipses"
(
    "Id" integer NOT NULL GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ConfigFrequencyId" integer NOT NULL REFERENCES public."ConfigFrequencies"("Id") ON DELETE CASCADE,
    "EllipseIndex" integer NOT NULL,
    "Height" numeric,
    "Width" numeric,
    "Ex" numeric,
    "Ey" numeric,
    "Angel" numeric
);

ALTER TABLE IF EXISTS public."ConfigEllipses" OWNER to postgres;

--its important for some point of insertion of pre-define data 
-- ============================================================================
-- 11. INITIAL SEED DATA: OPERATORS
-- ============================================================================
INSERT INTO public."Operators" ("OperatorName")
SELECT 'Operator 1' WHERE NOT EXISTS (SELECT 1 FROM public."Operators" WHERE "OperatorName" = 'Operator 1');

INSERT INTO public."Operators" ("OperatorName")
SELECT 'Operator 2' WHERE NOT EXISTS (SELECT 1 FROM public."Operators" WHERE "OperatorName" = 'Operator 2');

INSERT INTO public."Operators" ("OperatorName")
SELECT 'Operator 3' WHERE NOT EXISTS (SELECT 1 FROM public."Operators" WHERE "OperatorName" = 'Operator 3');

INSERT INTO public."Operators" ("OperatorName")
SELECT 'Operator 4' WHERE NOT EXISTS (SELECT 1 FROM public."Operators" WHERE "OperatorName" = 'Operator 4');


-- ============================================================================
-- 12. INITIAL SEED DATA: PART FAMILIES & PARTS
-- ============================================================================
INSERT INTO public."PartFamilies" ("FamilyName")
SELECT 'Main Plate' WHERE NOT EXISTS (SELECT 1 FROM public."PartFamilies" WHERE "FamilyName" = 'Main Plate');

INSERT INTO public."PartFamilies" ("FamilyName")
SELECT 'Cover Plate' WHERE NOT EXISTS (SELECT 1 FROM public."PartFamilies" WHERE "FamilyName" = 'Cover Plate');

INSERT INTO public."PartFamilies" ("FamilyName")
SELECT 'Hub Flange' WHERE NOT EXISTS (SELECT 1 FROM public."PartFamilies" WHERE "FamilyName" = 'Hub Flange');

INSERT INTO public."PartFamilies" ("FamilyName")
SELECT 'Hub' WHERE NOT EXISTS (SELECT 1 FROM public."PartFamilies" WHERE "FamilyName" = 'Hub');

INSERT INTO public."PartFamilies" ("FamilyName")
SELECT 'Support Washer' WHERE NOT EXISTS (SELECT 1 FROM public."PartFamilies" WHERE "FamilyName" = 'Support Washer');

-- Seed Sample Parts if table is empty
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 1, 'L-03120-0HQ5-00' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03120-0HQ5-00');
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 1, 'L-03120-1554-00' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03120-1554-00');
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 2, 'L-03124-0GP4-00' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03124-0GP4-00');
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 3, 'L-03125-0GY7-01' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03125-0GY7-01');
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 4, 'L-03133-0GF8-06' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03133-0GF8-06');
INSERT INTO public."Parts" ("PartFamilyId", "PartNumber")
SELECT 5, 'L-03158-0GK5-00' WHERE NOT EXISTS (SELECT 1 FROM public."Parts" WHERE "PartNumber" = 'L-03158-0GK5-00');

-- ============================================================================
-- END OF SCRIPT
-- ============================================================================
