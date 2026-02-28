DECLARE @runA INT = 8544;
DECLARE @runB INT = 8640;
DECLARE @eps  FLOAT = 0.0000001;

WITH A AS (
    SELECT mrv_id, measurename, level_value, CAST(measurevalue AS FLOAT) AS value_a
    FROM [cubiq].[st_mrv_recon_target_data]
    WHERE benchmark_run_id = @runA
),
B AS (
    SELECT mrv_id, measurename, level_value, CAST(measurevalue AS FLOAT) AS value_b
    FROM [cubiq].[st_mrv_recon_target_data]
    WHERE benchmark_run_id = @runB
),
J AS (
    SELECT
        COALESCE(A.mrv_id, B.mrv_id) AS mrv_id,
        COALESCE(A.measurename, B.measurename) AS measurename,
        COALESCE(A.level_value, B.level_value) AS level_value,
        A.value_a,
        B.value_b,
        CASE
            WHEN A.mrv_id IS NULL THEN 'MissingA'
            WHEN B.mrv_id IS NULL THEN 'MissingB'
            WHEN ABS(A.value_a - B.value_b) <= @eps THEN 'Match'
            ELSE 'Mismatch'
        END AS status
    FROM A
    FULL OUTER JOIN B
      ON A.mrv_id = B.mrv_id
     AND A.measurename = B.measurename
     AND A.level_value = B.level_value
)
SELECT
    mrv_id,
    measurename,
    SUM(CASE WHEN status = 'Match'    THEN 1 ELSE 0 END) AS match_cnt,
    SUM(CASE WHEN status = 'Mismatch' THEN 1 ELSE 0 END) AS mismatch_cnt,
    SUM(CASE WHEN status = 'MissingA' THEN 1 ELSE 0 END) AS missing_in_a_cnt,
    SUM(CASE WHEN status = 'MissingB' THEN 1 ELSE 0 END) AS missing_in_b_cnt
FROM J
GROUP BY mrv_id, measurename
HAVING
    SUM(CASE WHEN status <> 'Match' THEN 1 ELSE 0 END) > 0  -- only show issues
ORDER BY mismatch_cnt DESC, missing_in_a_cnt DESC, missing_in_b_cnt DESC;
