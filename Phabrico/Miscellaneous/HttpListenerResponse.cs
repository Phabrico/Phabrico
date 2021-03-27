using System;
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

        private string internalContentType;
        private System.Net.WebHeaderCollection internalHeaders = new System.Net.WebHeaderCollection();
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
                if (internalHttpListenerResponse == null)
                {
                    return internalContentType;
                }
                
                return internalHttpListenerResponse.ContentType;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalContentType = value;
                }
                
                internalHttpListenerResponse.ContentType = value;
            }
        }

        /// <summary>
        /// Gets or sets the collection of header name/value pairs returned by the server.
        /// </summary>
        public virtual System.Net.WebHeaderCollection Headers
        {
            get
            {
                if (internalHttpListenerResponse == null)
                {
                    return internalHeaders;
                }
                
                return internalHttpListenerResponse.Headers;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalHeaders = value;
                }
                
                internalHttpListenerResponse.Headers = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of bytes in the body data included in the response.
        /// </summary>
        public virtual long ContentLength64
        {
            get
            {
                if (internalHttpListenerResponse == null)
                {
                    return internalContentLength64;
                }
                
                return internalHttpListenerResponse.ContentLength64;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalContentLength64 = value;
                }
                
                internalHttpListenerResponse.ContentLength64 = value;
            }
        }

        /// <summary>
        /// Gets or sets the HTTP status code to be returned to the client.
        /// </summary>
        public int StatusCode
        {
            get
            {
                if (internalHttpListenerResponse == null)
                {
                    return internalStatusCode;
                }
                
                return internalHttpListenerResponse.StatusCode;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalStatusCode = value;
                }
                
                internalHttpListenerResponse.StatusCode = value;
            }
        }

        /// <summary>
        /// Gets or sets a text description of the HTTP status code returned to the client.
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (internalHttpListenerResponse == null)
                {
                    return internalStatusDescription;
                }
                
                return internalHttpListenerResponse.StatusDescription;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalStatusDescription = value;
                }
                
                internalHttpListenerResponse.StatusDescription = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the HTTP Location header in this response.
        /// </summary>
        public string RedirectLocation
        {
            get
            {
                if (internalHttpListenerResponse == null)
                {
                    return internalRedirectLocation;
                }
                
                return internalHttpListenerResponse.RedirectLocation;
            }

            set
            {
                if (internalHttpListenerResponse == null)
                {
                    internalRedirectLocation = value;
                }
                
                internalHttpListenerResponse.RedirectLocation = value;
            }
        }

        /// <summary>
        /// Gets a System.IO.Stream object to which a response can be written.
        /// </summary>
        public Stream OutputStream
        {
            get
            {
                return internalHttpListenerResponse.OutputStream;
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
        /// Adds a HTTP header to a response
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AddHeader(string name, string value)
        {
            if (internalHttpListenerResponse == null)
            {
                internalHeaders.Clear();
                internalHeaders.Add(name, value);
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
            if (internalHttpListenerResponse == null)
            {
                internalHeaders.Add(name, value);
            }
            else
            {
                internalHttpListenerResponse.AppendHeader(name, value);
            }
        }
    }
}
