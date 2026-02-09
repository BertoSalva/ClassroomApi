namespace Classroom.Infrastructure.Storage;

public sealed class LocalFileStorageOptions
{
    public string RootPath { get; set; } = "App_Data/uploads";
    public long MaxPdfBytes { get; set; } = 25 * 1024 * 1024; // 25MB default
}
