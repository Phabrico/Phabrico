using Phabrico.Http.Response;
using System.Collections.Generic;
using System.Reflection;

namespace Phabrico.Http
{
    /// <summary>
    /// Phabrico contains a lot of reflection code.
    /// Results retrieved from this reflection code is cached for performance reasons.
    /// This class represents a cached instance for a controller class
    /// </summary>
    public class CachedControllerInfo
    {
        /// <summary>
        /// HtmlViewPage look per (GET) controller methods.
        /// E.g. some controller methods show the navigator menu, others don't
        /// </summary>
        public Dictionary<MethodInfo, HtmlViewPage.ContentOptions> ControllerMethods { get; set; }

        /// <summary>
        /// REST-API-parameters which were passed to the controller method
        /// </summary>
        public string[] ControllerParameters { get; set; }

        /// <summary>
        /// Constructor reference of the controller
        /// </summary>
        public ConstructorInfo ControllerConstructor { get; set; }

        /// <summary>
        /// URL which invokes this controller
        /// </summary>
        public string ControllerUrl { get; set; }

        /// <summary>
        /// Alias-URL which invokes this controller
        /// </summary>
        public string ControllerUrlAlias { get; set; }
    }
}
