using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SubmissionProcessor.Worker.Models;

public class SubmissionFileMetaData
{

    public int Id { get; set; }

    [Required]
    public int SubmissionId { get; set; }

    [Required]
    public string OriginalFilName { get; set; } = null!;

    [Required]
    [Column(TypeName = "Varchar(50)")]
    public string GeneratedStorageName { get; set; } = null!;

    [Required]
    [Column(TypeName = "Varchar(20)")]
    public string ContentType { get; set; } = null!;

    [Required]
    public long Size { get; set; }

    [Required]
    public string Checksum { get; set; } = null!;

    [Required]
    public int UploadedBy { get; set; }

    [Required]
    public DateTime Timestamps { get; set; }

    private SubmissionFileMetaData() { }
}