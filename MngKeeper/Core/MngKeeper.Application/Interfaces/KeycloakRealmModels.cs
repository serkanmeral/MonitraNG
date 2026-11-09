namespace MngKeeper.Application.Interfaces;

public class KeycloakRealmUserSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Dolu ise kullanıcı LDAP/AD federation kaynağından gelir.</summary>
    public string? FederationLink { get; set; }
    /// <summary>AD title. Boşsa mevcut uygulama değeri korunur.</summary>
    public string? Title { get; set; }
    /// <summary>AD department. Boşsa mevcut uygulama değeri korunur.</summary>
    public string? Department { get; set; }
}

public class KeycloakUserPhotoData
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/jpeg";
    public string Extension { get; set; } = ".jpg";
}

public class KeycloakRealmGroupSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
}
