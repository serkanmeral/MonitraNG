using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;
using Microsoft.Extensions.Configuration;

namespace MyOpcClient1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpcController : ControllerBase
    {
        private const string OpcUaServerUrl = "opc.tcp://localhost:50000"; // Sunucu URL'si
        private readonly IConfiguration _configuration;
        private static string _serverUrl;
        private static string _username;
        private static string _password;

        //private async Task<Session> CreateSessionAsync()
        //{
        //    // OPC-UA istemcisi için uygulama yapılandırması
        //    var application = new ApplicationInstance
        //    {
        //        ApplicationName = "OpcUaClientApi",
        //        ApplicationType = ApplicationType.Client,

        //    };

        //    // Konfigürasyonu yükleyin
        //    var config = await application.LoadApplicationConfiguration(false);
        //    await application.CheckApplicationInstanceCertificate(false, 0);

        //    config.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
        //    config.SecurityConfiguration.AddAppCertToTrustedStore = true;

        //    // Endpoint yapılandırması
        //    var endpoint = CoreClientUtils.SelectEndpoint(OpcUaServerUrl, useSecurity: false);
        //    var endpointConfiguration = EndpointConfiguration.Create(config);
        //    var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);

        //    // Session oluştur
        //    return await Session.Create(config, configuredEndpoint, false, "OpcUaClientSession", 60000, null, null);
        //}

        public OpcController(IConfiguration configuration)
        {

            _configuration = configuration;

            _serverUrl = _configuration.GetValue<string>("OpcUa:ServerUrl");
            _username = _configuration.GetValue<string>("OpcUa:Username");
            _password = _configuration.GetValue<string>("OpcUa:Password");
        }
        private async Task<Session> CreateSessionAsync()
        {
            try
            {

                // Uygulama tanımı
                var application = new ApplicationInstance
                {
                    ApplicationName = "MyOpcClient1",
                    ApplicationType = ApplicationType.Client,
                };

                // Manuel yapılandırma
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "MyOpcClient1",
                    ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:MyOpcClient1",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates",
                            SubjectName = "MyOpcClient1"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Trusted"
                        },
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = "Certificates/Rejected"
                        },
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas
                    {
                        OperationTimeout = 15000,
                        MaxStringLength = 1048576,
                        MaxByteStringLength = 1048576,
                        MaxArrayLength = 65535,
                        MaxMessageSize = 4194304,
                        MaxBufferSize = 65535,
                        ChannelLifetime = 600000,
                        SecurityTokenLifetime = 3600000
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000,
                        MinSubscriptionLifetime = 10000
                    }
                };

                // Konfigürasyonu doğrula
                await config.Validate(ApplicationType.Client);

                // Sertifika kontrolü
                var certificateIdentifier = config.SecurityConfiguration.ApplicationCertificate;
                var certificate = await certificateIdentifier.LoadPrivateKey(null);

                if (certificate == null)
                {
                    // Sertifika oluşturma
                    certificate = CertificateFactory.CreateCertificate(
                        storeType: certificateIdentifier.StoreType,
                        storePath: certificateIdentifier.StorePath,
                        password: null,
                        applicationUri: config.ApplicationUri,
                        applicationName: config.ApplicationName,
                        subjectName: config.ApplicationName,
                        domainNames: new List<string> { config.ApplicationUri },
                        keySize: 2048,                         // Sertifika boyutu (bit)
                        startTime: DateTime.UtcNow,            // Başlangıç tarihi
                        lifetimeInMonths: 12,                  // Geçerlilik süresi (ay olarak)
                        hashSizeInBits: 256,                   // Hash uzunluğu (bit)
                        isCA: false,                           // IsCA
                        issuerCAKeyCert: null,                 // IssuerCertificate
                        publicKey: null,                       // PublicKey
                        pathLengthConstraint: 0                // PathLengthConstraint
                    );

                    certificateIdentifier.Certificate = certificate;
                }

                // Sertifikayı yapılandırmaya ekle
                config.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;

                // Endpoint seçimi ve session oluşturma
                var endpoint = CoreClientUtils.SelectEndpoint("opc.tcp://localhost:50000", useSecurity: true);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);

                return await Session.Create(config, configuredEndpoint, false, "MyOpcClient1Session", 60000, new UserIdentity(_username, _password), null);
            }
            catch (Exception ex)
            {
                var aa = ex.Message;

                return null;
            }
        }

        [HttpPost("browse")]
        public async Task<IActionResult> BrowseNode([FromBody] BrowseRequest request)
        {
            try
            {
                using (var session = await CreateSessionAsync())
                {
                    // Belirtilen NodeId üzerinden Browse işlemi
                    var browseNodeId = string.IsNullOrEmpty(request.NodeId) ? ObjectIds.ObjectsFolder : new NodeId(request.NodeId);

                    var references = session.FetchReferences(browseNodeId);

                    var result = references.Select(reference => new
                    {
                        NodeId = reference.NodeId.ToString(),
                        DisplayName = reference.DisplayName.Text,
                        BrowseName = reference.BrowseName.Name,
                        NodeClass = reference.NodeClass.ToString()
                    });

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("browse")]
        public async Task<IActionResult> Browse()//(string nodeId = null)
        {
            try
            {
                using (var session = await CreateSessionAsync())
                {
                    // Node ID belirleme (null ise root node alınır)
                    NodeId browseNodeId = ObjectIds.ObjectsFolder;//string.IsNullOrEmpty(nodeId) ? ObjectIds.ObjectsFolder : new NodeId(nodeId);

                    // Browse işlemi
                    var references = session.FetchReferences(browseNodeId);



                    // Sonuçları işleme
                    var nodes = new List<object>();
                    foreach (var reference in references)
                    {
                        nodes.Add(new
                        {
                            NodeId = reference.NodeId.ToString(),
                            DisplayName = reference.DisplayName.Text,
                            BrowseName = reference.BrowseName.Name,
                            NodeClass = reference.NodeClass.ToString()
                        });
                    }

                    return Ok(nodes);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("read")]
        public async Task<IActionResult> ReadNodeValue([FromBody] ReadRequest request)
        {
            try
            {
                using (var session = await CreateSessionAsync())
                {
                    // Belirtilen AttributeId ile okumayı özelleştir
                    var readValueId = new ReadValueId
                    {
                        NodeId = new NodeId(request.NodeId),
                        AttributeId = Attributes.Value // Value yerine başka bir AttributeId denenebilir
                    };

                    // Değer oku
                    var results = session.Read(
                        null,
                        0,
                        TimestampsToReturn.Both,
                        new ReadValueIdCollection { readValueId },
                        out var values,
                        out var diagnosticInfos
                    );




                    //if (StatusCode.IsBad(results))
                    //{
                    //    return BadRequest(new { Error = $"Read failed with status: {results}" });
                    //}

                    return Ok(new
                    {
                        NodeId = request.NodeId,
                        Value = values[0].Value,
                        StatusCode = values[0].StatusCode,
                        SourceTimestamp = values[0].SourceTimestamp
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }


        [HttpPost("createFolder")]
        public async Task<IActionResult> CreateFolder([FromBody] FolderRequest request)
        {
            try
            {
                using (var session = await CreateSessionAsync())
                {
                    // Parent Node ID kontrolü
                    var parentNodeId = string.IsNullOrEmpty(request.ParentNodeId)
                        ? ObjectIds.ObjectsFolder
                        : new NodeId(request.ParentNodeId);

                    var namespaces = session.NamespaceUris.ToArray();

                    var serverCapabilities = session.ReadValue(ObjectIds.Server_ServerCapabilities);
                    Console.WriteLine("Server Capabilities: " + serverCapabilities.Value.ToString());

                    // Namespace doğrulama
                    var namespaceIndex = session.NamespaceUris.GetIndex(namespaces[0]);
                    if (namespaceIndex < 0)
                    {
                        return BadRequest(new { Error = "Namespace not found on server." });
                    }

                    // Yeni Node oluşturma
                    var folderNodeId = new NodeId(Guid.NewGuid().ToString(), (ushort)namespaceIndex);



                    var folderNode = new AddNodesItem
                    {
                        ParentNodeId = parentNodeId,
                        ReferenceTypeId = ReferenceTypeIds.Organizes,
                        RequestedNewNodeId = folderNodeId,
                        BrowseName = new QualifiedName(request.FolderName, (ushort)namespaceIndex),
                        NodeClass = NodeClass.Object,
                        TypeDefinition = ObjectTypeIds.FolderType
                    };

                    var nodesToAdd = new AddNodesItemCollection { folderNode };
                    var results = session.AddNodes(null, nodesToAdd, out var addResults, out var diagnosticInfos);

                    // Hata kontrolü
                    if (addResults == null || addResults.Count == 0 || Opc.Ua.StatusCode.IsBad(addResults[0].StatusCode))
                    {
                        return BadRequest(new
                        {
                            Error = "AddNodes failed",
                            StatusCode = addResults?[0].StatusCode.ToString() ?? "Unknown"
                        });
                    }

                    return Ok(new
                    {
                        Message = "Folder created successfully",
                        FolderNodeId = folderNodeId.ToString(),
                        FolderName = request.FolderName
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        //    [HttpPost("write")]
        //    public async Task<IActionResult> WriteNodeValue([FromBody] WriteRequest request)
        //    {
        //        try
        //        {
        //            using (var session = await CreateSessionAsync())
        //            {
        //                // Yazılacak Node ve değer
        //                var nodeToWrite = new WriteValue
        //                {
        //                    NodeId = new NodeId(request.NodeId),
        //                    AttributeId = Attributes.Value,
        //                    Value = new DataValue(new Variant(request.Value))
        //                };

        //                // Yazma işlemi
        //                var writeResults = session.Write(new WriteValueCollection { nodeToWrite }, out var diagnosticInfos);
        //                if (writeResults[0] != StatusCodes.Good)
        //                {
        //                    return BadRequest(new { Error = $"Write failed: {StatusCodes.GetStatusCodeString(writeResults[0])}" });
        //                }

        //                return Ok(new { NodeId = request.NodeId, WrittenValue = request.Value });
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            return BadRequest(new { Error = ex.Message });
        //        }
        //    }
        //}

        public class WriteRequest
        {
            public string NodeId { get; set; } // Yazılacak Node ID
            public object Value { get; set; }  // Yazılacak değer
        }
        public class ReadRequest
        {
            public string NodeId { get; set; } // Yazılacak Node ID

        }
        public class BrowseRequest
        {
            public string NodeId { get; set; } // Keşfedilecek Node ID
        }
        public class FolderRequest
        {
            public string ParentNodeId { get; set; } // Yeni klasörün ekleneceği üst node'un ID'si
            public string FolderName { get; set; }   // Yeni klasörün adı
        }
    }
}
