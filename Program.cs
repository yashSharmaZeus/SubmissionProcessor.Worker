using SubmissionProcessor.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Logging.AddLog4Net("log4net.config");

var host = builder.Build();
host.Run();
