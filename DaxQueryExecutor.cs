WITH MRV AS
(
    SELECT
        md_mrv_name,
        definition_group_original,
        agg_point,
        ROW_NUMBER() OVER
        (
            PARTITION BY md_mrv_name
            ORDER BY definition_group_original
        ) AS rn
    FROM E
)
SELECT
    md_mrv_name,
    CASE
        WHEN MAX(CASE WHEN agg_point = 'Total' THEN 1 ELSE 0 END) = 1
        THEN STRING_AGG(
                CASE
                    WHEN agg_point = 'Total'
                    THEN definition_group_original
                END,
                ', '
             )
        ELSE MAX(
                CASE
                    WHEN rn = 1
                    THEN definition_group_original
                END
             )
    END AS definition_groups
FROM MRV
GROUP BY md_mrv_name
ORDER BY md_mrv_name;
