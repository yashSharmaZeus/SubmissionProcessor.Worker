namespace SubmissionProcessor.Worker.Services;

public interface ICallerService
{
    Task<HttpResponseMessage> GetById(string id);
}