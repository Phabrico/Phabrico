using System;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents an attribute to identify methods as Phabrico controller methods.
    /// These methods are executed by means of a url in a browser
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class UrlControllerAttribute : Attribute
    {
        /// <summary>
        /// Secondary URL that should trigger the given Phabrico controller method
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Layout parameter for a linked ViewPage
        /// </summary>
        public Http.Response.HtmlViewPage.ContentOptions HtmlViewPageOptions { get; set; } = Http.Response.HtmlViewPage.ContentOptions.Default;

        /// <summary>
        /// If set to false, the webserver will never cache the action
        /// </summary>
        public bool ServerCache { get; set; } = true;

        /// <summary>
        /// If set to true, the controller method can be executed without the need to be logged in
        /// </summary>
        public bool Unsecure { get; set; } = false;

        /// <summary>
        /// Primary URL that should trigger the given Phabrico controller method
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// If set to true, the Phabrico controller method will be executed in an impersonated user (=Logged on Windows user)
        /// </summary>
        public bool IntegratedWindowsSecurity { get; set; } = false;
    }
}
