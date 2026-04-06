



public class ListConnectedPlayer
{
    public ListConnectedPlayer()
    {
        items = new ConnectedPlayer[1024];
        count = 0;
    }
    internal ConnectedPlayer[] items;
    internal int count;

    internal void Add(ConnectedPlayer connectedPlayer)
    {
        items[count++] = connectedPlayer;
    }

    internal void RemoveAt(int at)
    {
        for (int i = at; i < count - 1; i++)
        {
            items[i] = items[i + 1];
        }
        count--;
    }
}

public class QueueNetIncomingMessage
{
    public QueueNetIncomingMessage()
    {
        items = new NetIncomingMessage[1];
        itemsSize = 1;
        count = 0;
    }
    private NetIncomingMessage[] items;
    private int count;
    private int itemsSize;

    internal int Count()
    {
        return count;
    }

    internal NetIncomingMessage Dequeue()
    {
        NetIncomingMessage ret = items[0];
        for (int i = 0; i < count - 1; i++)
        {
            items[i] = items[i + 1];
        }
        count--;
        return ret;
    }

    internal void Enqueue(NetIncomingMessage p)
    {
        if (count == itemsSize)
        {
            NetIncomingMessage[] items2 = new NetIncomingMessage[itemsSize * 2];
            for (int i = 0; i < itemsSize; i++)
            {
                items2[i] = items[i];
            }
            itemsSize = itemsSize * 2;
            items = items2;
        }
        items[count++] = p;
    }
}

public class QueueByteArray
{
    public QueueByteArray()
    {
        items = new ByteArray[1];
        itemsSize = 1;
        count = 0;
    }
    private ByteArray[] items;
    private int count;
    private int itemsSize;

    internal int Count()
    {
        return count;
    }

    internal ByteArray Dequeue()
    {
        ByteArray ret = items[0];
        for (int i = 0; i < count - 1; i++)
        {
            items[i] = items[i + 1];
        }
        count--;
        return ret;
    }

    internal void Enqueue(ByteArray p)
    {
        if (count == itemsSize)
        {
            ByteArray[] items2 = new ByteArray[itemsSize * 2];
            for (int i = 0; i < itemsSize; i++)
            {
                items2[i] = items[i];
            }
            itemsSize = itemsSize * 2;
            items = items2;
        }
        items[count++] = p;
    }
}

public class QueueByte
{
    public QueueByte()
    {
        max = 1024 * 1024 * 5;
        items = new byte[max];
    }
    private readonly byte[] items;
    internal int start;
    internal int count;
    internal int max;

    public int GetCount()
    {
        return count;
    }

    public void Enqueue(byte value)
    {
        int pos = start + count;
        pos = pos % max;
        count++;
        items[pos] = value;
    }

    public byte Dequeue()
    {
        byte ret = items[start];
        start++;
        start = start % max;
        count--;
        return ret;
    }


    public void DequeueRange(byte[] data, int length)
    {
        for (int i = 0; i < length; i++)
        {
            data[i] = Dequeue();
        }
    }

    internal void PeekRange(byte[] data, int length)
    {
        for (int i = 0; i < length; i++)
        {
            data[i] = items[(start + i) % max];
        }
    }
}

public class QueueINetOutgoingMessage
{
    public QueueINetOutgoingMessage()
    {
        items = new INetOutgoingMessage[1];
        itemsSize = 1;
        count = 0;
    }
    private INetOutgoingMessage[] items;
    private int count;
    private int itemsSize;

    internal int Count()
    {
        return count;
    }

    internal INetOutgoingMessage Dequeue()
    {
        INetOutgoingMessage ret = items[0];
        for (int i = 0; i < count - 1; i++)
        {
            items[i] = items[i + 1];
        }
        count--;
        return ret;
    }

    internal void Enqueue(INetOutgoingMessage p)
    {
        if (count == itemsSize)
        {
            INetOutgoingMessage[] items2 = new INetOutgoingMessage[itemsSize * 2];
            for (int i = 0; i < itemsSize; i++)
            {
                items2[i] = items[i];
            }
            itemsSize = itemsSize * 2;
            items = items2;
        }
        items[count++] = p;
    }
}


public class FastQueueInt
{
    public void Initialize(int maxCount)
    {
        this.maxCount = maxCount;
        values = new int[maxCount];
        Count = 0;
        start = 0;
        end = 0;
    }
    private int maxCount;
    private int[] values;
    internal int Count;
    private int start;
    private int end;
    public void Push(int value)
    {
        values[end] = value;
        Count++;
        end++;
        if (end >= maxCount)
        {
            end = 0;
        }
    }
    public int Pop()
    {
        int value = values[start];
        Count--;
        start++;
        if (start >= maxCount)
        {
            start = 0;
        }
        return value;
    }
    public void Clear()
    {
        Count = 0;
    }
}



public class FastStackInt
{
    public void Initialize(int maxCount)
    {
        valuesLength = maxCount;
        values = new int[maxCount];
    }
    private int[] values;
    private int valuesLength;
    internal int count;
    public void Push(int value)
    {
        while (count >= valuesLength)
        {
            int[] values2 = new int[valuesLength * 2];
            for (int i = 0; i < valuesLength; i++)
            {
                values2[i] = values[i];
            }
            values = values2;
            valuesLength = valuesLength * 2;
        }
        values[count] = value;
        count++;
    }
    public int Pop()
    {
        count--;
        return values[count];
    }
    public void Clear()
    {
        count = 0;
    }

    internal int Count_()
    {
        return count;
    }
}
