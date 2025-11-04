namespace MngDataGateway.Application.Configuration;

public class MongoDbOptions
{
    public const string SectionName = "MongoDB";
    
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "mngdatagateway";
}

