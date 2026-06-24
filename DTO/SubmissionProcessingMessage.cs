namespace SubmissionProcessor.Worker.DTO;

public class SubmissionProcessingMessage
{
    public string MessageId { get; set; } = null!;

    public string CorrelationId { get; set; } = null!;

    public int SubmissionId { get; set; }

    public int FileId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int ContractVersion { get; set; }
}