using System.Runtime.InteropServices;

namespace Extensibility;

public enum ext_ConnectMode
{
    ext_cm_AfterStartup = 0,
    ext_cm_Startup = 1,
    ext_cm_External = 2,
    ext_cm_CommandLine = 3
}

public enum ext_DisconnectMode
{
    ext_dm_HostShutdown = 0,
    ext_dm_UserClosed = 1
}

[ComImport]
[Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IDTExtensibility2
{
    [DispId(1)]
    void OnConnection(
        [In] object Application,
        [In] ext_ConnectMode ConnectMode,
        [In] object AddInInst,
        [In, Out, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref Array custom);

    [DispId(2)]
    void OnDisconnection(
        [In] ext_DisconnectMode RemoveMode,
        [In, Out, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref Array custom);

    [DispId(3)]
    void OnAddInsUpdate(
        [In, Out, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref Array custom);

    [DispId(4)]
    void OnStartupComplete(
        [In, Out, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref Array custom);

    [DispId(5)]
    void OnBeginShutdown(
        [In, Out, MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref Array custom);
}
