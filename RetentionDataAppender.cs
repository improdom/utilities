/* ============================================================
   Recreate tables for PbiQueryStress (SQL MI / SQL Server)
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
        benchmark_id        INT IDENTITY(1,1) NOT NULL,
        name                NVARCHAR(256) NULL,
        created_by          NVARCHAR(256) NULL,
        connection_string   NVARCHAR(MAX) NULL,
        number_iterations   INT NOT NULL CONSTRAINT DF_benchmark_number_iterations DEFAULT (1),
        number_threads      INT NOT NULL CONSTRAINT DF_benchmark_number_threads DEFAULT (1),
        query_delay         INT NOT NULL CONSTRAINT DF_benchmark_query_delay DEFAULT (0),
        run_options         NVARCHAR(MAX) NULL,
        concurrency         INT NULL,

        CONSTRAINT PK_benchmark PRIMARY KEY CLUSTERED (benchmark_id)
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
        query_id        INT IDENTITY(1,1) NOT NULL,
        query_name      NVARCHAR(256) NULL,
        dax_query       NVARCHAR(MAX) NULL,
        benchmark_id    INT NOT NULL,

        CONSTRAINT PK_query_template PRIMARY KEY CLUSTERED (query_id)
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
        runtime_id                   INT IDENTITY(1,1) NOT NULL,
        query_id                     INT NULL,
        dax                          NVARCHAR(MAX) NULL,

        start_time                   DATETIME2(7) NULL,
        end_time                     DATETIME2(7) NULL,
        runtime_ms                   FLOAT NULL,

        pbi_workspace                NVARCHAR(512) NULL,
        error                        NVARCHAR(MAX) NULL,
        status                       NVARCHAR(128) NULL,
        user_id                      NVARCHAR(256) NULL,

        benchmark_name               NVARCHAR(256) NULL,

        data_transfer_start_time     DATETIME2(7) NULL,
        data_transfer_end_time       DATETIME2(7) NULL,
        data_transfer_ms             FLOAT NULL,

        benchmark_run_id             INT NULL,
        number_threads               INT NULL,
        number_iterations            INT NULL,
        benchmark_run_name           NVARCHAR(256) NULL,

        concurrency                  INT NULL,
        thread_id                    NVARCHAR(128) NULL,

        CONSTRAINT PK_query_runtime PRIMARY KEY CLUSTERED (runtime_id)
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
        parameter_id         INT IDENTITY(1,1) NOT NULL,
        name                 NVARCHAR(256) NULL,
        mapping_column       NVARCHAR(256) NULL,
        query                NVARCHAR(MAX) NULL,
        connectionString     NVARCHAR(MAX) NULL,
        data_type            NVARCHAR(128) NULL,
        format               NVARCHAR(128) NULL,
        report_expression    NVARCHAR(MAX) NULL,
        param_value          NVARCHAR(MAX) NULL,

        query_id             INT NULL,
        benchmark_id         INT NULL,
        benchmark_param      BIT NOT NULL CONSTRAINT DF_parameters_benchmark_param DEFAULT (0),
        query_name           NVARCHAR(256) NULL,

        CONSTRAINT PK_parameters PRIMARY KEY CLUSTERED (parameter_id)
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
        id               INT IDENTITY(1,1) NOT NULL,
        business_date    NVARCHAR(64) NULL,
        local_value      NVARCHAR(MAX) NULL,
        src_book_id      NVARCHAR(256) NULL,
        source           NVARCHAR(128) NULL,
        measure_name     NVARCHAR(256) NULL,
        level_name       NVARCHAR(256) NULL,
        run_id           NVARCHAR(256) NULL,
        measure_value    FLOAT NULL,
        benchmark_run_id FLOAT NULL,

        CONSTRAINT PK_arc_mrv_recon_target_data PRIMARY KEY CLUSTERED (id)
    );
END
GO
