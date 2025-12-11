Event Radar remains the single source
  
  
  of truth. The current Marvel→Event Radar pipeline stays in place to feed status screens and readiness views, and ARC Orchestrator is only used temporarily to run CubIQ components—not to replace or split status tracking. FO events still originate from ARC, and readiness updates continue flowing through Marvel into Event Radar and onto the
  
  
  SELECT STRING_AGG(
        '"' + REPLACE(REPLACE(CAST(Column1 AS NVARCHAR(MAX)), '"', '""'), ',', '\,') 
        + '","' +
        REPLACE(REPLACE(CAST(Column2 AS NVARCHAR(MAX)), '"', '""'), ',', '\,')
        + '"'
    , CHAR(10))
FROM dbo.YourTable;
  






reports.



  
USE YourDatabaseName;   -- change this
GO

IF OBJECT_ID('dbo.ExportTableAsCsv') IS NOT NULL
    DROP PROC dbo.ExportTableAsCsv;
GO

CREATE PROC dbo.ExportTableAsCsv
    @SchemaName sysname,
    @TableName  sysname,
    @Where      nvarchar(max) = NULL   -- optional filter, e.g. N'WHERE is_active = ''Y'''
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FullName nvarchar(400) =
        QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName);

    IF OBJECT_ID(@FullName) IS NULL
    BEGIN
        RAISERROR('Table %s does not exist', 16, 1, @FullName);
        RETURN;
    END

    /* Build header: "col1","col2",... */
    DECLARE @Header nvarchar(max);

    SELECT @Header = STUFF((
        SELECT ',' + QUOTENAME(c.name, '"')
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(@FullName)
        ORDER BY c.column_id
        FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
    , 1, 1, '');

    /* Build data line: "val1","val2",... with " escaped as "" */
    DECLARE @SelectList nvarchar(max);

    SELECT @SelectList = STUFF((
        SELECT ',' +
           'ISNULL(''""'' + REPLACE(CAST(' + QUOTENAME(c.name) +
           ' AS nvarchar(max)), ''"'' , ''""'') + ''""'', ''""'')'
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(@FullName)
        ORDER BY c.column_id
        FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
    , 1, 1, '');

    DECLARE @Sql nvarchar(max) = N'
        SET NOCOUNT ON;
        SELECT ''' + @Header + N''' AS CSV
        UNION ALL
        SELECT ' + @SelectList + N'
        FROM ' + @FullName + N'
        ' + COALESCE(@Where, N'') + N';';

    EXEC sp_executesql @Sql;
END
GO


USE YourDatabaseName;   -- change this
GO

IF OBJECT_ID('dbo.ExportTableAsCsv') IS NOT NULL
    DROP PROC dbo.ExportTableAsCsv;
GO

CREATE PROC dbo.ExportTableAsCsv
    @SchemaName sysname,
    @TableName  sysname,
    @Where      nvarchar(max) = NULL   -- optional filter, e.g. N'WHERE is_active = ''Y'''
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FullName nvarchar(400) =
        QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName);

    IF OBJECT_ID(@FullName) IS NULL
    BEGIN
        RAISERROR('Table %s does not exist', 16, 1, @FullName);
        RETURN;
    END;

    /* Build header row: "col1","col2",... */
    DECLARE @Header nvarchar(max);

    SELECT @Header = STUFF((
        SELECT ',' + QUOTENAME(c.name, '"')
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(@FullName)
        ORDER BY c.column_id
        FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
    , 1, 1, '');

    /* Build concatenation expression for data row: "val1","val2",... */
    DECLARE @ConcatExpr nvarchar(max) = N'';

    SELECT @ConcatExpr =
        @ConcatExpr +
        CASE WHEN @ConcatExpr = N'' THEN N'' ELSE N' + '','' + ' END +
        N'ISNULL(''""'' + REPLACE(CAST(' + QUOTENAME(c.name) +
        N' AS nvarchar(max)), ''"'' , ''""'') + ''""'', ''""'')'
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(@FullName)
    ORDER BY c.column_id;

    /* Build and execute final SQL */
    DECLARE @Sql nvarchar(max) = N'
        SET NOCOUNT ON;

        SELECT ''' + @Header + N''' AS CSV
        UNION ALL
        SELECT ' + @ConcatExpr + N' AS CSV
        FROM ' + @FullName + N'
        ' + COALESCE(@Where, N'') + N';';

    EXEC sp_executesql @Sql;
END;
GO

