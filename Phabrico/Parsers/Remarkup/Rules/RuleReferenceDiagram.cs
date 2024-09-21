using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for Diagram objects
    /// </summary>
    [RuleXmlTag("DIAG")]
    public class RuleReferenceDiagram : RemarkupRule
    {
        public int DiagramID { get; private set; }

        /// <summary>
        /// Creates a copy of the current RuleReferenceDiagram
        /// </summary>
        /// <returns></returns>
        public override RemarkupRule Clone()
        {
            RuleReferenceDiagram copy = base.Clone() as RuleReferenceDiagram;
            if (copy != null)
            {
                copy.DiagramID = DiagramID;
            }
            return copy;
        }

        /// <summary>
        /// Converts Remarkup encoded text into HTML
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if the content could successfully be converted</returns>
        public override bool ToHTML(Storage.Database database, Browser browser, string url, ref string remarkup, out string html)
        {
            html = "";
            Match match = RegexSafe.Match(remarkup, @"^{DIAG(TRAN)?(-?[0-9]+)([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            Storage.Account accountStorage = new Storage.Account();
            Storage.Content content = new Storage.Content(database);
            Storage.Diagram diagramStorage = new Storage.Diagram();
            Storage.Stage stageStorage = new Storage.Stage();

            Account existingAccount = accountStorage.WhoAmI(database, browser);

            bool isTranslatedObject = match.Groups[1].Success;
            DiagramID = Int32.Parse(match.Groups[2].Value);
            Dictionary<string, string> diagramObjectOptions = match.Groups[3]
                                                                         .Value
                                                                         .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                         .ToDictionary(key => key.Split('=')
                                                                                                 .FirstOrDefault()
                                                                                                 .ToLower()
                                                                                                 .Trim(),
                                                                                       value => value.Contains('=') ? value.Split('=')[1] : "");

            bool showImageAsLink = false;
            string layout;
            if (diagramObjectOptions.TryGetValue("layout", out layout) == false)
            {
                diagramObjectOptions["layout"] = "left";
            }
            else
            if (layout.ToLower().Equals("link"))
            {
                showImageAsLink = true;
            }

            Phabricator.Data.Diagram diagramObject = null;
            if (isTranslatedObject)
            {
                string token = string.Format("PHID-OBJECT-{0}", DiagramID.ToString().PadLeft(18, '0'));
                Storage.Content.Translation translation = content.GetTranslation(token, browser.Session.Locale);
                if (translation != null)
                {
                    var diagramObjectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(translation.TranslatedRemarkup) as Newtonsoft.Json.Linq.JObject;
                    if (diagramObjectInfo != null)
                    {
                        diagramObject = new Phabricator.Data.Diagram();
                        diagramObject.ID = DiagramID;
                        diagramObject.Token = token;

                        string base64EncodedData = (string)diagramObjectInfo["Data"];
                        byte[] buffer = new byte[(int)(base64EncodedData.Length * 0.8)];
                        using (MemoryStream ms = new MemoryStream(buffer))
                        using (Phabrico.Parsers.Base64.Base64EIDOStream base64EIDOStream = new Parsers.Base64.Base64EIDOStream(base64EncodedData))
                        {
                            base64EIDOStream.CopyTo(ms);
                            Array.Resize(ref buffer, (int)base64EIDOStream.Length);
                        }

                        diagramObject.Data = buffer;
                        diagramObject.Size = buffer.Length;
                        diagramObject.FileName = (string)diagramObjectInfo["FileName"];
                    }
                }
            }
            else
            {
                diagramObject = diagramStorage.GetByID(database, DiagramID, true);
                if (diagramObject == null)
                {
                    diagramObject = stageStorage.Get<Phabricator.Data.Diagram>(database, browser.Session.Locale, Phabricator.Data.Diagram.Prefix, DiagramID, false);

                }
            }
            if (diagramObject != null)
            {
                LinkedPhabricatorObjects.Add(diagramObject);

                html = ProcessImageFile(database, browser, diagramObjectOptions, existingAccount, diagramObject, DiagramID, isTranslatedObject);
            }
            else
            {
                return false;
            }

            remarkup = remarkup.Substring(match.Length);

            Length = match.Length;

            return true;
        }

        /// <summary>
        /// Generates remarkup content
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="innerText">Text between XML opening and closing tags</param>
        /// <param name="attributes">XML attributes</param>
        /// <returns>Remarkup content, translated from the XML</returns>
        internal override string ConvertXmlToRemarkup(Storage.Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            Match match = RegexSafe.Match(innerText, @"^{DIAG(TRAN)?(-?[0-9]+)([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return innerText;

            DiagramID = Int32.Parse(match.Groups[2].Value);

            Storage.Stage stageStorage = new Storage.Stage();
            Storage.Diagram diagramStorage = new Storage.Diagram();
            Phabricator.Data.Diagram diagramObject = diagramStorage.GetByID(database, DiagramID, true);
            if (diagramObject == null)
            {
                diagramObject = stageStorage.Get<Phabricator.Data.Diagram>(database, browser.Session.Locale, Phabricator.Data.Diagram.Prefix, DiagramID, false);
            }
            else
            {
                diagramObject = diagramStorage.GetByID(database, DiagramID, false);
            }


            if (diagramObject != null)
            {
                // create clone of diagram
                Phabricator.Data.Diagram clonedDiagramObject = new Phabricator.Data.Diagram();
                clonedDiagramObject.Data = diagramObject.Data;
                clonedDiagramObject.DateModified = DateTimeOffset.MinValue;
                clonedDiagramObject.FileName = diagramObject.FileName;
                clonedDiagramObject.Language = browser.Session.Locale;

                stageStorage.Create(database, browser, clonedDiagramObject);

                // mark file object as 'unreviewed'
                Storage.Content content = new Storage.Content(database);
                string clonedFileObjectTitle = string.Format("DIAG{0} ({1})", diagramObject.ID, browser.Session.Locale);
                content.AddTranslation(clonedDiagramObject.Token, browser.Session.Locale, clonedFileObjectTitle, "");

                // modify innerText
                innerText = innerText.Substring(0, match.Groups[2].Index)
                          + clonedDiagramObject.ID
                          + innerText.Substring(match.Groups[2].Index + match.Groups[2].Length);
            }

            return innerText;
        }

        /// <summary>
        /// Returns the HTML code for an image-typed file
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="diagramObjectOptions">Remarkup options for the fiven file</param>
        /// <param name="existingAccount">Current Phabricator account</param>
        /// <param name="diagramObject">The file data as found in the SQLite database</param>
        /// <param name="diagramObjectID">ID of the fileObject</param>
        /// <param name="isTranslatedImage">True if file is stored in ContentTranslation (e.g. a translated diagram)</param>
        /// <returns>img HTML tag</returns>
        private string ProcessImageFile(Storage.Database database, Browser browser, Dictionary<string, string> diagramObjectOptions, Account existingAccount, Phabricator.Data.Diagram diagramObject, int diagramObjectID, bool isTranslatedImage)
        {
            Storage.Diagram diagramStorage = new Storage.Diagram();
            string imgClass = "";

            if (existingAccount != null && existingAccount.Theme == "dark" && existingAccount.Parameters.DarkenBrightImages != Account.DarkenImageStyle.Disabled)
            {
                // check if diagram content was also loaded, if not load it
                if (diagramObject.DataStream == null)
                {
                    Storage.Stage stageStorage = new Storage.Stage();
                    diagramObject = stageStorage.Get<Phabricator.Data.Diagram>(database, browser.Session.Locale, Phabricator.Data.Diagram.Prefix, diagramObjectID, true);
                    if (diagramObject == null)
                    {
                        // diagram was not loaded from staging area, so content was not loaded -> load diagram content
                        diagramObject = diagramStorage.GetByID(database, diagramObjectID, false);
                    }
                }
            }

            // check if diagram content was also loaded, if not load it
            if (diagramObject.DataStream == null)
            {
                Storage.Stage stageStorage = new Storage.Stage();
                diagramObject = stageStorage.Get<Phabricator.Data.Diagram>(database, browser.Session.Locale, Phabricator.Data.Diagram.Prefix, diagramObjectID, true);
                if (diagramObject == null)
                {
                    // diagram was not loaded from staging area, so content was not loaded -> load diagram content
                    diagramObject = diagramStorage.GetByID(database, diagramObjectID, false);
                }
            }

            string imgStyles = "";
            string imgLocatorClasses = "";
            string imgLocatorStyles = "";
            string alternativeText = "";
            string clickAction = "";

            if (diagramObjectOptions.ContainsKey("size") == false &&
                diagramObjectOptions.ContainsKey("width") == false &&
                diagramObjectOptions.ContainsKey("height") == false)
            {
                // if no image size is configured, set it default to full
                diagramObjectOptions["size"] = "full";
            }

            bool isFullSize = false;
            foreach (string style in diagramObjectOptions.Keys)
            {
                switch (style)
                {
                    case "float":
                        if (diagramObjectOptions["layout"].ToLower().Equals("center"))
                        {
                            imgLocatorStyles += "display:block; margin-left: auto; margin-right: auto;";
                        }
                        else
                        {
                            imgLocatorStyles += string.Format("float: {0};", diagramObjectOptions["layout"]);
                        }
                        break;

                    case "layout":
                        if (diagramObjectOptions.ContainsKey("float") == false)
                        {
                            if (diagramObjectOptions["layout"].ToLower().Equals("center"))
                            {
                                imgLocatorStyles += "margin-left: auto; margin-right: auto;";
                            }
                            else
                            if (diagramObjectOptions["layout"].ToLower().Equals("right"))
                            {
                                imgLocatorStyles += "margin-left: auto;";
                            }
                            else
                            if (diagramObjectOptions["layout"].ToLower().Equals("inline"))
                            {
                                imgLocatorStyles += "margin-top: 0px; margin-bottom: -20px;";
                            }
                        }
                        break;

                    case "size":
                        if (diagramObjectOptions[style].ToLower().Equals("full"))
                        {
                            isFullSize = true;
                            imgLocatorClasses += "full-size";
                        }
                        break;

                    default:
                        break;
                }
            }

            // check if alternative text should be displayed
            if (diagramObjectOptions.TryGetValue("alt", out alternativeText) && string.IsNullOrWhiteSpace(alternativeText) == false)
            {
                alternativeText = " alt=\"" + alternativeText.Trim('"') + "\"";
            }

            // check if we need to add an 'Edit-button' to the image (which will be displayed when mouse-hovering over the image)
            bool isEditable = false;
            string btnEditImageHtml = "...";

            if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet"))
                && (browser.HttpServer.Customization.HidePlugins.ContainsKey("DiagramsNet") == false
                    || browser.HttpServer.Customization.HidePlugins["DiagramsNet"] == false
                    )
               )
            {
                isEditable = true;
                imgClass += " diagram";
                btnEditImageHtml = string.Format("<a class='button' href='diagrams.net/DIAG{0}{1}' onclick='javascript:sessionStorage[\"originURL\"] = document.location.href; return true;'>" +
                                                      "<span class='phui-font-fa fa-sitemap'></span>" +
                                                 "</a>",
                    isTranslatedImage ? "TRAN" : "",
                    diagramObject.ID);
            }

            // return result
            if (isEditable)
            {
                if (isFullSize)
                {
                    /* TODO: margin-left aanpassen voor image-locator */
                    return string.Format("<div class='image-locator allow-full-screen {9}' style='{8}'>\n" +
                                         "  <div class='image-container'>\n" +
                                         "     <img rel='{0}' src='diagram/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>\n" +
                                         "     {7}\n" +
                                         "  </div>" +
                                         "</div>",
                        diagramObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        diagramObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction,
                        btnEditImageHtml,
                        imgLocatorStyles,
                        imgLocatorClasses);
                }
                else
                {
                    return string.Format("<div class='image-locator {9}' style='{8}'>\n" +
                                         "  <div class='image-container'>\n" +
                                         "     <img rel='{0}' src='diagram/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>\n" +
                                         "     {7}\n" +
                                         "  </div>" +
                                         "</div>",
                        diagramObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        diagramObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction,
                        btnEditImageHtml,
                        imgLocatorStyles,
                        imgLocatorClasses);
                }
            }
            else
            {
                if (isFullSize)
                {
                    return string.Format("<div class='image-locator allow-full-screen {7}' style='{6}'>\n" +
                                         "  <div class='image-container'>\n" +
                                         "     <img rel='{0}' src='diagram/data/{1}{2}/' class='{3}' style='{4}'{5} onload='imageLoaded(this)'>\n" +
                                         "  </div>" +
                                         "</div>",
                        diagramObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        diagramObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        imgLocatorStyles,
                        imgLocatorClasses);
                }
                else
                {
                    return string.Format(@"<img rel='{0}' src='diagram/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>",
                        diagramObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        diagramObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction);
                }
            }
        }
    }
}
