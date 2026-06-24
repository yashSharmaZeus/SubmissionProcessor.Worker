using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Models;

namespace SubmissionProcessor.Worker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ProcessingJobStatus> ProcessingJobStatus { get; set; }
    public DbSet<SubmissionFileMetaData> SubmissionFileMetaData { get; set; }
    public DbSet<ProcessingJob> ProcessingJob { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessingJobStatus>().HasData(
            new ProcessingJobStatus { Id = 1, StatusId = 0, Status = "Queued" },
            new ProcessingJobStatus { Id = 2, StatusId = 1, Status = "Processing" },
            new ProcessingJobStatus { Id = 3, StatusId = 2, Status = "Completed" },
            new ProcessingJobStatus { Id = 4, StatusId = 3, Status = "Failed" }
        );
    }
}