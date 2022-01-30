using System;
using System.Runtime.InteropServices;

namespace Phabrico.Http
{
    /// <summary>
    /// Helper class for Windows impersonation functionality
    /// </summary>
    public static class ImpersonationHelper
    {
        /// <summary>
        /// Combines DELETE, READ_CONTROL, WRITE_DAC, and WRITE_OWNER access
        /// </summary>
        private static readonly uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;

        /// <summary>
        /// Currently defined to equal READ_CONTROL
        /// </summary>
        private static readonly uint STANDARD_RIGHTS_READ = 0x00020000;

        /// <summary>
        /// Required to attach a primary token to a process. The SE_ASSIGNPRIMARYTOKEN_NAME privilege is also required to accomplish this task.
        /// </summary>
        private static readonly uint TOKEN_ASSIGN_PRIMARY = 0x0001;

        /// <summary>
        /// Required to duplicate an access token.
        /// </summary>
        private static readonly uint TOKEN_DUPLICATE = 0x0002;

        /// <summary>
        /// Required to attach an impersonation access token to a process.
        /// </summary>
        private static readonly uint TOKEN_IMPERSONATE = 0x0004;

        /// <summary>
        /// Required to query an access token.
        /// </summary>
        private static readonly uint TOKEN_QUERY = 0x0008;

        /// <summary>
        /// Required to query the source of an access token.
        /// </summary>
        private static readonly uint TOKEN_QUERY_SOURCE = 0x0010;

        /// <summary>
        /// Required to enable or disable the privileges in an access token.
        /// </summary>
        private static readonly uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

        /// <summary>
        /// Required to adjust the attributes of the groups in an access token.
        /// </summary>
        private static readonly uint TOKEN_ADJUST_GROUPS = 0x0040;

        /// <summary>
        /// Required to change the default owner, primary group, or DACL of an access token.
        /// </summary>
        private static readonly uint TOKEN_ADJUST_DEFAULT = 0x0080;

        /// <summary>
        /// Required to adjust the session ID of an access token. The SE_TCB_NAME privilege is required.
        /// </summary>
        private static readonly uint TOKEN_ADJUST_SESSIONID = 0x0100;

        /// <summary>
        /// Combines STANDARD_RIGHTS_READ and TOKEN_QUERY.
        /// </summary>
        private static readonly uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);

