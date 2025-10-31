namespace MyOpcClient1.Api.Models
{
    public class OpcUaConnectionInfo
    {
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class NodeInfo
    {
        public string NodeId { get; set; }
        public string DisplayName { get; set; }
        public string Value { get; set; }
        public string DataType { get; set; }
    }

    public class NodeWriteRequest
    {
        public string NodeId { get; set; }
        public string Value { get; set; }
    }

    public class BrowseNodeRequest
    {
        public string StartingNodeId { get; set; }
    }

    public class CreateFolderRequest
    {
        public string ParentNodeId { get; set; }
        public string FolderName { get; set; }
    }

    public class CreateVariableRequest
    {
        public string ParentNodeId { get; set; }
        public string VariableName { get; set; }
        public string DataType { get; set; }
        public string InitialValue { get; set; }
    }
}
