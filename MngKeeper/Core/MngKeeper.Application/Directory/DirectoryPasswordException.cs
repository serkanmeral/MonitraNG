namespace MngKeeper.Application.Directory;

public sealed class DirectoryPasswordException : Exception
{
    public DirectoryPasswordException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
