using System;
using System.Text;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Controller for showing Phabrico exceptions to the web browser
    /// </summary>
    public class Exception : Controller
    {
        private static string sourceLocationInPDB = null;

        /// <summary>
        /// This method is fired as soon as an uncaught exception is thrown and will show an error message and a stack trace
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/exception", Unsecure = true)]
        public void HttpGetException(Http.Server httpServer, Browser browser, ref Http.Response.HtmlViewPage htmlViewPage, string[] parameters, string parameterActions)
        {
            string base64ExceptionName = parameters[2].Substring("?data=".Length).Split('/')[0];
            string base64ExceptionMessage = parameters[0];
            string base64ExceptionStackTrace = parameters[1];
            
            string exceptionName = UTF8Encoding.UTF8.GetString(Convert.FromBase64String(base64ExceptionName));
            string exceptionMessage = UTF8Encoding.UTF8.GetString(Convert.FromBase64String(base64ExceptionMessage));
            string exceptionStackTrace = UTF8Encoding.UTF8.GetString(Convert.FromBase64String(base64ExceptionStackTrace));

            if (sourceLocationInPDB == null)
            {
                try
                {
                    // throw some exception to retrieve the current stacktrace
                    throw new System.Exception();
                }
                catch (System.Exception testException)
                {
                    string currentSourceFilePath = GetType().FullName.Replace(".", "\\\\");
                    Match matchSourceDirectory = RegexSafe.Match(testException.StackTrace, @".* in (.*)\\" + currentSourceFilePath + ".cs", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (matchSourceDirectory.Success)
                    {
                        sourceLocationInPDB = matchSourceDirectory.Groups[1].Value;
                    }
                    else
                    {
                        sourceLocationInPDB = "";
                    }
                }
            }

            if (string.IsNullOrEmpty(sourceLocationInPDB) == false)
            {
                exceptionStackTrace = exceptionStackTrace.Replace(sourceLocationInPDB, "");
            }

            htmlViewPage.SetText("EXCEPTION-NAME", exceptionName);
            htmlViewPage.SetText("EXCEPTION-MESSAGE", exceptionMessage);
            htmlViewPage.SetText("EXCEPTION-STACKTRACE", exceptionStackTrace, Http.Response.HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
        }
    }
}
