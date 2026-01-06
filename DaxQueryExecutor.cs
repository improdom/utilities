/* SQL Server (T-SQL) version of the query in your screenshot */

SELECT
    ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS synthetic_id,
    rm.risk_measure,
    mrvg.measure_group,
    ac.asset_class,
    ac.asset_class_id,
    rt.risk_type_id AS risk_category_id,
    rt.risk_type    AS risk_category,
    rt.risk_type,
    rt.risk_type_id,

    CASE
        WHEN mrvg.source_type <> 'Position' THEN
            CASE
                WHEN mrvdef.agg_filter IS NULL
                     AND mfv.filter_value <> 'TO'
                THEN mfv.filter_value
                ELSE mrvdef.agg_filter
            END
        ELSE mrvdef.agg_filter
    END AS agg_filter,

    mrvdef.agg_point,
    mrvg.measure_group_original,
    mrvdef.definition_group_original,
    mdgf.filter_id,
    mrvg.source_type AS riskmeasure_source,
    mrvg.non_agg     AS nonagg,
    mdgf.grid_view,
    mmd.dimension_type,
    md_attr.dimension_name  AS dimension_name_onprem,
    md_attr.attribute_name  AS attribute_name_onprem,
    mbs.display_folder_name AS dimension_name,
    mbs.business_name       AS attribute_name,

    /* Postgres: split_part(mbs.display_folder_name, '\', 1) */
    LEFT(mbs.display_folder_name, CHARINDEX('\', mbs.display_folder_name + '\') - 1) AS table_name,

    mbs.is_highway,
    gf.agg_point_order,

    /* Postgres: COALESCE(mrvdef.drill_onerisk, 'Y'::bpchar) */
    ISNULL(mrvdef.drill_onerisk, 'Y') AS drill_onerisk,

    mrvdef.mrv_definition
FROM [cubiq].[mrv_measure_group] mrvg
JOIN [cubiq].[mrv_definition_group] mrvdef
    ON  mrvg.measure_group_id = mrvdef.measure_group_id
    AND mrvdef.is_active      = 'Y'
    AND mrvdef.feed_onerisk   = 'Y'
JOIN [cubiq].[mrv_asset_class] ac
    ON  mrvdef.asset_class_id = ac.asset_class_id
    AND ac.is_active          = 'Y'
JOIN [cubiq].[mrv_risk_type] rt
    ON  rt.risk_type_id = mrvdef.risk_type_id
    AND rt.is_active    = 'Y'
JOIN [cubiq].[mrv_risk_measure] rm
    ON  rm.risk_measure_id = mrvg.risk_measure_id
    AND rm.is_active       = 'Y'

LEFT JOIN [cubiq].[mrv_definition_group_filter] mdgf
    ON  mrvdef.definition_group_id = mdgf.definition_group_id
    AND mdgf.grid_view IS NOT NULL
LEFT JOIN [cubiq].[mrv_filter_definition] mfd
    ON  mdgf.filter_id = mfd.filter_id
    AND mfd.is_active  = 'Y'
LEFT JOIN [cubiq].[mrv_filter_value] mfv
    ON mfv.filter_id = mfd.filter_id

/* First md = attribute picked by mdgf.attribute_id */
LEFT JOIN [cubiq].[mrv_measure_dimension] md_attr
    ON  md_attr.attribute_id = mdgf.attribute_id
    AND md_attr.is_active    = 'Y'

LEFT JOIN [cubiq].[mrv_definition_group_filter] gf
    ON  mrvdef.definition_group_id = gf.definition_group_id
    AND gf.filter_dimension        = 'Dimension'

/* In your screenshot the alias md is reused; SQL Server won’t allow that. */
LEFT JOIN [cubiq].[mrv_measure_dimension] md_dim
    ON  gf.attribute_id         = md_dim.attribute_id
    AND ISNULL(md_dim.is_active, 'Y') = 'Y'

LEFT JOIN [mr_agg_model_metadata].[mr_cdm_attributes_base] strategic_mbs
    ON md_attr.dimension_name = strategic_mbs.dimension_name

LEFT JOIN [mr_agg_model_metadata].[mr_cdm_attributes_base] mbs
    ON  mbs.legacy_folder_name   = md_attr.dimension_name
    AND ISNULL(mbs.is_active,'Y') = 'Y'
    AND md_attr.attribute_name   = mbs.legacy_business_name
    AND mbs.databricks_view_name = 'pbi_fact_risk_results_aggregated_vw'
    AND mbs.hierarchy_ind        = 'N'
    AND mbs.is_visible           = 'Y'

WHERE mrvg.is_active = 'Y';
