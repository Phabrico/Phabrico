using Phabrico.Miscellaneous;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Controller for showing Phabrico exceptions to the web browser
    /// </summary>
    public class Exception : Controller
    {
        /// <summary>
        /// Contains the path of the source from where Phabrico.exe was compiled.
        /// This variable is initialized during the first HTTP exception request
        /// by reading out the stacktrace of a intentional generated exception.
        /// </summary>
        private static string sourceLocationInPDB = null;

        /// <summary>
        /// This method is fired as soon as an uncaught exception is thrown and will show an error message and a stack trace
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="httpMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/exception", Unsecure = true, HtmlViewPageOptions = Http.Response.HtmlViewPage.ContentOptions.HideGlobalTreeView | Http.Response.HtmlViewPage.ContentOptions.HideHeader)]
        public void HttpGetException(Http.Server httpServer, ref Http.Response.HttpMessage httpMessage, string[] parameters, string parameterActions)
        {
            try
            {
                Http.Response.HtmlViewPage htmlViewPage = httpMessage as Http.Response.HtmlViewPage;

                string[] parameterArray;
                if (parameters.Any())
                {
                    parameterArray = parameters[0].Substring("?data=".Length).Split('/');
                }
                else
                {
                    parameterArray = parameterActions.Substring("data=".Length).Split('/');
                }

                string base64ExceptionName = parameterArray[0];
                string base64ExceptionMessage = parameterArray[1];
                string base64ExceptionStackTrace = parameterArray[2];

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

                if (htmlViewPage != null)
                {
                    htmlViewPage.SetText("EXCEPTION-NAME", exceptionName);
                    htmlViewPage.SetText("EXCEPTION-MESSAGE", exceptionMessage);
                    htmlViewPage.SetText("EXCEPTION-STACKTRACE", exceptionStackTrace, Http.Response.HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                }

                httpMessage = htmlViewPage;
            }
            catch
            {
                httpMessage = new Http.Response.HttpInternalServerError(httpServer, browser, browser.Request.RawUrl);
            }
        }
    }
}
