using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Opc.Ua.Security;
using Opc.Ua.Configuration;
using MyOpcClient1.Api.Models;
using MyOpcClient1.Api.Models;

namespace MyOpcClient1.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OpcUaController : ControllerBase
    {
        private static Session? _session;
        private static ApplicationConfiguration _config;
        private readonly IConfiguration _configuration;
        private static string _serverUrl;
        private static string _username;
        private static string _password;

        public OpcUaController(IConfiguration configuration)
        {
            _configuration = configuration;
            // Appsettings.json'dan bağlantı bilgilerini al
            _serverUrl = _configuration.GetValue<string>("OpcUa:ServerUrl");
            _username = _configuration.GetValue<string>("OpcUa:Username");
            _password = _configuration.GetValue<string>("OpcUa:Password");
        }

        private async Task<IActionResult> InitializeConnection(string serverUrl = null, string username = null, string password = null)
        {
            try
            {
                // Eğer yeni bağlantı bilgileri geldiyse güncelle
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    _serverUrl = serverUrl;
                    _username = username;
                    _password = password;
                }
                // Eğer bağlantı bilgileri yoksa appsettings.json'dan al
                else if (string.IsNullOrEmpty(_serverUrl))
                {
                    _serverUrl = _configuration.GetValue<string>("OpcUa:ServerUrl");
                    _username = _configuration.GetValue<string>("OpcUa:Username");
                    _password = _configuration.GetValue<string>("OpcUa:Password");

                    if (string.IsNullOrEmpty(_serverUrl))
                    {
                        return Problem("OPC UA connection settings not found in appsettings.json", statusCode: 500);
                    }
                }

                // Eğer config yoksa veya session null ise yeniden bağlantı kur
                if (_config == null || _session == null || _session.Connected == false)
                {
                    var application = new ApplicationInstance
                    {
                        ApplicationName = "MyOpcClient1",
                        ApplicationType = Opc.Ua.ApplicationType.Client,
                    };

                    // Manuel yapılandırma
                    var config = new ApplicationConfiguration
                    {
                        ApplicationName = "MyOpcClient1",
                        ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:MyOpcClient1",
                        ApplicationType = Opc.Ua.ApplicationType.Client,
                        SecurityConfiguration = new SecurityConfiguration
                        {
                            ApplicationCertificate = new Opc.Ua.CertificateIdentifier
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

                    _config = config;
                    await _config.Validate(Opc.Ua.ApplicationType.Client);

                    var certificateIdentifier = _config.SecurityConfiguration.ApplicationCertificate;

                    // Set up certificate
                    if (_config.SecurityConfiguration.ApplicationCertificate.Certificate == null)
                    {
                        X509Certificate2 certificate = null;
                        try
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
                        catch (Exception ex)
                        {
                            throw new Exception($"Failed to create certificate: {ex.Message}");
                        }

                        if (certificate != null)
                        {
                            _config.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;
                        }
                        else
                        {
                            throw new Exception("Failed to create certificate: Certificate is null");
                        }
                    }

                    // Bağlantıyı kur
                    var useSecurity = !string.IsNullOrEmpty(_username);
                    var endpoint = CoreClientUtils.SelectEndpoint(_serverUrl, useSecurity: useSecurity);
                    var endpointConfiguration = EndpointConfiguration.Create(_config);
                    var endpoint_description = new EndpointDescription(_serverUrl);

                    _session = await Session.Create(
                        _config,
                        new ConfiguredEndpoint(null, endpoint, endpointConfiguration),
                        //false,
                        false,
                        _config.ApplicationName,
                        60000,
                        null,//new UserIdentity(_username, _password),
                        null
                    );

                    //await Session.Create(config, configuredEndpoint, false, "MyOpcClient1Session", 60000, null, null);
                }

                return Ok("Connection initialized successfully");
            }
            catch (Exception ex)
            {
                return Problem($"Failed to initialize connection: {ex.Message}", statusCode: 500);
            }
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] OpcUaConnectionInfo connectionInfo)
        {
            return await InitializeConnection(connectionInfo.ServerUrl, connectionInfo.Username, connectionInfo.Password);
        }

        [HttpPost("browse")]
        public async Task<IActionResult> Browse([FromBody] BrowseNodeRequest request)
        {
            try
            {
                // Önce bağlantıyı kontrol et
                var initResult = await InitializeConnection();
                if (initResult is not OkObjectResult)
                {
                    return initResult;
                }

                //var nodeToBrowse = new NodeId(request.StartingNodeId);
                var nodeToBrowse = ObjectIds.ObjectsFolder;
                var browseDescription = new BrowseDescription
                {
                    NodeId = nodeToBrowse,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                    ResultMask = (uint)BrowseResultMask.All
                };

                // Create a browser object
                Browser browser = new Browser(_session);
                browser.BrowseDirection = BrowseDirection.Forward;
                browser.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
                browser.IncludeSubtypes = true;
                browser.NodeClassMask = (int)(NodeClass.Object | NodeClass.Variable);



                // Browse the node
                ReferenceDescriptionCollection references = browser.Browse(nodeToBrowse);

                var nodes = new List<NodeInfo>();
                if (references != null)
                {
                    foreach (var reference in references)
                    {
                        nodes.Add(new NodeInfo
                        {
                            NodeId = reference.NodeId.ToString(),
                            DisplayName = reference.DisplayName.Text,
                            DataType = reference.NodeClass.ToString()
                        });
                    }
                }

                return Ok(nodes);
            }
            catch (Exception ex)
            {
                return Problem($"Browse operation failed: {ex.Message}", statusCode: 500);
            }
        }

        [HttpGet("read/{nodeId}")]
        public async Task<IActionResult> Read(string nodeId)
        {
            try
            {
                // Önce bağlantıyı kontrol et
                var initResult = await InitializeConnection();
                if (initResult is not OkObjectResult)
                {
                    return initResult;
                }

                var node = new NodeId(nodeId);
                DataValue value = _session.ReadValue(node);

                var nodeInfo = new NodeInfo
                {
                    NodeId = nodeId,
                    Value = value.Value?.ToString(),
                    DataType = value.Value?.GetType().ToString()
                };

                return Ok(nodeInfo);
            }
            catch (Exception ex)
            {
                return Problem($"Read operation failed: {ex.Message}", statusCode: 500);
            }
        }

        [HttpPost("write")]
        public async Task<IActionResult> Write([FromBody] NodeWriteRequest request)
        {
            try
            {
                // Önce bağlantıyı kontrol et
                var initResult = await InitializeConnection();
                if (initResult is not OkObjectResult)
                {
                    return initResult;
                }

                var node = new NodeId(request.NodeId);
                var writeValue = new WriteValue
                {
                    NodeId = node,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(request.Value))
                };

                var valuesToWrite = new WriteValueCollection { writeValue };
                StatusCodeCollection results;
                DiagnosticInfoCollection diagnosticInfos;

                _session.Write(
                    null,                   // default request header
                    valuesToWrite,          // values to write
                    out results,            // operation result codes
                    out diagnosticInfos     // diagnostic information
                );

                if (results != null && results.Count > 0)
                {
                    if (Opc.Ua.StatusCode.IsGood(results[0]))
                    {
                        return Ok("Write operation successful");
                    }
                    else
                    {
                        return Problem($"Write operation failed with status: {results[0]}", statusCode: 500);
                    }
                }
                else
                {
                    return Problem("Write operation failed: No results returned", statusCode: 500);
                }
            }
            catch (Exception ex)
            {
                return Problem($"Write operation failed: {ex.Message}", statusCode: 500);
            }
        }

        [HttpPost("createFolder")]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            try
            {
                var result = await InitializeConnection();
                if (result is not OkObjectResult)
                {
                    return result;
                }

                // Parent node'u bul
                //var parentNode = new NodeId(request.ParentNodeId);
                var parentNodeId = string.IsNullOrEmpty(request.ParentNodeId)
                        ? ObjectIds.ObjectsFolder // ns=0;i=85
                        : new NodeId(request.ParentNodeId);

                var parentNodeExists = await _session.ReadNodeAsync(parentNodeId);
                if (parentNodeExists == null)
                {
                    return Problem($"Parent node {request.ParentNodeId} not found", statusCode: 404);
                }

                // Yeni folder için referans oluştur
                var folderTypeId = ObjectTypeIds.FolderType;
                var browseName = new QualifiedName(request.FolderName, 0);
                var displayName = new LocalizedText(request.FolderName);

                // Folder oluştur
                var folder = new FolderState(null);
                folder.Create(_session.SystemContext, parentNodeId, browseName, displayName, true);
                folder.TypeDefinitionId = folderTypeId;

                try
                {
                    // Folder'ı OPC UA sunucusuna ekle
                    folder.ClearChangeMasks(_session.SystemContext, true);
                    var addNodeRequest = new AddNodesItem
                    {
                        ParentNodeId = parentNodeId,
                        ReferenceTypeId = ReferenceTypeIds.Organizes,
                        RequestedNewNodeId = new NodeId(request.FolderName, 2),
                        BrowseName = browseName,
                        NodeClass = NodeClass.Object,
                        NodeAttributes = new ExtensionObject(new ObjectAttributes
                        {
                            DisplayName = displayName,
                            Description = LocalizedText.Null,
                            WriteMask = (int)AttributeWriteMask.None,
                            UserWriteMask = (int)AttributeWriteMask.None,
                            EventNotifier = 0
                        }),
                        TypeDefinition = ObjectTypeIds.FolderType
                    };
                    var nodesToAdd = new AddNodesItemCollection { addNodeRequest };
                    var response = _session.AddNodes(null, nodesToAdd, out var addResults, out var diagnosticInfos);



                    //if (response == null || response.Count == 0 || Opc.Ua.StatusCode.IsBad(response[0].StatusCode))
                    if (response == null)
                    {
                        return Problem($"Failed to create folder: Bad status code", statusCode: 500);
                    }

                    //return Ok(new { NodeId = response[0].AddedNodeId.ToString(), Message = $"Folder '{request.FolderName}' created successfully" });
                    return Ok(new { Message = $"Folder '{request.FolderName}' created successfully" });
                }
                catch (Exception ex)
                {
                    return Problem($"Failed to create folder: {ex.Message}", statusCode: 500);
                }
            }
            catch (Exception ex)
            {
                return Problem($"Error creating folder: {ex.Message}", statusCode: 500);
            }
        }

        //[HttpPost("createVariable")]
        //public async Task<IActionResult> CreateVariable([FromBody] CreateVariableRequest request)
        //{
        //    try
        //    {
        //        var result = await InitializeConnection();
        //        if (result is not OkObjectResult)
        //        {
        //            return result;
        //        }

        //        // Parent node'u bul
        //        var parentNode = new NodeId(request.ParentNodeId);
        //        var parentNodeExists = await _session.ReadNodeAsync(parentNode);
        //        if (parentNodeExists == null)
        //        {
        //            return Problem($"Parent node {request.ParentNodeId} not found", statusCode: 404);
        //        }

        //        // Veri tipini belirle
        //        NodeId dataTypeId;
        //        switch (request.DataType.ToLower())
        //        {
        //            case "boolean":
        //                dataTypeId = DataTypeIds.Boolean;
        //                break;
        //            case "int32":
        //            case "integer":
        //                dataTypeId = DataTypeIds.Int32;
        //                break;
        //            case "double":
        //            case "float":
        //                dataTypeId = DataTypeIds.Double;
        //                break;
        //            case "string":
        //                dataTypeId = DataTypeIds.String;
        //                break;
        //            default:
        //                return Problem($"Unsupported data type: {request.DataType}", statusCode: 400);
        //        }

        //        // Değişken için referans oluştur
        //        var browseName = new QualifiedName(request.VariableName, 0);
        //        var displayName = new LocalizedText(request.VariableName);

        //        // Değişkeni oluştur
        //        var variable = new BaseDataVariableState(_session.NodeCache, parentNode, browseName, displayName, dataTypeId, ValueRanks.Scalar);

        //        // İlk değeri ayarla
        //        try
        //        {
        //            switch (request.DataType.ToLower())
        //            {
        //                case "boolean":
        //                    variable.Value = bool.Parse(request.InitialValue);
        //                    break;
        //                case "int32":
        //                case "integer":
        //                    variable.Value = int.Parse(request.InitialValue);
        //                    break;
        //                case "double":
        //                case "float":
        //                    variable.Value = double.Parse(request.InitialValue);
        //                    break;
        //                case "string":
        //                    variable.Value = request.InitialValue;
        //                    break;
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            return Problem($"Invalid initial value for data type {request.DataType}", statusCode: 400);
        //        }

        //        try
        //        {
        //            // Değişkeni OPC UA sunucusuna ekle
        //            await variable.CreateOrUpdateAsync(_session.SystemContext);
        //            return Ok(new { NodeId = variable.NodeId.ToString(), Message = $"Variable '{request.VariableName}' created successfully" });
        //        }
        //        catch (Exception ex)
        //        {
        //            return Problem($"Failed to create variable: {ex.Message}", statusCode: 500);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return Problem($"Error creating variable: {ex.Message}", statusCode: 500);
        //    }
        //}

        [HttpPost("disconnect")]
        public IActionResult Disconnect()
        {
            try
            {
                if (_session != null)
                {
                    _session.Close();
                    _session = null;
                }
                // Bağlantı bilgilerini temizle
                _serverUrl = null;
                _username = null;
                _password = null;
                return Ok("Disconnected successfully");
            }
            catch (Exception ex)
            {
                return Problem($"Disconnect operation failed: {ex.Message}", statusCode: 500);
            }
        }
    }
}
