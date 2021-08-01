using System;
using System.Collections.Specialized;
using System.IO;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Wrapper for System.Net.HttpListenerResponse
    /// This class was wrapped because some of the functionality couldn't be unit-tested otherwise
    /// </summary>
    public class HttpListenerResponse
    {
        private System.Net.HttpListenerResponse internalHttpListenerResponse;
        private System.Web.HttpResponse internalHttpResponse;

        private string internalContentType;
        private NameValueCollection internalHeaders = new NameValueCollection();
        private long internalContentLength64;
        private int internalStatusCode;
        private string internalStatusDescription;
        private string internalRedirectLocation;

        /// <summary>
        /// Gets or sets the MIME type of the content returned.
        /// </summary>
        public virtual string ContentType
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalContentType;
                }
                else
                if (internalHttpResponse != null)
                {
                    return internalHttpResponse.ContentType;
                }
                else
                {
                    return internalHttpListenerResponse.ContentType;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalContentType = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.ContentType = value;
                }
                else
                {
                    internalHttpListenerResponse.ContentType = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the collection of header name/value pairs returned by the server.
        /// </summary>
        public virtual NameValueCollection Headers
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalHeaders;
                }
                else
                if (internalHttpResponse != null)
                {
                    return  internalHttpResponse.Headers;
                }
                else
                {
                    return internalHttpListenerResponse.Headers;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalHeaders = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.Headers.Clear();
                    internalHttpResponse.Headers.Add(value);
                }
                else
                {
                    internalHttpListenerResponse.Headers.Clear();
                    internalHttpListenerResponse.Headers.Add(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of bytes in the body data included in the response.
        /// </summary>
        public virtual long ContentLength64
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalContentLength64;
                }
                else
                if (internalHttpResponse != null)
                {
                    try
                    {
                        return Int32.Parse(internalHttpResponse.Headers["Content-Length"]);
                    }
                    catch
                    {
                        return 0;
                    }
                }
                else
                {
                    return internalHttpListenerResponse.ContentLength64;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalContentLength64 = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.Headers["Content-Length"] = value.ToString();
                }
                else
                {
                    internalHttpListenerResponse.ContentLength64 = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the HTTP status code to be returned to the client.
        /// </summary>
        public int StatusCode
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalStatusCode;
                }
                else
                if (internalHttpResponse != null)
                {
                    return internalHttpResponse.StatusCode;
                }
                else
                {
                    return internalHttpListenerResponse.StatusCode;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalStatusCode = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.StatusCode = value;
                }
                else
                {
                    internalHttpListenerResponse.StatusCode = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a text description of the HTTP status code returned to the client.
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalStatusDescription;
                }
                else
                if (internalHttpResponse != null)
                {
                    return internalHttpListenerResponse.StatusDescription;
                }
                else
                {
                    return internalHttpResponse.StatusDescription;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalStatusDescription = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.StatusDescription = value;
                }
                else
                {
                    internalHttpListenerResponse.StatusDescription = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of the HTTP Location header in this response.
        /// </summary>
        public string RedirectLocation
        {
            get
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    return internalRedirectLocation;
                }
                else
                if (internalHttpResponse != null)
                {
                    return internalHttpResponse.RedirectLocation;
                }
                else
                {
                    return internalHttpListenerResponse.RedirectLocation;
                }
            }

            set
            {
                if (internalHttpListenerResponse == null && internalHttpResponse == null)
                {
                    internalRedirectLocation = value;
                }
                else
                if (internalHttpResponse != null)
                {
                    internalHttpResponse.RedirectLocation = value;
                }
                else
                {
                    internalHttpListenerResponse.RedirectLocation = value;
                }
            }
        }

        /// <summary>
        /// Gets a System.IO.Stream object to which a response can be written.
        /// </summary>
        public Stream OutputStream
        {
            get
            {
                if (internalHttpResponse != null)
                {
                    return internalHttpResponse.OutputStream;
                }
                else
                {
                    return internalHttpListenerResponse.OutputStream;
                }
            }
        }

        /// <summary>
        /// Initializes a HttpListenerResponse object
        /// </summary>
        public HttpListenerResponse()
        {
        }

        /// <summary>
        /// Converts a System.Net.HttpListenerResponse object implicitly into a Phabrico.Miscellaneous.HttpListenerResponse object
        /// </summary>
        /// <param name="httpListenerResponse"></param>
        public static implicit operator HttpListenerResponse(System.Net.HttpListenerResponse httpListenerResponse)
        {
            return new HttpListenerResponse {
                internalHttpListenerResponse = httpListenerResponse
            };
        }

        /// <summary>
        /// Converts a System.Web.HttpResponse object implicitly into a Phabrico.Miscellaneous.HttpListenerContext object
        /// </summary>
        /// <param name="httpListenerContext"></param>
        public static implicit operator HttpListenerResponse(System.Web.HttpResponse httpContext)
        {
            return new HttpListenerResponse {
                internalHttpResponse = httpContext
            };
        }

        /// <summary>
        /// Adds a HTTP header to a response
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AddHeader(string name, string value)
        {
            if (internalHttpListenerResponse == null && internalHttpResponse == null)
            {
                internalHeaders.Clear();
                internalHeaders.Add(name, value);
            }
            else
            if (internalHttpResponse != null)
            {
                if (internalHttpResponse.Headers[name] == null)
                {
                    internalHttpResponse.AddHeader(name, value);
                }
                else
                {
                    internalHttpResponse.Headers[name] = value;
                }
            }
            else
            {
                internalHttpListenerResponse.AddHeader(name, value);
            }
        }

        /// <summary>
        /// Adds a HTTP header to a response
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AppendHeader(string name, string value)
        {
            if (internalHttpListenerResponse == null  &&  internalHttpResponse == null)
            {
                internalHeaders.Add(name, value);
            }
            else
            if (internalHttpResponse != null)
            {
                internalHttpResponse.AppendHeader(name, value);
            }
            else
            {
                internalHttpListenerResponse.AppendHeader(name, value);
            }
        }
    }
}
