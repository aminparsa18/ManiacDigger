namespace FragLabs.HTTP;

/// <summary>
/// An HTTP response.
/// </summary>
public class HttpResponse
{
    /// <summary>
    /// Response producer used to produce data to send to the client.
    /// </summary>
    public IResponseProducer Producer { get; set; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public HttpStatusCode StatusCode = HttpStatusCode.OK;

    /// <summary>
    /// HTTP version.
    /// </summary>
    public Version HttpVersion = System.Net.HttpVersion.Version10;

    /// <summary>
    /// HTTP response headers.
    /// </summary>
    public Dictionary<string, string> Headers = [];

    /// <summary>
    /// HTTP keep-alive response.
    /// </summary>
    public HttpConnection Connection = HttpConnection.Close;

    /// <summary>
    /// Gets the HTTP version string. Ex: HTTP/1.0
    /// </summary>
    public string HttpVersionString { get { return $"HTTP/{HttpVersion.Major}.{HttpVersion.Minor}"; } }
}

public enum HttpConnection
{
    Close,
    KeepAlive
}
