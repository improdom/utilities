DECLARE @runA INT = 8544;
DECLARE @runB INT = 8640;
DECLARE @eps  FLOAT = 0.0000001;   -- tolerance

WITH S AS (
    SELECT
        benchmark_run_id,
        mrv_id,
        measurename,
        SUM(CAST(measurevalue AS FLOAT)) AS total_value
    FROM [cubiq].[st_mrv_recon_target_data]
    WHERE benchmark_run_id IN (@runA, @runB)
    GROUP BY benchmark_run_id, mrv_id, measurename
)
SELECT
    COALESCE(A.mrv_id, B.mrv_id) AS mrv_id,
    COALESCE(A.measurename, B.measurename) AS measurename,
    A.total_value AS runA_sum,
    B.total_value AS runB_sum,
    CASE
        WHEN A.total_value IS NULL THEN 'Missing Run A'
        WHEN B.total_value IS NULL THEN 'Missing Run B'
        WHEN ABS(A.total_value - B.total_value) <= @eps THEN 'Match'
        ELSE 'Mismatch'
    END AS status
FROM S A
FULL OUTER JOIN S B
    ON A.mrv_id = B.mrv_id
   AND A.measurename = B.measurename
   AND A.benchmark_run_id = @runA
   AND B.benchmark_run_id = @runB
ORDER BY mrv_id, measurename;


WHERE
    A.total_value IS NULL
    OR B.total_value IS NULL
    OR ABS(A.total_value - B.total_value) > @eps
