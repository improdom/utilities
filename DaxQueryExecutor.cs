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

