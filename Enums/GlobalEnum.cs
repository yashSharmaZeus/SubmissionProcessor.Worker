namespace SubmissionProcessor.Worker.Enums;

public class GlobalEnums
{
    public enum ProcessingJobStatus
    {
        Queued = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }
}