using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SubmissionProcessor.Worker.Enums;

namespace SubmissionProcessor.Worker.Models;

public class ProcessingJob
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "varchar(10)")]
    public GlobalEnums.ProcessingJobStatus Status { get; set; }

    [Required]
    public int Attempts { get; set; }

    [Required]
    public string ErrorSummary { get; set; } = null!;

    [Required]
    public DateTime StartedTime { get; set; }

    [Required]
    public DateTime CompletedTime { get; set; }

    [Required]
    public string CorrelationId { get; set; } = null!;  
}