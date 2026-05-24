namespace MngKeeper.Application.Configuration;

public class DirectorySyncSettings
{
    /// <summary>Login başarılı olduktan sonra tek kullanıcı KC→Mongo sync.</summary>
    public bool LoginSyncEnabled { get; set; } = true;
}
