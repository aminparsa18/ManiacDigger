using System.Net;
using System.Net.Sockets;

namespace FragLabs.HTTP;

/// <summary>
/// HTTP server.
/// </summary>
public class HttpServer : IDisposable
{
    /// <summary>
    /// Endpoints configured to listen on.
    /// </summary>
    private List<IPEndPoint> _endpoints = [];

    /// <summary>
    /// List of endpoints the server managed to listen on.
    /// </summary>
    private List<IPEndPoint> _listeningEndpoints = [];

    /// <summary>
    /// List of sockets the server is listening for connections on.
    /// </summary>
    private List<Socket> _serverSockets = [];

    /// <summary>
    /// Installed http modules.
    /// </summary>
    private List<IHttpModule> _modules = [];

    /// <summary>
    /// Creates a new HttpServer instance.
    /// <param name="endpoints">Array of ip endpoints to listen for connections on.</param>
    /// </summary>
    public HttpServer(params IPEndPoint[] endpoints)
    {
        _endpoints = [.. endpoints];
        IsDisposed = false;
        IsRunning = false;
        //  default max request body size to 16MB
        MaxBodySize = 16777216;
    }

    /// <summary>
    /// Installs a module into the HTTP server.
    /// </summary>
    /// <param name="module"></param>
    public void Install(IHttpModule module)
    {
        lock (_modules)
        {
            if (!_modules.Contains(module))
            {
                _modules.Add(module);
                module.Installed(this);
            }
        }
    }

    /// <summary>
    /// Uninstalls a module from the HTTP server.
    /// </summary>
    /// <param name="module"></param>
    public void Uninstall(IHttpModule module)
    {
        lock (_modules)
        {
            if (!_modules.Contains(module))
            {
                return;
            }
            _modules.Remove(module);
            module.Uninstalled(this);
        }
    }

    /// <summary>
    /// Starts the HTTP server.
    /// </summary>
    public void Start()
    {
        Stop();

        IsRunning = false;
        _serverSockets = [];
        _listeningEndpoints = [];

        foreach (var endpoint in _endpoints)
        {
            try
            {
                var sock = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sock.Bind(endpoint);
                sock.Listen(50);

                _listeningEndpoints.Add(endpoint);
                _serverSockets.Add(sock);

                SockAccept(sock);
            }
            catch (Exception){}
        }

        if (_listeningEndpoints.Count > 0)
        {
            IsRunning = true;
        }
    }

    /// <summary>
    /// Stops the HTTP server.
    /// </summary>
    public void Stop()
    {
        if (IsRunning)
        {
            foreach (var socket in _serverSockets)
            {
                socket.Close();
            }
            _serverSockets = null;
            IsRunning = false;
        }
    }

    /// <summary>
    /// Gets whether the object has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }
    public void Dispose()
    {
        if (!IsDisposed)
        {
            Stop();
        }
        else
        {
            throw new ObjectDisposedException("HttpServer");
        }
    }

    /// <summary>
    /// Listen for an incoming connection on a socket.
    /// </summary>
    /// <param name="sock"></param>
    private void SockAccept(Socket sock)
    {
        var args = new SocketAsyncEventArgs();
        args.Completed += ProcessAccept;
        if (!sock.AcceptAsync(args))
        {
            ProcessAccept(sock, args);
        }
    }

    /// <summary>
    /// Async callback that processes new connections.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ProcessAccept(object? sender, SocketAsyncEventArgs args)
    {
        SockAccept((Socket)sender);

        if (args.SocketError == SocketError.Success)
        {
            var newClient = args.AcceptSocket;
            var reader = new HttpRequestReader(newClient, MaxBodySize);
            reader.ReadComplete += ProcessRequest;
            reader.HttpError += ProcessHttpError;
            reader.AsyncReadRequest();
        }
        args.Dispose();
    }

    private void ProcessHttpError(HttpStatusCode httpStatusCode, HttpRequest request)
    {
        var response = new HttpResponse
        {
            StatusCode = httpStatusCode,
            Producer = new BufferedProducer("An error occurred, please hang tight")
        };
        var writer = new HttpResponseWriter(request.ClientSocket);
        writer.AsyncWrite(request, response);
    }

    /// <summary>
    /// Async event handles HTTP requests received.
    /// </summary>
    /// <param name="request"></param>
    private void ProcessRequest(HttpRequest request)
    {
        var handledByModule = false;
        lock (_modules)
        {
            foreach (var module in _modules)
            {
                if (module.ResponsibleForRequest(request))
                {
                    handledByModule = true;
                    var args = new ProcessRequestEventArgs
                    {
                        Request = request,
                        Response = new HttpResponse()
                        {
                            HttpVersion = request.Version,
                            StatusCode = HttpStatusCode.OK,
                            Connection = HttpConnection.Close,
                            Headers = []
                        }
                    };
                    args.Completed += ModuleProcessComplete;
                    if (!module.ProcessAsync(args))
                        ModuleProcessComplete(module, args);
                }
            }
        }
        if (!handledByModule)
        {
            var response = new HttpResponse
            {
                StatusCode = HttpStatusCode.NotFound
            };
            response.Headers.Add("Content-Type", "text/plain");
            response.Producer = new BufferedProducer("404 - Not Found");
            var writer = new HttpResponseWriter(request.ClientSocket);
            writer.AsyncWrite(request, response);
        }
    }

    private void ModuleProcessComplete(object? sender, ProcessRequestEventArgs e)
    {
        var writer = new HttpResponseWriter(e.Request.ClientSocket);
        writer.AsyncWrite(e.Request, e.Response);
    }

    /// <summary>
    /// Gets if the server is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets or sets the maximum allowable size for a request body.
    /// </summary>
    public int MaxBodySize { get; set; }
}
