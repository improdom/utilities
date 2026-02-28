DECLARE @runA INT = 8544;
DECLARE @runB INT = 8640;
DECLARE @eps  FLOAT = 0.0000001;

WITH A AS (
    SELECT
        mrv_id,
        measurename,
        SUM(CAST(measurevalue AS FLOAT)) AS runA_sum
    FROM [cubiq].[st_mrv_recon_target_data]
    WHERE benchmark_run_id = @runA
    GROUP BY mrv_id, measurename
),
B AS (
    SELECT
        mrv_id,
        measurename,
        SUM(CAST(measurevalue AS FLOAT)) AS runB_sum
    FROM [cubiq].[st_mrv_recon_target_data]
    WHERE benchmark_run_id = @runB
    GROUP BY mrv_id, measurename
)
SELECT
    COALESCE(A.mrv_id, B.mrv_id) AS mrv_id,
    COALESCE(A.measurename, B.measurename) AS measurename,
    A.runA_sum,
    B.runB_sum,
    CASE
        WHEN A.runA_sum IS NULL THEN 'Missing Run A'
        WHEN B.runB_sum IS NULL THEN 'Missing Run B'
        WHEN ABS(A.runA_sum - B.runB_sum) <= @eps THEN 'Match'
        ELSE 'Mismatch'
    END AS status
FROM A
FULL OUTER JOIN B
    ON A.mrv_id = B.mrv_id
   AND A.measurename = B.measurename
ORDER BY mrv_id, measurename;

WHERE
    A.runA_sum IS NULL
    OR B.runB_sum IS NULL
    OR ABS(A.runA_sum - B.runB_sum) > @eps
