using System.Text;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a plain text message
    /// </summary>
    public class PlainTextMessage : HttpFound
    {
        /// <summary>
        /// Initializes a new instance of PlainTextMessage
        /// </summary>
        public PlainTextMessage():
            this("{}")
        {
        }

        /// <summary>
        /// Initializes a new instance of PlainTextMessage
        /// </summary>
        /// <param name="text">Plain text message</param>
        public PlainTextMessage(string text)
            : base(null, null, null)
        {
            Content = text;
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            // set data type
            ContentType = "text/plain";

            // send data
            data = UTF8Encoding.UTF8.GetBytes(Content);
            base.Send(browser, data);
        }
    }
}