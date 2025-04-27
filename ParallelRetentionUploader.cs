using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

public class ParallelRetentionUploader
{
    private readonly RetentionDataAppender _dataAppender;

    public int MaxDegreeOfParallelism { get; set; } = 5;
    public Action<string> Logger { get; set; } = Console.WriteLine;

    public ParallelRetentionUploader(RetentionDataAppender dataAppender)
    {
        _dataAppender = dataAppender ?? throw new ArgumentNullException(nameof(dataAppender));
    }

    public async Task UploadDatasetsAsync(IEnumerable<(string Database, string Table, DataTable Data)> datasets)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        };

        await Parallel.ForEachAsync(datasets, options, async (dataset, cancellationToken) =>
        {
            try
            {
                Logger?.Invoke($"Starting upload for {dataset.Table}...");
                await _dataAppender.AppendDataAsync(dataset.Database, dataset.Table, dataset.Data);
                Logger?.Invoke($"Upload completed for {dataset.Table}.");
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error uploading {dataset.Table}: {ex.Message}");
                throw;
            }
        });
    }
}
