using SubmissionProcessor.Worker.Data;

namespace SubmissionProcessor.Worker.Services;

public class FileStorageService : IFileStorageService
{
    public readonly string _storageRoot;
    private readonly AppDbContext _context;
    public readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger, AppDbContext context)
    {
        _storageRoot = configuration["FileStorageSettings: StorageRoot"] ?? "./Storage";
        Directory.CreateDirectory(_storageRoot);
        _logger = logger;
        _context = context;
    }

    public string getFullPath(string fileName)
    {
        _logger.LogInformation(Path.Combine(_storageRoot, fileName));
        return Path.Combine(_storageRoot, fileName);
    }

    public Task<FileStream> OpenReadAsync(string fileName)
    {
        string fullPath = getFullPath(fileName);
        if (!File.Exists(fullPath))
        {
            _logger.LogInformation("Requested File does not exists: {}", fileName);
            throw new FileNotFoundException("Requested File does not exists");
        }
        FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string fileName)
    {
        string fullPath = getFullPath(fileName);
        return Task.FromResult(File.Exists(fullPath));
    }


}