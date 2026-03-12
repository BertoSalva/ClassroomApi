using ExcelDataReader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Classroom.Infrastructure.Identity;

public sealed class ExcelAdmissionsValidator : IAdmissionsValidator
{
    private readonly AdmissionsOptions _options;
    private readonly IHostEnvironment _env;

    public ExcelAdmissionsValidator(IOptions<AdmissionsOptions> options, IHostEnvironment env)
    {
        _options = options.Value;
        _env = env;

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public Task<bool> IsValidAsync(string adminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminId)) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(_options.FilePath)) return Task.FromResult(false);

        var path = _options.FilePath;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(_env.ContentRootPath, path);

        if (!File.Exists(path)) return Task.FromResult(false);

        var needle = Normalize(adminId);

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var rowIndex = -1;
        var admissionCol = -1;

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            rowIndex++;

            if (admissionCol < 0)
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var header = (reader.GetValue(i)?.ToString() ?? string.Empty).Trim();
                    if (header.Equals("Admission No", StringComparison.OrdinalIgnoreCase) ||
                        header.Equals("AdmissionNo", StringComparison.OrdinalIgnoreCase))
                    {
                        admissionCol = i;
                        break;
                    }
                }

                continue;
            }

            var value = reader.GetValue(admissionCol)?.ToString();
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (Normalize(value) == needle)
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private static string Normalize(string value)
        => new string(value.Where(char.IsDigit).ToArray());
}