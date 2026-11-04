-- Migration script for 8F Role-Based Password Authentication
-- Creates AuthCredentials and AuthAuditLog tables in PostgreSQL.

CREATE TABLE IF NOT EXISTS public."AuthCredentials" (
    "Role"          VARCHAR(16)  PRIMARY KEY CHECK ("Role" IN ('Admin','Operator')),
    "PasswordHash"  BYTEA        NOT NULL,
    "Salt"          BYTEA        NOT NULL,
    "Iterations"    INTEGER      NOT NULL,
    "UpdatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedByRole" VARCHAR(16)  NULL
);

CREATE TABLE IF NOT EXISTS public."AuthAuditLog" (
    "Id"        BIGSERIAL PRIMARY KEY,
    "EventTime" TIMESTAMPTZ NOT NULL DEFAULT now(),
    "Event"     VARCHAR(32) NOT NULL,   -- LoginSuccess, LoginFailed, Logout, PasswordChanged, PasswordReset, FirstRunSetup, ExitNoAuth
    "Role"      VARCHAR(16) NULL,
    "Detail"    TEXT NULL
);
