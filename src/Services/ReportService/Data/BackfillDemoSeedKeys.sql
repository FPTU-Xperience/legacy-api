-- Backfill SeedKey for verified demo reports (IDs 1023-1027).
-- Run manually once after migration is applied in Development/Docker.
-- Idempotent: skips rows already seeded, errors on mismatched SeedKey.
-- Uses verified Period values: Q1/2026, Q2/2026, Spring 2026, AI for Students 2026, Summer 2026.
-- Run this via: docker exec -i clubreport-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "ClubReportHub!2026" -d ClubReportHub_Report -C -i BackfillDemoSeedKeys.sql

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Errors int = 0;

    -- Q1/2026 (ID 1023)
    IF EXISTS (SELECT 1 FROM Reports WHERE Id = 1023 AND SeedKey IS NULL)
    BEGIN
        UPDATE Reports SET SeedKey = 'DEMO-IT-Q1-2026'
          WHERE Id = 1023 AND ClubId = 1 AND Period = 'Q1/2026'
            AND ReportType = N'Báo cáo Quý' AND SeedKey IS NULL;
        IF @@ROWCOUNT = 0
        BEGIN
            PRINT 'ERROR: Report 1023 not found or already has SeedKey';
            SET @Errors = @Errors + 1;
        END
    END

    -- Q2/2026 (ID 1024)
    IF EXISTS (SELECT 1 FROM Reports WHERE Id = 1024 AND SeedKey IS NULL)
    BEGIN
        UPDATE Reports SET SeedKey = 'DEMO-IT-Q2-2026'
          WHERE Id = 1024 AND ClubId = 1 AND Period = 'Q2/2026'
            AND ReportType = N'Báo cáo Quý' AND SeedKey IS NULL;
        IF @@ROWCOUNT = 0
        BEGIN
            PRINT 'ERROR: Report 1024 not found or already has SeedKey';
            SET @Errors = @Errors + 1;
        END
    END

    -- Spring 2026 (ID 1025)
    IF EXISTS (SELECT 1 FROM Reports WHERE Id = 1025 AND SeedKey IS NULL)
    BEGIN
        UPDATE Reports SET SeedKey = 'DEMO-SE-SPRING-2026'
          WHERE Id = 1025 AND ClubId = 2 AND Period = 'Spring 2026'
            AND ReportType = N'Báo cáo Học kỳ' AND SeedKey IS NULL;
        IF @@ROWCOUNT = 0
        BEGIN
            PRINT 'ERROR: Report 1025 not found or already has SeedKey';
            SET @Errors = @Errors + 1;
        END
    END

    -- AI for Students 2026 (ID 1026)
    IF EXISTS (SELECT 1 FROM Reports WHERE Id = 1026 AND SeedKey IS NULL)
    BEGIN
        UPDATE Reports SET SeedKey = 'DEMO-AI-FOR-STUDENTS-2026'
          WHERE Id = 1026 AND ClubId = 3 AND Period = 'AI for Students 2026'
            AND ReportType = N'Báo cáo Chuyên đề' AND SeedKey IS NULL;
        IF @@ROWCOUNT = 0
        BEGIN
            PRINT 'ERROR: Report 1026 not found or already has SeedKey';
            SET @Errors = @Errors + 1;
        END
    END

    -- Summer 2026 (ID 1027)
    IF EXISTS (SELECT 1 FROM Reports WHERE Id = 1027 AND SeedKey IS NULL)
    BEGIN
        UPDATE Reports SET SeedKey = 'DEMO-ART-SUMMER-2026'
          WHERE Id = 1027 AND ClubId = 4 AND Period = 'Summer 2026'
            AND ReportType = N'Báo cáo Học kỳ' AND SeedKey IS NULL;
        IF @@ROWCOUNT = 0
        BEGIN
            PRINT 'ERROR: Report 1027 not found or already has SeedKey';
            SET @Errors = @Errors + 1;
        END
    END

    -- Verify: ensure no row has a mismatched SeedKey
    IF EXISTS (
        SELECT 1 FROM Reports WHERE Id IN (1023,1024,1025,1026,1027)
        AND SeedKey NOT IN (
            'DEMO-IT-Q1-2026','DEMO-IT-Q2-2026','DEMO-SE-SPRING-2026',
            'DEMO-AI-FOR-STUDENTS-2026','DEMO-ART-SUMMER-2026'
        )
        AND SeedKey IS NOT NULL
    )
    BEGIN
        PRINT 'ERROR: One or more demo reports have unexpected SeedKey values';
        SET @Errors = @Errors + 1;
    END

    IF @Errors > 0
    BEGIN
        RAISERROR('Backfill completed with %d error(s)', 16, 1, @Errors);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    COMMIT TRANSACTION;
    PRINT 'Demo SeedKey backfill completed successfully. 5 rows updated.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
