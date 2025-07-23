var databricksClient = DatabricksClient.CreateClient(new Uri("https://<your-instance>.azuredatabricks.net"), "<your-token>");
var service = new DatabricksWarehouseService(databricksClient);

string? warehouseId = await service.GetWarehouseIdByNameAsync("My Warehouse");

if (warehouseId != null)
{
    Console.WriteLine($"Warehouse ID: {warehouseId}");
}
else
{
    Console.WriteLine("Warehouse not found.");
}