        /// <summary>
        /// Combines all possible access rights for a token.
        /// </summary>
        private static readonly uint TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE | TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID);

        /// <summary>
        /// contains values that specify security impersonation levels.
        /// Security impersonation levels govern the degree to which a server process can act on behalf of a client process.
        /// </summary>
        public enum SECURITY_IMPERSONATION_LEVEL
        {
            /// <summary>
            /// The server process cannot obtain identification information about the client, and it cannot impersonate the client.
            /// It is defined with no value given, and thus, by ANSI C rules, defaults to a value of zero.
            /// </summary>
            SecurityAnonymous,

            /// <summary>
            /// The server process can obtain information about the client, such as security identifiers and privileges, but it cannot
            /// impersonate the client. This is useful for servers that export their own objects, for example, database products that
            /// export tables and views. Using the retrieved client-security information, the server can make access-validation decisions
            /// without being able to use other services that are using the client's security context.
            /// </summary>
            SecurityIdentification,

            /// <summary>
            /// The server process can impersonate the client's security context on its local system. The server cannot impersonate the
            /// client on remote systems.
            /// </summary>
            SecurityImpersonation,

            /// <summary>
            /// The server process can impersonate the client's security context on remote systems.
            /// </summary>
            SecurityDelegation
        }

        /// <summary>
        /// contains values that differentiate between a primary token and an impersonation token.
        /// </summary>
        public enum TOKEN_TYPE
        {
            /// <summary>
            /// Indicates a primary token.
            /// </summary>
            TokenPrimary = 1,

            /// <summary>
            /// Indicates an impersonation token.
            /// </summary>
            TokenImpersonation
        }

        /// <summary>
        /// Specifies the connection state of a Remote Desktop Services session.
        /// </summary>
        public enum WTS_CONNECTSTATE_CLASS
        {
            /// <summary>
            /// A user is logged on to the WinStation. This state occurs when a user is signed in and actively connected to the device.
            /// </summary>
            WTSActive,

            /// <summary>
            /// The WinStation is connected to the client.
            /// </summary>
            WTSConnected,

            /// <summary>
            /// The WinStation is in the process of connecting to the client.
            /// </summary>
            WTSConnectQuery,

            /// <summary>
            /// The WinStation is shadowing another WinStation.
            /// </summary>
            WTSShadow,

            /// <summary>
            /// The WinStation is active but the client is disconnected. This state occurs when a user is signed in but not actively connected
            /// to the device, such as when the user has chosen to exit to the lock screen.
            /// </summary>
            WTSDisconnected,

            /// <summary>
            /// The WinStation is waiting for a client to connect.
            /// </summary>
            WTSIdle,

            /// <summary>
            /// The WinStation is listening for a connection. A listener session waits for requests for new client connections.
            /// No user is logged on a listener session. A listener session cannot be reset, shadowed, or changed to a regular client session.
            /// </summary>
            WTSListen,

            /// <summary>
            /// The WinStation is being reset.
            /// </summary>
            WTSReset,

            /// <summary>
            /// The WinStation is down due to an error.
            /// </summary>
            WTSDown,

            /// <summary>
            /// The WinStation is initializing.
            /// </summary>
            WTSInit
        }

        /// <summary>
        /// Contains information about a client session on a Remote Desktop Session Host (RD Session Host) server
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            /// <summary>
            /// Session identifier of the session.
            /// </summary>
            public Int32 SessionID;
            
            /// <summary>
            /// Pointer to a null-terminated string that contains the WinStation name of this session.
            /// The WinStation name is a name that Windows associates with the session, for example, "services", "console", or "RDP-Tcp#0".
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public String pWinStationName;

            /// <summary>
            /// A value from the WTS_CONNECTSTATE_CLASS enumeration type that indicates the session's current connection state.
            /// </summary>
            public WTS_CONNECTSTATE_CLASS State;
        }

        /// <summary>
        /// creates a new access token that duplicates an existing token. This function can create either a primary token or an impersonation token.
        /// </summary>
        /// <param name="existingToken">handle to an access token opened with TOKEN_DUPLICATE access</param>
        /// <param name="desiredAccess">Specifies the requested access rights for the new token</param>
        /// <param name="tokenAttributes">pointer to a SECURITY_ATTRIBUTES structure that specifies a security descriptor for the new token </param>
        /// <param name="impersonationLevel">Specifies a value from the SECURITY_IMPERSONATION_LEVEL enumeration that indicates the impersonation level of the new token.</param>
        /// <param name="tokenType">Specifies a value from the TOKEN_TYPE enumeration</param>
        /// <param name="newToken">pointer to a HANDLE variable that receives the new token.</param>
        /// <returns>Non-zero if success</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public extern static bool DuplicateTokenEx(IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes, SECURITY_IMPERSONATION_LEVEL impersonationLevel, TOKEN_TYPE tokenType, out IntPtr newToken);

        /// <summary>
        /// Retrieves a list of sessions on a Remote Desktop Session Host (RD Session Host) server.
        /// </summary>
        /// <param name="hServer">handle to the RD Session Host server</param>
        /// <param name="Reserved">This parameter is reserved. It must be zero</param>
        /// <param name="Version">version of the enumeration request. This parameter must be 1.</param>
        /// <param name="ppSessionInfo">pointer to an array of WTS_SESSION_INFO structures that represent the retrieved sessions</param>
        /// <param name="pCount">pointer to the number of WTS_SESSION_INFO structures returned in the ppSessionInfo parameter</param>
        /// <returns>Non-zero if success</returns>
        [DllImport("wtsapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int WTSEnumerateSessions(System.IntPtr hServer, int Reserved, int Version, ref System.IntPtr ppSessionInfo, ref int pCount);

        /// <summary>
        /// Frees memory allocated by a Remote Desktop Services function.
        /// </summary>
        /// <param name="memory">Pointer to the memory to free.</param>
        [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = false)]
        public static extern void WTSFreeMemory(IntPtr memory);

        /// <summary>
        /// Obtains the primary access token of the logged-on user specified by the session ID
        /// </summary>
        /// <param name="sessionId">Remote Desktop Services session identifier</param>
        /// <param name="tokenHandle">pointer to the token handle for the logged-on user (if success)</param>
        /// <returns>Non-zero if success</returns>
        [DllImport("wtsapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool WTSQueryUserToken(int sessionId, out IntPtr tokenHandle);

        /// <summary>
        /// Returns the token of the user currently logged on on WinSta0
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetCurrentUserToken()
        {
            IntPtr currentToken = IntPtr.Zero;
            IntPtr primaryToken = IntPtr.Zero;
            IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

            int dwSessionId = 0;

            IntPtr pSessionInfo = IntPtr.Zero;
            int dwCount = 0;

            WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, ref pSessionInfo, ref dwCount);

            Int32 dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

            Int64 current = (Int64)pSessionInfo;
            for (int i = 0; i < dwCount; i++)
            {
                WTS_SESSION_INFO si = (WTS_SESSION_INFO)Marshal.PtrToStructure((System.IntPtr)current, typeof(WTS_SESSION_INFO));
                if (WTS_CONNECTSTATE_CLASS.WTSActive == si.State)
                {
                    dwSessionId = si.SessionID;
                    break;
                }

                current += dataSize;
            }

            WTSFreeMemory(pSessionInfo);

            bool bRet = WTSQueryUserToken(dwSessionId, out currentToken);
            if (bRet == false)
            {
                return IntPtr.Zero;
            }

            bRet = DuplicateTokenEx(currentToken, TOKEN_ASSIGN_PRIMARY | TOKEN_ALL_ACCESS, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out primaryToken);
            if (bRet == false)
            {
                return IntPtr.Zero;
            }

            return primaryToken;
        }
    }
}
