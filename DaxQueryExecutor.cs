{
  "sequence": {
    "operations": [
      {
        "createOrReplace": {
          "object": {
            "database": "ARC_Risk_Model",
            "table": "pbi_queryspace_trade",
            "partition": "Trade_2025_07_12"
          },
          "partition": {
            "name": "Trade_2025_07_12",
            "mode": "DirectQuery",
            "source": {
              "type": "m",
              "expression": "let\n    Source = Databricks.Contents(\"https://<your-databricks-instance>\")\n    in\n        Source"
            }
          }
        }
      },
      {
        "refresh": {
          "type": "full",
          "objects": [
            {
              "database": "ARC_Risk_Model",
              "table": "pbi_queryspace_trade",
              "partition": "Trade_2025_07_12"
            }
          ]
        }
      }
    ]
  }
}
