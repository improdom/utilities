WITH SUMMARY AS (
    SELECT
        S.NAME AS QUERYSPACE,
        R.QUERY_ID AS QUERYID,
        Q.NAME AS QUERYNAME,
        R.COB_DATE AS COBDATE,
        
        -- Execution status
        CASE 
            WHEN QE.STATUS IS NULL THEN 'Pending'
            ELSE QE.STATUS
        END AS executionStatus,
        
        -- Readiness status
        COUNT(DISTINCT R.NODE_ID) AS totalNodes,
        COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = FALSE) AS pendingNodes,
        COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = TRUE) AS readyNodes,
        
        CASE
            WHEN COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = TRUE) = 0 THEN 'Pending'
            WHEN COUNT(DISTINCT R.NODE_ID) FILTER (WHERE IS_READY = FALSE) = 0 THEN 'Ready'
            ELSE 'InProgress'
        END AS readinessStatus

    FROM ...
    ...
)
