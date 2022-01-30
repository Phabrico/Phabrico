using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Wrapper for System.Net.HttpListenerContext
    /// This class was wrapped because some of the functionality couldn't be unit-tested otherwise
    /// </summary>
    public class HttpListenerContext
    {
        private System.Web.HttpContext internalHttpContext;
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
                return internalHttpListenerContext == null
                    && internalHttpContext == null;
            }
        }

        public string[] Modules
        {
            get
            {
                return internalHttpContext?.ApplicationInstance?.Modules?.AllKeys;
            }
        }

        /// <summary>
        /// Gets the System.Net.HttpListenerRequest that represents a client's request for a resource.
        /// </summary>
        public HttpListenerRequest Request
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    if (internalHttpListenerContext == null)
                    {
                        if (internalHttpContext == null)
                        {
                            if (internalHttpListenerRequest == null)
                            {
                                internalHttpListenerRequest = new HttpListenerRequest();
                            }
                        }
                        else
                        {
                            internalHttpListenerRequest = internalHttpContext.Request;
                        }
                    }
                    else
                    {
                        internalHttpListenerRequest = internalHttpListenerContext.Request;
                    }
                }

                return internalHttpListenerRequest;
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
                    if (internalHttpContext == null)
                    {
                        if (internalHttpListenerResponse == null)
                        {
                            internalHttpListenerResponse = new HttpListenerResponse();
                        }

                        return internalHttpListenerResponse;
                    }
                    else
                    {
                        return internalHttpContext.Response;
                    }
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
                if (internalHttpListenerContext == null && internalHttpContext == null)
                {
                    return null;
                }
                else
                {
                    System.Security.Principal.WindowsPrincipal windowsPrincipal = null;

                    if (internalHttpContext != null)
                    {
                        windowsPrincipal = internalHttpContext.User as System.Security.Principal.WindowsPrincipal;
                    }
                    else
                    {
                        windowsPrincipal = internalHttpListenerContext?.User as System.Security.Principal.WindowsPrincipal;
                    }

                    if (windowsPrincipal == null) return null;

                    return windowsPrincipal.Identity as System.Security.Principal.WindowsIdentity;
                }
            }
        }

        /// <summary>
        /// Returns true if the request is a WebSocket request
        /// </summary>
        public bool IsWebSocketRequest
        {
            get
            {
                if (internalHttpListenerContext == null && internalHttpContext == null)
                {
                    return false;
                }

                if (internalHttpContext != null)
                {
                    return internalHttpContext.IsWebSocketRequest
                        || internalHttpContext.Request.Headers.AllKeys.Any(key => key.Equals("Sec-WebSocket-Key"));  // HttpContext.IsWebSocketRequest is not always working ?!?
                }
                else
                {
                    return internalHttpListenerContext?.Request?.IsWebSocketRequest ?? false;
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
            internalHttpListenerRequest = new HttpListenerRequest(request);

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
        /// Converts a System.Web.HttpContext object implicitly into a Phabrico.Miscellaneous.HttpListenerContext object
        /// </summary>
        /// <param name="httpListenerContext"></param>
        public static implicit operator HttpListenerContext(System.Web.HttpContext httpContext)
        {
            return new HttpListenerContext {
                internalHttpContext = httpContext
            };
        }

        /// <summary>
        /// Accept an incoming WebSocket connection
        /// The communication of the websocket is executed in AcceptWebSocketAsync
        /// </returns>
        public void AcceptWebSocket()
        {
            if (internalHttpContext != null)
            {
                internalHttpContext.AcceptWebSocketRequest(AcceptWebSocketAsync);
            }

            if (internalHttpListenerContext != null)
            {
                Task<HttpListenerWebSocketContext> result = internalHttpListenerContext.AcceptWebSocketAsync(null, 8192, TimeSpan.FromMilliseconds(500));
                result.ContinueWith(task =>  AcceptWebSocketAsync(result.Result) );
            }
        }

        /// <summary>
        /// Processes the communication of a websocket
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task AcceptWebSocketAsync(WebSocketContext context)
        {
            byte[] buffer = new byte[8192];
            Http.Server.WebSockets.Add(context);

            try
            {
                if (context.WebSocket.State == WebSocketState.Open)
                {
                    Http.Server.SendLatestNotifications();
                }

                while (context.WebSocket.State == WebSocketState.Open)
                {
                    var response = await context.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (response.MessageType == WebSocketMessageType.Close)
                    {
                        await
                            context.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close response received",
                                CancellationToken.None);
                    }
                }
            }
            finally
            {
                Http.Server.WebSockets.Remove(context);
            }
        }
    }
}
