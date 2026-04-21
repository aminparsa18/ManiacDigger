using System.Net;
// -----------------------------------------------------------------------------

public interface IHttpModule
{
    bool ResponsibleForRequest(HttpListenerRequest request);
    Task ProcessAsync(HttpListenerContext context);
}
