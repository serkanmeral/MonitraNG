namespace MngDocument.Application.Configuration;

public class MngDocumentSettings
{
    public const string SectionName = "MngDocumentSettings";

    public ServerSettings Server { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public DataGatewaySettings DataGateway { get; set; } = new();
    public ResourceSettings Resources { get; set; } = new();
    public DocumentRenderingSettings DocumentRendering { get; set; } = new();
    public CollaboraSettings Collabora { get; set; } = new();
    public WopiSettings Wopi { get; set; } = new();
    public KeeperSettings Keeper { get; set; } = new();
    public DomainFooterProfileSettings FooterProfile { get; set; } = new();
    /// <summary>When true and footerBlocks empty, applies legacy Odak FooterInjector + FooterProfile.</summary>
    public bool LegacyOdakFooterEnabled { get; set; } = true;
    public DocumentGenerationSettings DocumentGeneration { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5095;
    public string Scheme { get; set; } = "http";
}

public class DataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";
}

public class ResourceSettings
{
    /// <summary>Markdown içeriği için izin verilen maksimum karakter (Mongo'da text alanı).</summary>
    public int MaxMarkdownContentLength { get; set; } = 1_000_000;

    /// <summary>Tek seferde dönen ağaç/liste için üst sınır (DG sayfalama limiti).</summary>
    public int MaxTreeNodes { get; set; } = 5000;
}
