using System;
using System.Threading.Tasks;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Wrapper for System.Net.HttpListenerContext
    /// This class was wrapped because some of the functionality couldn't be unit-tested otherwise
    /// </summary>
    public class HttpListenerContext
    {
        private System.Net.HttpListenerContext internalHttpListenerContext;
        private HttpListenerResponse internalHttpListenerResponse;
        private HttpListenerRequest internalHttpListenerRequest;

        /// <summary>
        /// True if this instance is running in a Unit test
        /// </summary>
        public bool IsFake
        {
            get
            {
                return internalHttpListenerContext == null;
            }
        }

        /// <summary>
        /// Gets the System.Net.HttpListenerRequest that represents a client's request for a resource.
        /// </summary>
        public HttpListenerRequest Request
        {
            get
            {
                if (internalHttpListenerContext == null)
                {
                    if (internalHttpListenerRequest == null)
                    {
                        internalHttpListenerRequest = new HttpListenerRequest();
                    }

                    return internalHttpListenerRequest;
                }
                else
                {
                    return internalHttpListenerContext.Request;
                }
            }
        }

        /// <summary>
        ///  Gets the System.Net.HttpListenerResponse object that will be sent to the client in response to the client's request.
        /// </summary>
        public HttpListenerResponse Response
        {
            get
            {
                if (internalHttpListenerContext == null)
                {
                    if (internalHttpListenerResponse == null)
                    {
                        internalHttpListenerResponse = new HttpListenerResponse();
                    }

                    return internalHttpListenerResponse;
                }
                else
                {
                    return internalHttpListenerContext.Response;
                }
            }
        }

        /// <summary>
        /// If logged on with a Windows account, this property will return the Windows identity
        /// </summary>
        public System.Security.Principal.WindowsIdentity WindowsIdentity
        {
            get
            {
                if (internalHttpListenerContext == null)
                {
                    return null;
                }
                else
                {
                    System.Security.Principal.WindowsPrincipal windowsPrincipal = internalHttpListenerContext.User as System.Security.Principal.WindowsPrincipal;
                    if (windowsPrincipal == null) return null;

                    return windowsPrincipal.Identity as System.Security.Principal.WindowsIdentity;
                }
            }
        }


        /// <summary>
        /// Initializes a HttpListenerContext object
        /// </summary>
        public HttpListenerContext()
        {
        }

        /// <summary>
        /// Semi copy constructor
        /// </summary>
        /// <param name="request"></param>
        public HttpListenerContext(HttpListenerRequest request)
        {
            internalHttpListenerRequest = new HttpListenerRequest();
            internalHttpListenerRequest.Cookies = request.Cookies;
            internalHttpListenerRequest.RawUrl = request.RawUrl;
            internalHttpListenerRequest.RemoteEndPoint = request.RemoteEndPoint;
            internalHttpListenerRequest.UserAgent = request.UserAgent;
            internalHttpListenerRequest.UserLanguages = request.UserLanguages;

            internalHttpListenerResponse = new HttpListenerResponse();
        }

        /// <summary>
        /// Converts a System.Net.HttpListenerContext object implicitly into a Phabrico.Miscellaneous.HttpListenerContext object
        /// </summary>
        /// <param name="httpListenerContext"></param>
        public static implicit operator HttpListenerContext(System.Net.HttpListenerContext httpListenerContext)
        {
            return new HttpListenerContext {
                internalHttpListenerContext = httpListenerContext
            };
        }

        /// <summary>
        /// Accept a WebSocket connection specifying the supported WebSocket sub-protocol,
        /// receive buffer size, and WebSocket keep-alive interval as an asynchronous operation.
        /// </summary>
        /// <param name="subProtocol">The supported WebSocket sub-protocol</param>
        /// <param name="receiveBufferSize">The receive buffer size in bytes</param>
        /// <param name="keepAliveInterval">The WebSocket protocol keep-alive interval in milliseconds</param>
        /// <returns>
        /// Returns System.Threading.Tasks.Task`1.The task object representing the asynchronous
        /// operation. The System.Threading.Tasks.Task`1.Result property on the task object
        /// returns an System.Net.WebSockets.HttpListenerWebSocketContext object.
        /// </returns>
        public Task<System.Net.WebSockets.HttpListenerWebSocketContext> AcceptWebSocketAsync(string subProtocol, int receiveBufferSize, TimeSpan keepAliveInterval)
        {
            return internalHttpListenerContext.AcceptWebSocketAsync(subProtocol, receiveBufferSize, keepAliveInterval);
        }
    }
}
