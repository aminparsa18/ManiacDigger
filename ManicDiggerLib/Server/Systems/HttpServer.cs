using System.Net;
using System.Text;

public class ServerSystemHttpServer : ServerSystem
{
    private bool started;
    internal FragLabs.HTTP.HttpServer httpServer;

    public override void Update(Server server, float dt)
    {
        if (!started)
        {
            started = true;

            int httpPort = server.Port + 1;
            if (server.config.EnableHTTPServer && (!server.IsSinglePlayer))
            {
                try
                {
                    httpServer = new FragLabs.HTTP.HttpServer(new IPEndPoint(IPAddress.Any, httpPort));
                    MainHttpModule m = new()
                    {
                        server = server,
                        system = this
                    };
                    httpServer.Install(m);
                    foreach (var module in server.httpModules)
                    {
                        httpServer.Install(module.module);
                    }
                    httpServer.Start();
                    Console.WriteLine(server.language.ServerHTTPServerStarted(), httpPort);
                }
                catch
                {
                    Console.WriteLine(server.language.ServerHTTPServerError(), httpPort);
                }
            }
        }
        for (int i = 0; i < server.httpModules.Count; i++)
        {
            ActiveHttpModule m = server.httpModules[i];
            if (httpServer != null)
            {
                if (!m.installed)
                {
                    m.installed = true;
                    httpServer.Install(m.module);
                }
            }
        }
    }

    public override void OnRestart(Server server)
    {
        foreach (ActiveHttpModule m in server.httpModules)
        {
            if (m.installed)
            {
                httpServer.Uninstall(m.module);
            }
        }
        server.httpModules.Clear();
    }
}

public class ActiveHttpModule
{
    public string name;
    public ManicDigger.Func<string> description;
    public FragLabs.HTTP.IHttpModule module;
    public bool installed;
}

internal class MainHttpModule : FragLabs.HTTP.IHttpModule
{
    public Server server;
    public ServerSystemHttpServer system;

    public void Installed(FragLabs.HTTP.HttpServer server)
    {
    }

    public void Uninstalled(FragLabs.HTTP.HttpServer server)
    {
    }

    public bool ResponsibleForRequest(FragLabs.HTTP.HttpRequest request)
    {
        if (request.Uri.AbsolutePath.Equals("/", StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public bool ProcessAsync(FragLabs.HTTP.ProcessRequestEventArgs args)
    {
        var sb = new StringBuilder("<html>");

        foreach (var m in server.httpModules.OrderBy(m => m.name))
            sb.Append($"<a href='{m.name}'>{m.name}</a> - {m.description()}");

        sb.Append("</html>");
        args.Response.Producer = new FragLabs.HTTP.BufferedProducer(sb.ToString());
        return false;
    }
}