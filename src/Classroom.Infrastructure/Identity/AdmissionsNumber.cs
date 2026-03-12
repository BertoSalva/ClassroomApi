using System.ComponentModel.DataAnnotations;

namespace Classroom.Infrastructure.Identity;

public sealed class AdmissionsNumber
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string AdminId { get; set; } = string.Empty;
}