using System.Net;
using System.Text;

public class ServerSystemHttpServer : ServerSystem
{
    private HttpListener _listener;
    private CancellationTokenSource _cts;

    protected override void Initialize(Server server)
    {
        if (!server.config.EnableHTTPServer || server.IsSinglePlayer)
            return;

        int httpPort = server.Port + 1;
        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{httpPort}/");
            _listener.Start();

            _ = ListenAsync(server, _cts.Token);

            Console.WriteLine(server.language.ServerHTTPServerStarted(), httpPort);
        }
        catch
        {
            Console.WriteLine(server.language.ServerHTTPServerError(), httpPort);
        }
    }

    private async Task ListenAsync(Server server, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => RouteRequest(server, context), ct);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private async Task RouteRequest(Server server, HttpListenerContext context)
    {
        try
        {
            // check main module first, then installed modules
            var allModules = Enumerable
                .Repeat<IHttpModule>(new MainHttpModule { server = server }, 1)
                .Concat(server.httpModules.Select(m => m.module));

            var handler = allModules.FirstOrDefault(m => m.ResponsibleForRequest(context.Request));

            if (handler != null)
                await handler.ProcessAsync(context);
            else
                await WriteResponse(context, 404, "text/plain", "404 - Not Found");
        }
        catch (Exception ex)
        {
            await WriteResponse(context, 500, "text/plain", ex.Message);
        }
    }

    public override void OnRestart(Server server)
    {
        _cts?.Cancel();
        _listener?.Stop();
        server.httpModules.Clear();
    }

    // helper the modules can also use
    public static async Task WriteResponse(HttpListenerContext ctx, int statusCode, string contentType, string body)
    {
        var buffer = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = buffer.Length;
        await ctx.Response.OutputStream.WriteAsync(buffer);
        ctx.Response.OutputStream.Close();
    }
}

public class ActiveHttpModule
{
    public string name;
    public Func<string> description;
    public IHttpModule module;
    public bool installed;
}

// -----------------------------------------------------------------------------
internal class MainHttpModule : IHttpModule
{
    public Server server;

    public bool ResponsibleForRequest(HttpListenerRequest request) =>
        request.Url.AbsolutePath.Equals("/", StringComparison.OrdinalIgnoreCase);

    public async Task ProcessAsync(HttpListenerContext context)
    {
        var sb = new StringBuilder("<html><body>");

        foreach (var m in server.httpModules.OrderBy(m => m.name))
            sb.Append($"<a href='/{m.name}'>{m.name}</a> - {m.description()}<br/>");

        sb.Append("</body></html>");

        await ServerSystemHttpServer.WriteResponse(context, 200, "text/html", sb.ToString());
    }
}