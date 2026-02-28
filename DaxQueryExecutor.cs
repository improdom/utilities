DECLARE @runA INT = 8544;
DECLARE @runB INT = 8640;
DECLARE @eps  FLOAT = 0.00001;   -- tolerance

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
),
R AS (
    SELECT
        COALESCE(A.mrv_id, B.mrv_id) AS mrv_id,
        COALESCE(A.measurename, B.measurename) AS measurename,
        CASE
            WHEN A.runA_sum IS NULL THEN 'Mismatch'
            WHEN B.runB_sum IS NULL THEN 'Mismatch'
            WHEN ABS(A.runA_sum - B.runB_sum) <= @eps THEN 'Match'
            ELSE 'Mismatch'
        END AS status
    FROM A
    FULL OUTER JOIN B
        ON A.mrv_id = B.mrv_id
       AND A.measurename = B.measurename
)
SELECT
    COUNT(*) AS total_mrvs,
    SUM(CASE WHEN status = 'Match' THEN 1 ELSE 0 END) AS match_cnt,
    SUM(CASE WHEN status = 'Mismatch' THEN 1 ELSE 0 END) AS mismatch_cnt,
    100.0 * SUM(CASE WHEN status = 'Match' THEN 1 ELSE 0 END) / COUNT(*) AS match_pct,
    100.0 * SUM(CASE WHEN status = 'Mismatch' THEN 1 ELSE 0 END) / COUNT(*) AS mismatch_pct
FROM R;
