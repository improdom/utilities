version: "1.0"

reconciliation_set:
  name: "ARC Risk - Daily Reconciliation"
  description: "Daily validations across SSAS (MDX), Power BI (DAX), Delta (SQL), and REST APIs."

  # ---------------------------------------------------------------------------
  # Shared catalog of connections used by items in this set
  # ---------------------------------------------------------------------------
  connections:
    ssas_onprem_mrv:
      provider: "ssas"
      server: "ssas-prod.company.net"
      database: "MRV_Tabular"
      auth:
        mode: "kerberos"               # kerberos | username_password
        username: "${secrets/recon/SSAS_USER}"
        password: "${secrets/recon/SSAS_PASSWORD}"

    powerbi_arc_node1:
      provider: "powerbi"
      workspace: "ARC Workspace"
      dataset: "ARC Risk Model - Node1"
      xmla_endpoint: "${secrets/recon/PBI_XMLA_ENDPOINT}"
      auth:
        mode: "service_principal"
        tenant_id: "${secrets/recon/PBI_TENANT_ID}"
        client_id: "${secrets/recon/PBI_CLIENT_ID}"
        client_secret: "${secrets/recon/PBI_CLIENT_SECRET}"
      options:
        readonly: true

    databricks_uc_prod:
      provider: "databricks"
      warehouse_id: "${secrets/recon/DATABRICKS_WAREHOUSE_ID}"
      http_path: "${secrets/recon/DATABRICKS_HTTP_PATH}"
      catalog: "main"
      schema: "risk"

    risk_results_api:
      provider: "rest"
      base_url: "https://api.company.net/risk"
      auth:
        type: "oauth2_client_credentials"
        token_url: "https://login.microsoftonline.com/${secrets/recon/TENANT_ID}/oauth2/v2.0/token"
        client_id: "${secrets/recon/API_CLIENT_ID}"
        client_secret: "${secrets/recon/API_CLIENT_SECRET}"
        scope: "api://risk.read/.default"
      headers:
        Accept: "application/json"

  # ---------------------------------------------------------------------------
  # Optional defaults applied to all items (items can override)
  # ---------------------------------------------------------------------------
  defaults:
    execution:
      timeout_seconds: 900
      max_rows: null
      fetch_size: 50000
    compare:
      mode: "rowset"                   # rowset | aggregates
      null_equals_null: true
      ignore_columns: []
      numeric_tolerance:
        enabled: false
        absolute: 0.0
        relative: 0.0

  # ---------------------------------------------------------------------------
  # Parameters usable by items; placeholders use @param_name in all query types.
  # Your engine maps these into MDX/DAX/SQL/REST specifics at runtime.
  # ---------------------------------------------------------------------------
  parameters:
    cob_date:
      type: "date"
      required: true
      description: "Close-of-business date"
    book_id:
      type: "string"
      required: false
      default: null
    top_n:
      type: "int"
      required: false
      default: 100

  # ---------------------------------------------------------------------------
  # Reconciliation Items (each compares a Source and a Target)
  # ---------------------------------------------------------------------------
  reconciliation_items:

    - name: "SSAS vs Power BI - Total VaR"
      description: "Compare aggregated VaR between SSAS cube (MDX) and PBI model (DAX)."

      source:
        connection_ref: "ssas_onprem_mrv"
        language: "mdx"
        statement: |
          WITH
            MEMBER [Measures].[VarTotal] AS [Measures].[VaR]
          SELECT
            { [Measures].[VarTotal] } ON COLUMNS
          FROM [MRV Cube]
          WHERE ( [Date].[COB Date].&[@cob_date] )

      target:
        connection_ref: "powerbi_arc_node1"
        language: "dax"
        statement: |
          EVALUATE
          ROW(
            "VarTotal",
            CALCULATE(
              [VaR],
              TREATAS( { DATEVALUE("@cob_date") }, 'Date'[COB Date] )
            )
          )

      compare:
        mode: "aggregates"
        numeric_tolerance:
          enabled: true
          absolute: 0.01
          relative: 0.0001

    - name: "API vs Power BI - Risk Results Rowset"
      description: "Compare REST API risk results against PBI semantic model output."
      execution:
        timeout_seconds: 1800
        max_rows: 1000000

      source:
        connection_ref: "risk_results_api"
        language: "rest"
        request:
          method: "GET"
          path: "/v1/risk-results"
          query_params:
            cobDate: "@cob_date"
            bookId: "@book_id"
          pagination:
            type: "cursor"            # none | page | cursor
            cursor_field: "nextToken"
            max_pages: 200
        response:
          format: "json"
          root_path: "$.data"
          fields:
            cob_date: "$.cobDate"
            book_id: "$.bookId"
            position_id: "$.positionId"
            risk_measure_id: "$.riskMeasureId"
            value: "$.value"

      target:
        connection_ref: "powerbi_arc_node1"
        language: "dax"
        statement: |
          EVALUATE
          CALCULATETABLE(
            SUMMARIZECOLUMNS(
              'Date'[COB Date],
              'Book'[Book Id],
              'Position'[Position Id],
              'Risk Measure'[Risk Measure Id],
              "Value", [Risk Value]
            ),
            TREATAS( { DATEVALUE("@cob_date") }, 'Date'[COB Date] ),
            IF(
              "@book_id" = "" || "@book_id" = "null",
              TRUE(),
              TREATAS( { "@book_id" }, 'Book'[Book Id] )
            )
          )

      compare:
        mode: "rowset"
        key_columns:
          - "COB Date"
          - "Book Id"
          - "Position Id"
          - "Risk Measure Id"
        numeric_tolerance:
          enabled: true
          absolute: 0.0001
          relative: 0.000001

    - name: "Delta vs Power BI - Risk Results Rowset"
      description: "Compare Delta table (SQL) against PBI semantic output for the same keys."

      source:
        connection_ref: "databricks_uc_prod"
        language: "sql"
        statement: |
          SELECT
            cob_date,
            book_id,
            position_id,
            risk_measure_id,
            value
          FROM main.risk.delta_risk_results
          WHERE cob_date = DATE(@cob_date)
            AND ( @book_id IS NULL OR book_id = @book_id )

      target:
        connection_ref: "powerbi_arc_node1"
        language: "dax"
        statement: |
          EVALUATE
          CALCULATETABLE(
            SUMMARIZECOLUMNS(
              'Date'[COB Date],
              'Book'[Book Id],
              'Position'[Position Id],
              'Risk Measure'[Risk Measure Id],
              "Value", [Risk Value]
            ),
            TREATAS( { DATEVALUE("@cob_date") }, 'Date'[COB Date] ),
            IF(
              "@book_id" = "" || "@book_id" = "null",
              TRUE(),
              TREATAS( { "@book_id" }, 'Book'[Book Id] )
            )
          )

      compare:
        mode: "rowset"
        key_columns:
          - "COB Date"
          - "Book Id"
          - "Position Id"
          - "Risk Measure Id"
        numeric_tolerance:
          enabled: true
          absolute: 0.0001
          relative: 0.000001
