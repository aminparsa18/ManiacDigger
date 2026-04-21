using System.Text;

namespace FragLabs.HTTP;

public interface IResponseProducer : IDisposable
{
    /// <summary>
    /// Gets if the producer has been connected.
    /// </summary>
    bool Connected { get; }

    /// <summary>
    /// Gets the http request.
    /// </summary>
    HttpRequest Request { get; }

    void Connect(HttpRequest request);
    void Disconnect();
    bool ReadAsync(ProducerEventArgs e);
    byte[] Read();
    void Dispose();
    Dictionary<string, string> AdditionalHeaders(HttpRequest request);

    /// <summary>
    /// Hook for modifying the http response before headers are sent.
    /// </summary>
    /// <param name="response"></param>
    void BeforeHeaders(HttpResponse response);
}

/// <summary>
/// An HTTP response producer.
/// </summary>
public abstract class ResponseProducer : IResponseProducer
{
    /// <summary>
    /// Gets if the producer has been connected.
    /// </summary>
    public bool Connected { get; private set; }

    /// <summary>
    /// Gets the http request.
    /// </summary>
    public HttpRequest Request { get; private set; }

    public ResponseProducer()
    {
        IsDisposed = false;
        Connected = false;
    }

    public virtual void Connect(HttpRequest request)
    {
        Connected = true;
        Request = request;
    }

    public virtual void Disconnect()
    {
        Connected = false;
    }

    public virtual bool ReadAsync(ProducerEventArgs e)
    {
        e.Buffer = Read();
        if (e.Buffer != null)
            e.ByteCount = e.Buffer.Length;
        else
            e.ByteCount = 0;
        return false;
    }

    public virtual byte[] Read()
    {
        return null;
    }

    public bool IsDisposed { get; private set; }
    public virtual void Dispose()
    {
        if (IsDisposed)
            throw new ObjectDisposedException("ResponseProducer");
        IsDisposed = true;
    }

    public virtual Dictionary<string, string> AdditionalHeaders(HttpRequest request)
    {
        return null;
    }

    /// <summary>
    /// Hook for modifying the http response before headers are sent.
    /// </summary>
    /// <param name="response"></param>
    public virtual void BeforeHeaders(HttpResponse response)
    {
    }
}

public class BufferedProducer : ResponseProducer
{
    private byte[] buffer;

    public BufferedProducer(string html) : this(html, Encoding.UTF8) { }
    public BufferedProducer(string html, Encoding encoding) : this(encoding.GetBytes(html)) { }

    public BufferedProducer(byte[] data)
    {
        buffer = data;
    }

    public override byte[] Read()
    {
        if (buffer == null)
            return null;
        var ret = buffer;
        buffer = null;
        return ret;
    }

    public override Dictionary<string, string> AdditionalHeaders(HttpRequest request)
    {
        var ret = new Dictionary<string, string>
        {
            { "Content-Length", buffer.Length.ToString() }
        };
        return ret;
    }
}

public class ProducerEventArgs : EventArgs
{
    public event EventHandler<ProducerEventArgs> Completed;
    public byte[] Buffer;
    public int ByteCount;
    public void Complete(object sender)
    {
        Completed?.Invoke(sender, this);
    }
}
