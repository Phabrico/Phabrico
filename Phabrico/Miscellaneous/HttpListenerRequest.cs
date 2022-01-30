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
        System.Web.HttpRequest internalHttpRequest;

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
                if (internalHttpListenerRequest == null  &&  internalHttpRequest == null)
                {
                    return 0;
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.ContentLength;
                }
                else
                {
                    return internalHttpListenerRequest?.ContentLength64 ?? 0;
                }
            }
        }

        /// <summary>
        /// Gets the MIME type of the body data included in the request.
        /// </summary>
        public virtual string ContentType
        {
            get
            {
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    return "";
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.ContentType;
                }
                else
                {
                    return internalHttpListenerRequest?.ContentType ?? "";
                }
            }
        } 

        /// <summary>
        /// Gets the cookies sent with the request.
        /// </summary>
        public virtual CookieCollection Cookies
        {
            get
            {
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    return internalCookies;
                }

                if (internalHttpRequest != null)
                {
                    CookieCollection cookieCollection = new CookieCollection();
                    foreach (string httpCookieName in internalHttpRequest.Cookies.AllKeys)
                    {
                        cookieCollection.Add(new Cookie(httpCookieName, internalHttpRequest.Cookies[httpCookieName].Value));
                    }
                    return cookieCollection;
                }
                else
                {
                    return internalHttpListenerRequest?.Cookies;
                }
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
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    return "GET";
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.HttpMethod;
                }
                else
                {
                    return internalHttpListenerRequest?.HttpMethod ?? "";
                }
            }
        } 

        /// <summary>
        /// Gets a stream that contains the body data sent by the client.
        /// </summary>
        public Stream InputStream
        {
            get
            {
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    return new MemoryStream();
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.InputStream;
                }
                else
                {
                    return internalHttpListenerRequest?.InputStream;
                }
            }
        }

        /// <summary>
        /// Returns true if the request is sent from the local computer
        /// </summary>
        public bool IsLocal
        {
            get
            {
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    return true;
                }

                if (internalHttpRequest != null)
                {
                    try
                    {
                        return internalHttpRequest.IsLocal;
                    }
                    catch
                    {
                        return true;
                    }
                }
                else
                {
                    return internalHttpListenerRequest?.IsLocal ?? true;
                }
            }
        }

        /// <summary>
        /// Gets the URL information (without the host and port) requested by the client.
        /// </summary>
        public virtual string RawUrl
        {
            get
            {
                string rawUrl;

                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    rawUrl = internalRequestUrl ?? Http.Server.RootPath;
                }
                else
                if (internalHttpRequest != null)
                {
                    rawUrl = internalHttpRequest.RawUrl;
                }
                else
                {
                    rawUrl = internalHttpListenerRequest?.RawUrl;
                }

                return "/" + rawUrl?.Substring(Http.Server.RootPath.TrimEnd('/').Length)
                                   ?.TrimStart('/');
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
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    internalRemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234);
                    return internalRemoteEndPoint;
                }

                if (internalHttpRequest != null)
                {
                    return new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234);
                }
                else
                {
                    return internalHttpListenerRequest?.RemoteEndPoint;
                }
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
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    internalUserAgent = "";
                    return internalUserAgent;
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.UserAgent;
                }
                else
                {
                    return internalHttpListenerRequest?.UserAgent;
                }
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
                if (internalHttpListenerRequest == null && internalHttpRequest == null)
                {
                    internalUserLanguages = new string[] { "en-US" };
                    return internalUserLanguages;
                }

                if (internalHttpRequest != null)
                {
                    return internalHttpRequest.UserLanguages;
                }
                else
                {
                    return internalHttpListenerRequest?.UserLanguages ?? new string[] { "en-US" };
                }
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
        /// Initializes a new HttpListenerRequest object with some properties
        /// </summary>
        /// <param name="request"></param>
        public HttpListenerRequest(HttpListenerRequest request)
        {
            Cookies = request.Cookies;
            RawUrl = request.RawUrl;
            RemoteEndPoint = request.RemoteEndPoint;
            UserAgent = request.UserAgent;
            UserLanguages = request.UserLanguages;

            internalHttpRequest = request.internalHttpRequest;
            internalHttpListenerRequest = request.internalHttpListenerRequest;
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

        /// <summary>
        /// Converts a System.Web.HttpRequest object implicitly into a Phabrico.Miscellaneous.HttpListenerRequest object
        /// </summary>
        /// <param name="httpListenerRequest"></param>
        public static implicit operator HttpListenerRequest(System.Web.HttpRequest httpRequest)
        {
            return new HttpListenerRequest {
                internalHttpRequest = httpRequest
            };
        }
    }
}
