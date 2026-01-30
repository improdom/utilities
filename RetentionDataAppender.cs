version: "1.0"

reconciliation_run:
  # Point to the set by stable id (preferred) or by name if you must
  set_ref:
    id: "arc-risk-daily"
    # Optional: pin the exact version of the definition for reproducibility.
    # This can be a git commit SHA, tag, or a semantic version you store.
    definition_version: "git:4b1c9b7"

  run_id: "arc-risk-daily-2026-01-30T09-15-00Z"   # optional; engine can generate
  requested_by: "julio.diaz"
  requested_at_utc: "2026-01-30T09:15:00Z"

  # Parameter values supplied at execution time
  parameters:
    cob_date: "2026-01-30"
    book_id: "EQD"
    top_n: 50

  # Optional: choose which items to run
  selection:
    mode: "include"        # include | exclude | all
    items:
      - "SSAS vs Power BI - Total VaR"
      - "API vs Power BI - Risk Results Rowset"

  # Optional: runtime overrides (do NOT change the set definition)
  overrides:
    execution:
      timeout_seconds: 1800
    compare:
      null_equals_null: true

  # Optional: output control for this run
  output:
    sink: "delta"
    delta_table: "main.recon.reconciliation_results"
    write_mode: "append"
    partition_by:
      - "run_id"
