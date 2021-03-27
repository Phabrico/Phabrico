using System.IO;
using System.Net;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Wrapper for System.Net.HttpListenerRequest
    /// This class was wrapped because some of the functionality couldn't be unit-tested otherwise
    /// </summary>
    public class HttpListenerRequest
    {
        System.Net.HttpListenerRequest internalHttpListenerRequest;

        private CookieCollection internalCookies = new CookieCollection();
        private IPEndPoint internalRemoteEndPoint;
        private string internalRequestUrl;
        private string internalUserAgent;
        private string[] internalUserLanguages;

        /// <summary>
        /// Gets the length of the body data included in the request.
        /// </summary>
        public long ContentLength64
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return 0;
                }

                return internalHttpListenerRequest.ContentLength64;
            }
        }

        /// <summary>
        /// Gets the MIME type of the body data included in the request.
        /// </summary>
        public virtual string ContentType
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return "";
                }

                return internalHttpListenerRequest.ContentType;
            }
        } 

        /// <summary>
        /// Gets the cookies sent with the request.
        /// </summary>
        public virtual CookieCollection Cookies
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return internalCookies;
                }

                return internalHttpListenerRequest.Cookies;
            }

            set
            {
                internalCookies = value;
            }
        }

        /// <summary>
        /// Gets the HTTP method specified by the client.
        /// </summary>
        public virtual string HttpMethod
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return "GET";
                }

                return internalHttpListenerRequest.HttpMethod;
            }
        } 

        /// <summary>
        /// Gets a stream that contains the body data sent by the client.
        /// </summary>
        public Stream InputStream
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return new MemoryStream();
                }

                return internalHttpListenerRequest.InputStream;
            }
        }

        /// <summary>
        /// Returns true if the request is sent from the local computer
        /// </summary>
        public bool IsLocal
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return true;
                }

                return internalHttpListenerRequest.IsLocal;
            }
        }
        
        /// <summary>
        /// Returns true if the request is a WebSocket request
        /// </summary>
        public bool IsWebSocketRequest
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return false;
                }

                return internalHttpListenerRequest.IsWebSocketRequest;
            }
        }

        /// <summary>
        /// Gets the URL information (without the host and port) requested by the client.
        /// </summary>
        public virtual string RawUrl
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    return internalRequestUrl;
                }

                return internalHttpListenerRequest.RawUrl;
            }

            set
            {
                internalRequestUrl = value;
            }
        }

        /// <summary>
        /// Gets the client IP address and port number from which the request originated.
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    internalRemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234);
                    return internalRemoteEndPoint;
                }

                return internalHttpListenerRequest.RemoteEndPoint;
            }

            set
            {
                internalRemoteEndPoint = value;
            }
        }

        /// <summary>
        /// Gets the user agent presented by the client.
        /// </summary>
        public virtual string UserAgent
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    internalUserAgent = "";
                    return internalUserAgent;
                }

                return internalHttpListenerRequest.UserAgent;
            }

            set
            {
                internalUserAgent = value;
            }
        }

        /// <summary>
        /// Gets the language of the browser
        /// </summary>
        public virtual string[] UserLanguages
        {
            get
            {
                if (internalHttpListenerRequest == null)
                {
                    internalUserLanguages = new string[] { "en-US" };
                    return internalUserLanguages;
                }

                return internalHttpListenerRequest.UserLanguages;
            }

            set
            {
                internalUserLanguages = value;
            }
        }

        /// <summary>
        /// Initializes a new HttpListenerRequest object
        /// </summary>
        public HttpListenerRequest()
        {
        }

        /// <summary>
        /// Converts a System.Net.HttpListenerRequest object implicitly into a Phabrico.Miscellaneous.HttpListenerRequest object
        /// </summary>
        /// <param name="httpListenerRequest"></param>
        public static implicit operator HttpListenerRequest(System.Net.HttpListenerRequest httpListenerRequest)
        {
            return new HttpListenerRequest {
                internalHttpListenerRequest = httpListenerRequest
            };
        }
    }
}
