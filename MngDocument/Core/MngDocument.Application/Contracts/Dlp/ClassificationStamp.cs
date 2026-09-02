namespace MngDocument.Application.Contracts.Dlp;

public sealed record ClassificationStamp(
    string ClassificationId,
    string ClassificationName,
    int Sensitivity,
    int SchemaVersion = 1);
