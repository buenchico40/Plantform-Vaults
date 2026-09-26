-- PlatformVault · Secuencias para códigos legibles (RN-001)
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID('app.ObjectCodeSeq') IS NULL  CREATE SEQUENCE app.ObjectCodeSeq AS int START WITH 1 INCREMENT BY 1;
IF OBJECT_ID('app.GroupCodeSeq') IS NULL   CREATE SEQUENCE app.GroupCodeSeq AS int START WITH 1 INCREMENT BY 1;
IF OBJECT_ID('app.RequestCodeSeq') IS NULL CREATE SEQUENCE app.RequestCodeSeq AS int START WITH 1 INCREMENT BY 1;
GO
