public class LoginService
{
    internal LoginResult loginResult;

    public void Login(IGameService platform, string user, string password, string publicServerKey, string token, LoginResult result, LoginData resultLoginData_) => loginResult = result;
}
