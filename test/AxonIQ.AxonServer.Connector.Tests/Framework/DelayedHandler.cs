namespace AxonIQ.AxonServer.Connector.Tests.Framework;

public class DelayedHandler : DelegatingHandler
{
    public TimeSpan RequestDelay { get; set; } = TimeSpan.Zero;
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (RequestDelay != TimeSpan.Zero)
        {
            await Task.Delay(RequestDelay, cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        
        if (ResponseDelay != TimeSpan.Zero)
        {
            await Task.Delay(ResponseDelay, cancellationToken);
        }

        return response;
    }
}