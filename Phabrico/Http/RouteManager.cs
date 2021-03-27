using System.Collections.Generic;

namespace Phabrico.Http
{
    /// <summary>
    /// Represents a class which manages some internal Phabrico URL routing
    /// </summary>
    public class RouteManager
    {
        /// <summary>
        /// collection of global URL aliases
        /// A global URL alias is a URL which is immediately redirected to another URL without any reloading or reflection.
        /// The dictionary key represents the alias itself.
        /// The dictionary value represents the URL to be redirected to.
        /// </summary>
        private static Dictionary<string, string> globalUrlAliases = new Dictionary<string, string>()
        {
            { "/user/info/",    "/maniphest/opentasks/peruser/" },
            { "/project/info/", "/maniphest/opentasks/perproject/" },
        };

        /// <summary>
        /// Converts an alias URL to a real URL.
        /// In case the given URL is not an alias URL, the given URL itself will be returned
        /// In case the alias contains some parameters, the parameters will also be assigned to the redirected URL.
        /// </summary>
        /// <param name="urlAlias"></param>
        /// <returns></returns>
        public static string GetInternalURL(string urlAlias)
        {
            foreach (string globalUrlAlias in globalUrlAliases.Keys)
            {
                if (urlAlias.StartsWith(globalUrlAlias))
                {
                    return globalUrlAliases[globalUrlAlias] + urlAlias.Substring(globalUrlAlias.Length);
                }
            }

            return urlAlias;
        }
    }
}