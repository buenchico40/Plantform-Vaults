-- PlatformVault · Validación: integridad de la cadena de hashes de auditoría (RN-076)
SET NOCOUNT ON;
DECLARE @R TABLE (EventsVerified bigint, FirstBrokenSequence bigint, FromSequence bigint, ToSequence bigint);
INSERT @R EXEC audit.usp_AuditChain_Verify;
IF EXISTS (SELECT 1 FROM @R WHERE FirstBrokenSequence IS NOT NULL)
BEGIN
    DECLARE @Broken bigint = (SELECT FirstBrokenSequence FROM @R);
    RAISERROR(N'Cadena de auditoría rota en la secuencia %I64d.', 16, 1, @Broken); RETURN;
END
DECLARE @Verified bigint = (SELECT EventsVerified FROM @R);
PRINT CONCAT(N'OK cadena de auditoría: ', @Verified, N' eventos verificados.');
GO
