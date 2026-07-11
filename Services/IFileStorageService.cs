namespace SubmissionProcessor.Worker.Services;

public interface IFileStorageService
{
    string getFullPath(string fileName);
    Task<FileStream> OpenReadAsync(string fileName);
    Task<bool> ExistsAsync(string fileName);
}