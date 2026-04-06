public class QueueByte
{
    private readonly byte[] items;
    internal int start;
    internal int count;
    internal int max;

    public QueueByte(int capacity = 1024 * 1024 * 5)
    {
        max = capacity;
        items = new byte[max];
    }

    public int Count => count;

    public void Enqueue(byte value)
    {
        items[(start + count++) % max] = value;
    }

    public byte Dequeue()
    {
        byte ret = items[start];
        start = (start + 1) % max;
        count--;
        return ret;
    }

    public void DequeueRange(byte[] data, int length)
    {
        for (int i = 0; i < length; i++)
            data[i] = Dequeue();
    }

    public void PeekRange(byte[] data, int length)
    {
        for (int i = 0; i < length; i++)
            data[i] = items[(start + i) % max];
    }
}