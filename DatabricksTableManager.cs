
public byte[] SerializeDefinition(ItemDefinition pbiDefinition)
{
    ArgumentNullException.ThrowIfNull(pbiDefinition);

    using var zipStream = new MemoryStream();

    // The ZIP archive must be disposed before calling ToArray()
    // so the central directory and other ZIP metadata are written.
    using (var archive = new ZipArchive(
               zipStream,
               ZipArchiveMode.Create,
               leaveOpen: true))
    {
        foreach (var part in pbiDefinition.Parts ?? [])
        {
            if (string.IsNullOrWhiteSpace(part.Path))
            {
                throw new InvalidOperationException(
                    "An ItemDefinitionPart contains an empty path.");
            }

            // ZIP paths conventionally use forward slashes.
            var entryPath = part.Path
                .Replace('\\', '/')
                .TrimStart('/');

            if (string.IsNullOrWhiteSpace(entryPath))
            {
                throw new InvalidOperationException(
                    $"The path '{part.Path}' is not a valid ZIP entry path.");
            }

            var entry = archive.CreateEntry(
                entryPath,
                CompressionLevel.Optimal);

            using var entryStream = entry.Open();

            var content = part.BinaryPayload ?? Array.Empty<byte>();
            entryStream.Write(content, 0, content.Length);
        }
    }

    return zipStream.ToArray();
}





VAR Temp1 =
    MINX (
        CALCULATETABLE (
            VALUES ( 'Scenario'[Stress Magnitude] ),
            REMOVEFILTERS ( 'Scenario'[Scenario Name] ),
            REMOVEFILTERS ( 'Scenario'[Stress Magnitude] ),
            'Scenario'[Stress Magnitude Value] >= -0.15,
            'Scenario'[Stress Magnitude Value] <= 0.15
        ),
        CALCULATE ( [Structured Product Delta Family] )
    )
RETURN
    IF (
        ISBLANK ( Temp1 ),
        BLANK (),
        MIN ( 0, Temp1 )
    )
