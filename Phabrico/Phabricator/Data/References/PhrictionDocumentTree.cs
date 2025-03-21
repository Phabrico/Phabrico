﻿using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Phabrico.Data.References
{
    /// <summary>
    /// Represents the hierarchical tree of the underlying Phriction documents
    /// </summary>
    public class PhrictionDocumentTree : HashSet<PhrictionDocumentTree>
    {
        /// <summary>
        /// Phriction document as represented by the current tree item
        /// </summary>
        public Phabricator.Data.Phriction Data { get; set; }
        
        /// <summary>
        /// Underlying Phriction documents
        /// </summary>
        public IEnumerable<Phabricator.Data.Phriction> Children
        {
            get
            {
                foreach (PhrictionDocumentTree childHierarchy in this.OrderBy(child => child.Data))
                {
                    yield return childHierarchy.Data;
                }
            }
        }

        /// <summary>
        /// Compare 2 Phriction documents
        /// </summary>
        /// <param name="phrictionLeft"></param>
        /// <param name="phrictionRight"></param>
        /// <returns></returns>
        public int Compare(Phabricator.Data.Phriction phrictionLeft, Phabricator.Data.Phriction phrictionRight)
        {
            return phrictionLeft.Name.CompareTo(phrictionRight);
        }

        /// <summary>
        /// Translates the current tree item to HTML
        /// </summary>
        /// <param name="rootDocumentPath"></param>
        /// <returns></returns>
        public string ToHTML(string rootDocumentPath)
        {
            string html = "<ul class='phui-document-hierarchy'><li>";

            if (this.Any())
            {
                html += "<ul class='phui-document-hierarchy-item'>";

                foreach (PhrictionDocumentTree childHierarchy in this.OrderBy(child => child.Data))
                {
                    html += "<li>";

                    if (childHierarchy.Any())
                    {
                        string url = GetURL(childHierarchy.Data, rootDocumentPath).ToString();
                        string description = GetDescription(childHierarchy.Data).ToString();
                        if (string.IsNullOrWhiteSpace(description))
                        {
                            description = url;
                        }

                        html += string.Format("<span><a href='{0}'>{1}</a></span>", url, description);
                        html += "<ul class='phui-document-hierarchy-item'>";

                        foreach (PhrictionDocumentTree grandchildHierarchy in childHierarchy.OrderBy(child => child.Data))
                        {
                            html += "<li>";

                            if (grandchildHierarchy.Any())
                            {
                                url = GetURL(grandchildHierarchy.Data, rootDocumentPath).ToString();
                                description = GetDescription(grandchildHierarchy.Data).ToString();
                                if (string.IsNullOrWhiteSpace(description))
                                {
                                    description = url;
                                }

                                html += string.Format("<span><a href='{0}'>{1}</a></span>", url, description);
                                html += "<ul class='phui-document-hierarchy-item'>";
                                foreach (PhrictionDocumentTree greatgrandchildHierarchy in grandchildHierarchy.OrderBy(grandchild => grandchild.Data))
                                {
                                    url = GetURL(greatgrandchildHierarchy.Data, rootDocumentPath).ToString();
                                    description = GetDescription(greatgrandchildHierarchy.Data).ToString();
                                    if (string.IsNullOrWhiteSpace(description))
                                    {
                                        description = url;
                                    }

                                    html += string.Format("<li><a href='{0}'>{1}</a></li>", url, description);
                                }

                                html += "</ul>";
                            }
                            else
                            {
                                url = GetURL(grandchildHierarchy.Data, rootDocumentPath).ToString();
                                description = GetDescription(grandchildHierarchy.Data).ToString();
                                if (string.IsNullOrWhiteSpace(description))
                                {
                                    description = url;
                                }

                                html += string.Format("<a href='{0}'>{1}</a>", url, description);
                            }

                            html += "</li>";
                        }

                        html += "</ul>";
                    }
                    else
                    {
                        string url = GetURL(childHierarchy.Data, rootDocumentPath).ToString();
                        string description = GetDescription(childHierarchy.Data).ToString();
                        if (string.IsNullOrWhiteSpace(description))
                        {
                            description = url;
                        }

                        html += string.Format("<a href='{0}'>{1}</a>", url, description);
                    }

                    html += "</li>";
                }

                html += "</ul>";
            }

            html += "</li></ul>";

            return html;
        }

        /// <summary>
        /// Returns the title of a given Phriction document
        /// </summary>
        /// <param name="phrictionDocument"></param>
        /// <returns></returns>
        private string GetDescription(Phabricator.Data.Phriction phrictionDocument)
        {
            if (phrictionDocument != null)
            {
                return phrictionDocument.Name;
            }

            return "";
        }

        /// <summary>
        /// Returns the URL to a given Phriction document
        /// </summary>
        /// <param name="phrictionDocument"></param>
        /// <param name="rootDocumentPath"></param>
        /// <returns></returns>
        private string GetURL(Phabricator.Data.Phriction phrictionDocument, string rootDocumentPath)
        {
            string url;

            if (phrictionDocument != null)
            {
                string encodedUrl = phrictionDocument.Path;
                if (encodedUrl.EndsWith("/"))
                {
                    encodedUrl = HttpUtility.UrlEncode(encodedUrl.TrimEnd('/')) + "/";
                }
                else
                {
                    encodedUrl = HttpUtility.UrlEncode(encodedUrl);
                }

                url = encodedUrl.Replace("%2f", "/")   // make sure we don't have encoded '/' characters
                                .Replace("%2F", "/");  //
                if (rootDocumentPath.Any() && url.StartsWith(rootDocumentPath))
                {
                    url = url.Substring(rootDocumentPath.Length);
                }

                return ("w/" + url).Replace("//", "/");
            }

            return "";
        }
    }
}
