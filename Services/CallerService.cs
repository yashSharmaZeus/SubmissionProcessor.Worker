using System.Net;
using Polly.CircuitBreaker;

namespace SubmissionProcessor.Worker.Services;

public class CallerService : ICallerService
{
    public const string clientName = "TraineeManagementClient";
    private readonly IHttpClientFactory _httpClientFactory;

    public CallerService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    public async Task<HttpResponseMessage> GetById(string id)
    {
        HttpClient client = _httpClientFactory.CreateClient(clientName);
        
        try
        {
            HttpResponseMessage response = await client.GetAsync($"/api/TrainingDirectory/{id}");
            return response;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                Content = new StringContent("The remote server took too long to respond.")
            };
        }
        catch (BrokenCircuitException ex)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent($"Circuit broken : {ex.Message}")
            };
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode ?? HttpStatusCode.InternalServerError;
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent($"Network or server error: {ex.Message}")
            };
        }

    }
}