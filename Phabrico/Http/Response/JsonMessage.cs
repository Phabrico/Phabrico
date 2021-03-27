using System.Text;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a JSON message
    /// </summary>
    public class JsonMessage : HttpFound
    {
        /// <summary>
        /// Initializes a new instance of JsonMessage
        /// </summary>
        public JsonMessage():
            this("{}")
        {
        }

        /// <summary>
        /// Initializes a new instance of JsonMessage
        /// </summary>
        /// <param name="json">JSON message</param>
        public JsonMessage(string json)
            : base(null, null, null)
        {
            Content = json;
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            // set data type
            ContentType = "application/json";

            // send data
            data = UTF8Encoding.UTF8.GetBytes(Content);
            base.Send(browser, data);
        }
    }
}