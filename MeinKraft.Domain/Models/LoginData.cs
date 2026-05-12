public class LoginData
{
    public string ServerAddress { get; set; }
    public int Port { get; set; }
    public string AuthCode { get; set; } //Md5(private server key + player name)
    public string Token { get; set; }

    public bool PasswordCorrect { get; set; }
    public bool ServerCorrect { get; set; }
}