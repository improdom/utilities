SELECT *
FROM mr_agg_model_refresh.pbi_query_readiness_status r
JOIN mr_agg_model_refresh.pbi_queries q ON r.query_id = q.id
JOIN mr_agg_model_refresh.pbi_query_spaces s ON q.query_space_id = s.id
LEFT JOIN mr_agg_model_refresh.pbi_query_execution_status qe ON q.id = qe.query_id
WHERE r.query_id = 56 AND r.cob_date = '2025-06-10';



COUNT(DISTINCT r.node_id) AS TotalNodes,
COUNT(DISTINCT r.node_id) FILTER (WHERE is_ready = false) AS PendingNodes,
COUNT(DISTINCT r.node_id) FILTER (WHERE is_ready = true) AS ReadyNodes
