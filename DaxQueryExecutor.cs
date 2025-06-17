WITH SUMMARY AS (
    SELECT 
        S.NAME AS QUERYSPACE,
        R.QUERY_ID AS QUERYID,
        Q.NAME AS QUERYNAME,
        R.COB_DATE AS COBDATE,
        CASE 
            WHEN QE.STATUS IS NULL THEN 'Pending'
            ELSE QE.STATUS
        END AS QUERYSTATUS,
        COUNT(DISTINCT R.NODE_ID) AS TOTALNODES,
        COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = FALSE) AS PENDINGNODES,
        COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = TRUE) AS READYNODES,
        CASE
            WHEN COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = TRUE) = 0 THEN 'Pending'
            WHEN COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = FALSE) = 0 THEN 'Ready'
            ELSE 'InProgress'
        END AS READINESSSTATUS
    FROM mr_agg_model_refresh.pbi_query_readiness_status R
    JOIN mr_agg_model_refresh.pbi_queries Q 
      ON R.QUERY_ID = Q.ID
    JOIN mr_agg_model_refresh.pbi_query_spaces S 
      ON Q.QUERY_SPACE_ID = S.ID
    LEFT JOIN mr_agg_model_refresh.pbi_query_execution_status QE 
      ON Q.ID = QE.QUERY_ID AND R.COB_DATE = QE.COB_DATE
    WHERE R.COB_DATE = TO_DATE(:cobdate, 'yyyy-MM-dd')
    GROUP BY R.QUERY_ID, Q.NAME, S.NAME, QE.STATUS, R.COB_DATE
),

AGGREGATED AS (
    SELECT 
        QUERYSPACE,
        COBDATE,
        COUNT(*) AS TOTAL_QUERIES,
        COUNT(*) FILTER (WHERE QUERYSTATUS = 'Succeeded') AS SUCCESSFUL_QUERIES
    FROM SUMMARY
    GROUP BY QUERYSPACE, COBDATE
),

FINAL AS (
    SELECT
        A.QUERYSPACE,
        A.COBDATE,
        CASE 
            WHEN A.SUCCESSFUL_QUERIES = A.TOTAL_QUERIES THEN 'Ready'
            WHEN A.SUCCESSFUL_QUERIES = 0 THEN 'Pending'
            ELSE 'InProgress'
        END AS QUERYSPACESTATUS,
        COALESCE(QS.READY_EVENT_PUBLISHED, FALSE) AS READY_EVENT_PUBLISHED
    FROM AGGREGATED A
    LEFT JOIN mr_agg_model_refresh.pbi_query_space_execution_status QS
      ON QS.query_space_id = A.QUERYSPACE
     AND QS.cob_date = A.COBDATE
)

SELECT 
    A.QUERYSPACE,
    A.QUERYID,
    A.QUERYNAME,
    A.COBDATE,
    A.QUERYSTATUS,
    A.TOTALNODES,
    A.PENDINGNODES,
    A.READYNODES,
    A.READINESSSTATUS,
    CM.QUERYSPACESTATUS,
    CM.READY_EVENT_PUBLISHED
FROM FINAL CM
JOIN SUMMARY A 
  ON CM.QUERYSPACE = A.QUERYSPACE 
 AND CM.COBDATE = A.COBDATE
ORDER BY A.QUERYID;
