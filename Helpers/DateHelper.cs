namespace SubmissionProcessor.Worker.Helpers;

public static class DateHelper
{
    public static DateTime Now() => new DateTime(
        DateTime.UtcNow.Year,
        DateTime.UtcNow.Month,
        DateTime.UtcNow.Day,
        DateTime.UtcNow.Hour,
        DateTime.UtcNow.Minute,
        DateTime.UtcNow.Second
    );
}
