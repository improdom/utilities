Console.WriteLine(JsonSerializer.Serialize(new {
    result.Submitted, result.Succeeded, result.Failed, ms = result.Elapsed.TotalMilliseconds
}));
