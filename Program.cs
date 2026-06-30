using SubmissionProcessor.Worker;
using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Data;
using SubmissionProcessor.Worker.Services;
using Polly;
using Polly.Fallback;
using System.Net;
using Microsoft.Extensions.Http.Resilience;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ICallerService,CallerService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
?? throw new InvalidOperationException("connection String: 'Default connections not found'");


var ApiSettings = builder.Configuration.GetSection("ApiSettings");
builder.Services.AddHttpClient("TraineeManagementClient", client =>
{
    client.BaseAddress = new Uri(ApiSettings["TrainingDirectoryBaseUrl"]!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(int.Parse(ApiSettings["Timeout"]!));
}).AddResilienceHandler("CustomStandardPipeline", pipelineBuilder =>
    {
        pipelineBuilder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = args => ValueTask.FromResult(
        args.Outcome.Result?.StatusCode == HttpStatusCode.InternalServerError ||
        args.Outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable ||
        args.Outcome.Exception is HttpRequestException),

            FallbackAction = _ => ValueTask.FromResult(Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"Fallback\",\"data\":\"Service Unavailable\"}", System.Text.Encoding.UTF8, "application/json")
            }))
        });

        pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = args =>
            {
                var res = args.Outcome.Result;
                var method = res?.RequestMessage?.Method;

                if (res?.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return ValueTask.FromResult(false);

                bool isSafeAndTransient = (method == HttpMethod.Get || method == HttpMethod.Put) &&
                       (res?.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout
                        || args.Outcome.Exception is HttpRequestException);

                return ValueTask.FromResult(isSafeAndTransient);
            }
        });

        pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 4,
            BreakDuration = TimeSpan.FromSeconds(15)
        });

        pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(int.Parse(ApiSettings["Timeout"]!)));
    });
    
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(
        connectionString
    ));

builder.Logging.AddLog4Net("log4net.config");

var host = builder.Build();
host.Run();