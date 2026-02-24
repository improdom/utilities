/* ============================================================
   Tables without PK / FK / indexes
   ============================================================ */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ============================================================
   1) benchmark
   ============================================================ */
IF OBJECT_ID('dbo.benchmark', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.benchmark
    (
        benchmark_id        INT,
        name                NVARCHAR(256),
        created_by          NVARCHAR(256),
        connection_string   NVARCHAR(MAX),
        number_iterations   INT,
        number_threads      INT,
        query_delay         INT,
        run_options         NVARCHAR(MAX),
        concurrency         INT
    );
END
GO

/* ============================================================
   2) query_template
   ============================================================ */
IF OBJECT_ID('dbo.query_template', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.query_template
    (
        query_id        INT,
        query_name      NVARCHAR(256),
        dax_query       NVARCHAR(MAX),
        benchmark_id    INT
    );
END
GO

/* ============================================================
   3) query_runtime
   ============================================================ */
IF OBJECT_ID('dbo.query_runtime', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.query_runtime
    (
        runtime_id                   INT,
        query_id                     INT,
        dax                          NVARCHAR(MAX),

        start_time                   DATETIME2(7),
        end_time                     DATETIME2(7),
        runtime_ms                   FLOAT,

        pbi_workspace                NVARCHAR(512),
        error                        NVARCHAR(MAX),
        status                       NVARCHAR(128),
        user_id                      NVARCHAR(256),

        benchmark_name               NVARCHAR(256),

        data_transfer_start_time     DATETIME2(7),
        data_transfer_end_time       DATETIME2(7),
        data_transfer_ms             FLOAT,

        benchmark_run_id             INT,
        number_threads               INT,
        number_iterations            INT,
        benchmark_run_name           NVARCHAR(256),

        concurrency                  INT,
        thread_id                    NVARCHAR(128)
    );
END
GO

/* ============================================================
   4) parameters
   ============================================================ */
IF OBJECT_ID('dbo.parameters', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.parameters
    (
        parameter_id         INT,
        name                 NVARCHAR(256),
        mapping_column       NVARCHAR(256),
        query                NVARCHAR(MAX),
        connectionString     NVARCHAR(MAX),
        data_type            NVARCHAR(128),
        format               NVARCHAR(128),
        report_expression    NVARCHAR(MAX),
        param_value          NVARCHAR(MAX),

        query_id             INT,
        benchmark_id         INT,
        benchmark_param      BIT,
        query_name           NVARCHAR(256)
    );
END
GO

/* ============================================================
   5) arc_mrv_recon_target_data
   ============================================================ */
IF OBJECT_ID('dbo.arc_mrv_recon_target_data', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.arc_mrv_recon_target_data
    (
        id               INT,
        business_date    NVARCHAR(64),
        local_value      NVARCHAR(MAX),
        src_book_id      NVARCHAR(256),
        source           NVARCHAR(128),
        measure_name     NVARCHAR(256),
        level_name       NVARCHAR(256),
        run_id           NVARCHAR(256),
        measure_value    FLOAT,
        benchmark_run_id FLOAT
    );
END
GO
