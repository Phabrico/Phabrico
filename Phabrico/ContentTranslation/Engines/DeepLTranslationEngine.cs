using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace Phabrico.ContentTranslation.Engines
{
    /// <summary>
    /// Translation engine for https://www.deepl.com
    /// </summary>
    [Translation(Name = "deepl")]
    public class DeepLTranslationEngine : TranslationEngine
    {
        public override bool IsRemoteTranslationService { get; } = true;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiKey">API key to be used for online translation</param>
        public DeepLTranslationEngine(string apiKey)
            : base(apiKey)
        {
        }

        /// <summary>
        /// Translates some content from one language to another language
        /// </summary>
        /// <param name="sourceLanguage">Language of content</param>
        /// <param name="destinationLanguage">Language of translated content</param>
        /// <param name="content">Content to be translated</param>
        /// <param name="previouslyTranslatedContent">Translated content. Can be empty if this is the first translation time or it can contain a translation from a previous call</param>
        /// <param name="origin">Location where the content can be found (e.g. a token or a url)</param>
        /// <returns>Translated content</returns>
        protected override string Translate(string sourceLanguage, string destinationLanguage, string content, string previouslyTranslatedContent, string origin)
        {
            string url = "https://api-free.deepl.com/v2/translate";
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Method = "POST";
            httpWebRequest.Timeout = 30000;
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";
            httpWebRequest.UserAgent = "Phabrico";

            string postData = string.Format("auth_key={0}&text={1}&target_lang={2}&source_lang={3}&tag_handling=xml", 
                                              Uri.EscapeDataString(APIKey),
                                              Uri.EscapeDataString(content),
                                              Uri.EscapeDataString(destinationLanguage),
                                              Uri.EscapeDataString(sourceLanguage)
                                           );


            using (Stream httpWebRequestStream = httpWebRequest.GetRequestStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(httpWebRequestStream))
                {
                    streamWriter.Write(postData);
                }
            }

            try
            {
                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    using (Stream httpWebResponseStream = httpWebResponse.GetResponseStream())
                    {
                        using (StreamReader streamReader = new StreamReader(httpWebResponseStream, Encoding.UTF8))
                        {
                            string jsonResult = streamReader.ReadToEnd();
                            JObject json = JsonConvert.DeserializeObject(jsonResult) as JObject;
                            if (json == null) return null;
                            return (string)json["translations"][0]["text"];
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                WebException webException = exception as WebException;
                if (webException != null && webException.Response != null)
                {
                    using (WebResponse response = webException.Response)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(dataStream))
                            {
                                string details = reader.ReadToEnd();
                                if (string.IsNullOrWhiteSpace(details))
                                {
                                    details = webException.Message;
                                }

                                throw new WebException(details);
                            }
                        }
                    }
                }

                throw;
            }
        }
    }
}
