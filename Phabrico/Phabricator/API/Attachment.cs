using Newtonsoft.Json.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represents an Attachment filter used in the Phabricator Conduit API
    /// </summary>
    public class Attachment
    {
        /// <summary>
        /// Name of the attachment filter
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of Attachment
        /// </summary>
        /// <param name="name"></param>
        public Attachment(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Converts one or more attachments to a JSON object
        /// </summary>
        /// <param name="attachments"></param>
        /// <returns></returns>
        public static JObject ToObject(Attachment[] attachments)
        {
            JObject result = new JObject();

            foreach (Attachment attachment in attachments)
            {
                result[attachment.Name] = "true";
            }

            return result;
        }
    }
}
