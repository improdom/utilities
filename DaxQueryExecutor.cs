{
  "operations": [
    {
      "update": {
        "object": {
          "database": "YourDatabaseName",
          "table": "YourDimensionTable",
          "partition": "DimPartition_01"
        },
        "property": "source",
        "value": {
          "query": "SELECT * FROM DimensionTable WHERE id = 2"
        }
      }
    },
    {
      "refresh": {
        "type": "dataOnly",
        "objects": [
          {
            "database": "YourDatabaseName",
            "table": "YourDimensionTable",
            "partition": "DimPartition_01"
          }
          // Add other dimension partitions here if needed
        ]
      }
    },
    {
      "refresh": {
        "type": "calculate",
        "objects": [
          {
            "database": "YourDatabaseName",
            "table": "YourDimensionTable"
          }
        ]
      }
    },
    {
      "update": {
        "object": {
          "database": "YourDatabaseName",
          "table": "YourFactTable",
          "partition": "FactPartition_01"
        },
        "property": "source",
        "value": {
          "query": "SELECT * FROM FactTable WHERE load_id = 123"
        }
      }
    },
    {
      "refresh": {
        "type": "dataOnly",
        "objects": [
          {
            "database": "YourDatabaseName",
            "table": "YourFactTable",
            "partition": "FactPartition_01"
          }
          // Add other fact partitions here if needed
        ]
      }
    }
  ]
}
