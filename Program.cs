using SubmissionProcessor.Worker;
using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Data;
using SubmissionProcessor.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
?? throw new InvalidOperationException("connection String: 'Default connections not found'");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(
        connectionString
    ));

builder.Logging.AddLog4Net("log4net.config");

var host = builder.Build();
host.Run();