public class Expiry
{
    public static Expiry Create(float p)
    {
        Expiry expires = new()
        {
            TotalTime = p,
            TimeLeft = p
        };
        return expires;
    }

    public float TotalTime { get; set; }
    public float TimeLeft { get; set; }
}
