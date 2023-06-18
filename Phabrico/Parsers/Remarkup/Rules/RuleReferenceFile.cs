using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for File objects
    /// </summary>
    [RuleXmlTag("F")]
    public class RuleReferenceFile : RemarkupRule
    {
        public int FileID { get; private set; }

        /// <summary>
        /// Creates a copy of the current RuleReferenceFile
        /// </summary>
        /// <returns></returns>
        public override RemarkupRule Clone()
        {
            RuleReferenceFile copy = base.Clone() as RuleReferenceFile;
            if (copy != null)
            {
                copy.FileID = FileID;
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
            Match match = RegexSafe.Match(remarkup, @"^{F(TRAN)?(-?[0-9]+)([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            Storage.Account accountStorage = new Storage.Account();
            Storage.Content content = new Storage.Content(database);
            Storage.File fileStorage = new Storage.File();
            Storage.Stage stageStorage = new Storage.Stage();

            Account existingAccount = accountStorage.WhoAmI(database, browser);

            bool isTranslatedObject = match.Groups[1].Success;
            FileID = Int32.Parse(match.Groups[2].Value);
            Dictionary<string, string> fileObjectOptions = match.Groups[3]
                                                                         .Value
                                                                         .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                         .ToDictionary(key => key.Split('=')
                                                                                                 .FirstOrDefault()
                                                                                                 .ToLower()
                                                                                                 .Trim(),
                                                                                       value => value.Contains('=') ? value.Split('=')[1] : "");

            bool showImageAsLink = false;
            string layout;
            if (fileObjectOptions.TryGetValue("layout", out layout) == false)
            {
                fileObjectOptions["layout"] = "left";
            }
            else
            if (layout.ToLower().Equals("link"))
            {
                showImageAsLink = true;
            }

            Phabricator.Data.File fileObject = null;
            if (isTranslatedObject)
            {
                string token = string.Format("PHID-OBJECT-{0}", FileID.ToString().PadLeft(18, '0'));
                Storage.Content.Translation translation = content.GetTranslation(token, browser.Session.Locale);
                if (translation != null)
                {
                    Newtonsoft.Json.Linq.JObject fileObjectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(translation.TranslatedRemarkup) as Newtonsoft.Json.Linq.JObject;
                    if (fileObjectInfo != null)
                    {
                        fileObject = new Phabricator.Data.File();
                        fileObject.ID = FileID;
                        fileObject.Token = token;

                        string base64EncodedData = (string)fileObjectInfo["Data"];
                        byte[] buffer = new byte[(int)(base64EncodedData.Length * 0.8)];
                        using (MemoryStream ms = new MemoryStream(buffer))
                        using (Phabrico.Parsers.Base64.Base64EIDOStream base64EIDOStream = new Parsers.Base64.Base64EIDOStream(base64EncodedData))
                        {
                            base64EIDOStream.CopyTo(ms);
                            Array.Resize(ref buffer, (int)base64EIDOStream.Length);
                        }

                        fileObject.Data = buffer;
                        fileObject.Size = buffer.Length;
                        fileObject.Properties = (string)fileObjectInfo["Properties"];
                        fileObject.FileName = (string)fileObjectInfo["FileName"];
                    }
                }
            }
            else
            {
                fileObject = fileStorage.GetByID(database, FileID, true);
                if (fileObject == null)
                {
                    fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, FileID, false);

                }
            }

            if (fileObject != null)
            {
                LinkedPhabricatorObjects.Add(fileObject);

                if (fileObject.FileType == Phabricator.Data.File.FileStyle.Video)
                {
                    KeyValuePair<string, string> mediaParameter = fileObjectOptions.FirstOrDefault(parameter => parameter.Key.Equals("media", StringComparison.OrdinalIgnoreCase));
                    if (mediaParameter.Key != null)
                    {
                        if (mediaParameter.Value.Equals("audio", StringComparison.OrdinalIgnoreCase))
                        {
                            fileObject.FileType = Phabricator.Data.File.FileStyle.Audio;
                        }
                    }
                }

                switch (fileObject.FileType)
                {
                    case Phabricator.Data.File.FileStyle.Audio:
                        html = ProcessAudioFile(fileObjectOptions, fileObject);
                        break;

                    case Phabricator.Data.File.FileStyle.Image:
                        if (showImageAsLink)
                        {
                            fileObject.FontAwesomeIcon = "fa-file-picture-o";

                            string imageName;
                            if (fileObjectOptions.TryGetValue("name", out imageName))
                            {
                                fileObject.FileName = imageName;
                            }

                            html = ProcessGenericFile(fileObjectOptions, fileObject, browser);
                        }
                        else
                        {
                            html = ProcessImageFile(database, browser, fileObjectOptions, existingAccount, fileObject, FileID, isTranslatedObject);
                        }
                        break;

                    case Phabricator.Data.File.FileStyle.Other:
                        html = ProcessGenericFile(fileObjectOptions, fileObject, browser);
                        break;

                    case Phabricator.Data.File.FileStyle.Video:
                        html = ProcessVideoFile(fileObjectOptions, fileObject);
                        break;

                    default:
                        break;
                }
            }
            else
            if (isTranslatedObject == false && FileID > 0)
            {
                // ERROR: file object not found!
                // file object was not downloaded from Phabricator (or file object was not granted on Phabricator to be viewed by everybody)
                database.MarkFileObject(FileID, true, PhabricatorObjectToken);

                // show replacement for missing file
                html = string.Format("<div class=\"inaccessible-file\" title=\"{1}\"><div><div>{{F{0}}}</div></div></div>",
                                        FileID,
                                        HttpUtility.HtmlEncode(
                                            Locale.TranslateText("File inaccessible", browser.Session.Locale)
                                        )
                                    );
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
            Match match = RegexSafe.Match(innerText, @"^{F(TRAN)?(-?[0-9]+)([^}]*)}", RegexOptions.Singleline);
            FileID = Int32.Parse(match.Groups[2].Value);

            Storage.Stage stageStorage = new Storage.Stage();
            Storage.File fileStorage = new Storage.File();
            Phabricator.Data.File fileObject = fileStorage.GetByID(database, FileID, true);
            if (fileObject == null)
            {
                fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, FileID, false);
                if (fileObject.ContentType.Equals("image/drawio"))
                {
                    fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, FileID, true);
                }
            }
            else
            if (fileObject.ContentType.Equals("image/drawio"))
            {
                fileObject = fileStorage.GetByID(database, FileID, false);
            }


            if (fileObject != null)
            {
                if (fileObject.ContentType.Equals("image/drawio"))
                {
                    // create clone of diagram
                    Phabricator.Data.File clonedFileObject = new Phabricator.Data.File();
                    clonedFileObject.Data = fileObject.Data;
                    clonedFileObject.DateModified = DateTimeOffset.MinValue;
                    clonedFileObject.TemplateFileName = "{0}";
                    clonedFileObject.ImagePropertyPixelHeight = fileObject.ImagePropertyPixelHeight;
                    clonedFileObject.ImagePropertyPixelWidth = fileObject.ImagePropertyPixelWidth;
                    clonedFileObject.OriginalID = fileObject.ID;
                    clonedFileObject.Language = browser.Session.Locale;

                    stageStorage.Create(database, browser, clonedFileObject);

                    // mark file object as 'unreviewed'
                    Storage.Content content = new Storage.Content(database);
                    string clonedFileObjectTitle = string.Format("F(TRAN)?{0} ({1})", fileObject.ID, browser.Session.Locale);
                    content.AddTranslation(clonedFileObject.Token, browser.Session.Locale, clonedFileObjectTitle, "");

                    // modify innerText
                    innerText = innerText.Substring(0, match.Groups[2].Index)
                              + clonedFileObject.ID
                              + innerText.Substring(match.Groups[2].Index + match.Groups[2].Length);
                }
            }

            return innerText;
        }

        /// <summary>
        /// Returns the number of different colors in the center of the bitmap.
        /// For the calculation of the bitmap, the bitmap is divided in 3x3 parts.
        /// The middle part is the center
        /// </summary>
        /// <param name="bitmap">Bitmap to be checked</param>
        /// <returns>
        /// Ordered List of KeyValuePairs in which key is the RGB value and value is the
        /// number of pixels found for this given color. The list itself is ordered, so that the color
        /// which appears most in the center is the first color in the list
        /// </returns>
        private List<KeyValuePair<uint, int>> CountNumberOfColorsInCenter(Bitmap bitmap)
        {
            Dictionary<uint, int> centerColorCount = new Dictionary<uint, int>();
            int centerSize = 100;
            int deltaWidth = 0;
            int deltaHeight = 0;
            int centerWidth = bitmap.Width / 3;

            if (centerWidth > centerSize)
            {
                deltaWidth = centerWidth - centerSize;
                centerWidth = centerSize;
            }
            int centerHeight = bitmap.Height / 3;
            if (centerHeight > centerSize)
            {
                deltaHeight = centerHeight - centerSize;
                centerHeight = centerSize;
            }
            for (int x = bitmap.Width / 3 + deltaWidth; x < bitmap.Width / 3 + deltaWidth + centerWidth; x++)
            {
                for (int y = bitmap.Height / 3 + deltaHeight; y < bitmap.Height / 3 + deltaHeight + centerHeight; y++)
                {
                    int count;
                    uint colorValue = (uint)bitmap.GetPixel(x, y).ToArgb();
                    if (centerColorCount.TryGetValue(colorValue, out count) == false)
                    {
                        count = 0;
                    }

                    centerColorCount[colorValue] = count + 1;
                }
            }

            return centerColorCount.OrderByDescending(kvp => kvp.Value)
                                   .ToList();
        }

        /// <summary>
        /// Returns the number of different colors in the 4 corners of the bitmap.
        /// For each corner, 10x10 pixels will be checked
        /// </summary>
        /// <param name="bitmap">Bitmap to be checked</param>
        /// <returns>Dictionary where key is the RGB value and value is the number of pixels found for this given color</returns>
        private Dictionary<uint, int> CountNumberOfColorsInCorners(Bitmap bitmap)
        {
            Dictionary<uint, int> boundaryColorCount = new Dictionary<uint, int>();

            foreach (int x in new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, bitmap.Width - 10, bitmap.Width - 9, bitmap.Width - 8, bitmap.Width - 7, bitmap.Width - 6, bitmap.Width - 5, bitmap.Width - 4, bitmap.Width - 3, bitmap.Width - 2, bitmap.Width - 1 })
            {
                foreach (int y in new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, bitmap.Height - 10, bitmap.Height - 9, bitmap.Height - 8, bitmap.Height - 7, bitmap.Height - 6, bitmap.Height - 5, bitmap.Height - 4, bitmap.Height - 3, bitmap.Height - 2, bitmap.Height - 1 })
                {
                    int count;
                    uint colorValue = (uint)bitmap.GetPixel(x, y).ToArgb();
                    if (boundaryColorCount.TryGetValue(colorValue, out count) == false)
                    {
                        count = 0;
                    }

                    boundaryColorCount[colorValue] = count + 1;
                }
            }

            return boundaryColorCount;
        }

        /// <summary>
        /// Returns the HTML code for a audo-typed file
        /// </summary>
        /// <param name="fileObjectOptions">Remarkup options for the fiven file</param>
        /// <param name="fileObject">The file data as found in the SQLite database</param>
        /// <returns>audio HTML tag</returns>
        private string ProcessAudioFile(Dictionary<string,string> fileObjectOptions, Phabricator.Data.File fileObject)
        {
            string audioOptions = "";
            string audioPreload = "none";
            foreach (string option in fileObjectOptions.Keys)
            {
                switch (option)
                {
                    case "autoplay":
                        audioOptions += " autoplay=\"autoplay\"";
                        audioPreload = "auto";
                        break;

                    case "loop":
                        audioOptions += " loop=\"loop\"";
                        break;

                    default:
                        break;
                }
            }

            return string.Format(@"<audio controls='controls' preload='{1}'{2}><source src='file/data/{0}/' type='audio/x-wav'></audio><br>", fileObject.ID, audioPreload, audioOptions);
        }

        /// <summary>
        /// Returns the HTML code for an image-typed file
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="fileObjectOptions">Remarkup options for the fiven file</param>
        /// <param name="existingAccount">Current Phabricator account</param>
        /// <param name="fileObject">The file data as found in the SQLite database</param>
        /// <param name="fileObjectID">ID of the fileObject</param>
        /// <param name="isTranslatedImage">True if file is stored in ContentTranslation (e.g. a translated diagram)</param>
        /// <returns>img HTML tag</returns>
        private string ProcessImageFile(Storage.Database database, Browser browser, Dictionary<string, string> fileObjectOptions, Account existingAccount, Phabricator.Data.File fileObject, int fileObjectID, bool isTranslatedImage)
        {
            Storage.File fileStorage = new Storage.File();
            string imgClass = "";

            if (existingAccount != null && existingAccount.Theme == "dark" && existingAccount.Parameters.DarkenBrightImages != Account.DarkenImageStyle.Disabled)
            {
                // check if file content was also loaded, if not load it
                if (fileObject.DataStream == null)
                {
                    Storage.Stage stageStorage = new Storage.Stage();
                    fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, fileObjectID, true);
                    if (fileObject == null)
                    {
                        // file was not loaded from staging area, so content was not loaded -> load file content
                        fileObject = fileStorage.GetByID(database, fileObjectID, false);
                    }
                }

                try
                {
                    // load image and count the 16 boundary colors of the image (10x10 pixels for each corner) + center
                    fileObject.DataStream.Seek(0, SeekOrigin.Begin);
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(fileObject.DataStream))
                    {
                        if (bitmap.Height >= 10 && bitmap.Width >= 10)
                        {
                            // count colors in corners and center of bitmap
                            Dictionary<uint, int> boundaryColorCount = CountNumberOfColorsInCorners(bitmap);
                            List<KeyValuePair<uint, int>> centerColorCount = CountNumberOfColorsInCenter(bitmap);

                            // validate color count
                            if (boundaryColorCount.Keys.Any(color => (color & 0xFF000000) == 0) ||  // image is transparent
                                boundaryColorCount.Keys.All(color => color > 0xFFA0A0A0) ||         // 0xFFA0A0A0 = max brighest grey allowed
                                centerColorCount.FirstOrDefault().Key > 0xFFA0A0A0 ||               //
                                centerColorCount.Skip(1).FirstOrDefault().Key > 0xFFC0C0C0)         //
                            {
                                if (existingAccount.Parameters.DarkenBrightImages == Account.DarkenImageStyle.Extreme)
                                {
                                    // tag image with "darkness-extreme"
                                    imgClass = " darkness-extreme";
                                }
                                else
                                {
                                    // tag image with "darkness-moderate"
                                    imgClass = " darkness-moderate";
                                }
                            }
                        }
                    }
                }
                catch (System.Exception exception)
                {
                    Logging.WriteError(browser.Token.ID, "RuleReferenceFile.ProcessImageFile(1): " + exception.Message);
                }
            }

            if (fileObject.ContentType.Equals("image/drawio")) // set width of drawio images again, as the original stored width does not always seem to be right
            {
                // check if file content was also loaded, if not load it
                if (fileObject.DataStream == null)
                {
                    Storage.Stage stageStorage = new Storage.Stage();
                    fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, fileObjectID, true);
                    if (fileObject == null)
                    {
                        // file was not loaded from staging area, so content was not loaded -> load file content
                        fileObject = fileStorage.GetByID(database, fileObjectID, false);
                    }
                }

                try
                {
                    // load image and count the 16 boundary colors of the image (10x10 pixels for each corner) + center
                    fileObject.DataStream.Seek(0, SeekOrigin.Begin);
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(fileObject.DataStream))
                    {
                        // set width
                        fileObject.ImagePropertyPixelWidth = bitmap.Width;
                    }
                }
                catch (System.Exception exception)
                {
                    Logging.WriteError(browser.Token.ID, "RuleReferenceFile.ProcessImageFile(2): " + exception.Message);
                }
            }

            string imgStyles = "";
            string imgLocatorClasses = "";
            string imgLocatorStyles = "";
            string alternativeText = "";
            string clickAction = "";

            if (fileObjectOptions.ContainsKey("size") == false &&
                fileObjectOptions.ContainsKey("width") == false &&
                fileObjectOptions.ContainsKey("height") == false)
            {
                // if no image size is configured, set it default to full
                fileObjectOptions["size"] = "full";
            }

            bool isFullSize = false;
            foreach (string style in fileObjectOptions.Keys)
            {
                switch (style)
                {
                    case "float":
                        if (fileObjectOptions["layout"].ToLower().Equals("center"))
                        {
                            imgLocatorStyles += "display:block; margin-left: auto; margin-right: auto;";
                        }
                        else
                        {
                            imgLocatorStyles += string.Format("float: {0};", fileObjectOptions["layout"]);
                        }
                        break;

                    case "layout":
                        if (fileObjectOptions.ContainsKey("float") == false)
                        {
                            if (fileObjectOptions["layout"].ToLower().Equals("center"))
                            {
                                imgLocatorStyles += "margin-left: auto; margin-right: auto;";
                            }
                            else
                            if (fileObjectOptions["layout"].ToLower().Equals("right"))
                            {
                                imgLocatorStyles += "margin-left: auto;";
                            }
                            else
                            if (fileObjectOptions["layout"].ToLower().Equals("inline"))
                            {
                                imgLocatorStyles += "margin-top: 0px; margin-bottom: -20px;";
                            }
                        }
                        break;

                    case "width":
                        int width, maxWidth;
                        if (Int32.TryParse(fileObjectOptions[style], out width) == false)
                        {
                            width = 100;
                        }

                        if (fileObject.ImagePropertyPixelWidth > width)
                        {
                            maxWidth = fileObject.ImagePropertyPixelWidth;
                        }
                        else
                        {
                            maxWidth = width;
                            width = fileObject.ImagePropertyPixelWidth;
                        }
                        imgStyles += string.Format("max-width:{0}px; width:{1}px; cursor:pointer;", maxWidth, width);
                        clickAction = string.Format("onclick=\"resizeImage('width', this, {0})\"", width);
                        break;

                    case "height":
                        int height, maxHeight;
                        if (Int32.TryParse(fileObjectOptions[style], out height) == false)
                        {
                            height = 100;
                        }

                        if (fileObject.ImagePropertyPixelHeight > height)
                        {
                            maxHeight = fileObject.ImagePropertyPixelHeight;
                        }
                        else
                        {
                            maxHeight = height;
                            height = fileObject.ImagePropertyPixelHeight;
                        }
                        imgStyles += string.Format("max-height:{0}px; height:{1}px; cursor:pointer;", maxHeight, height);
                        clickAction = string.Format("onclick=\"resizeImage('height', this, {0})\"", height);
                        break;

                    case "size":
                        if (fileObjectOptions[style].ToLower().Equals("full"))
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
            if (fileObjectOptions.TryGetValue("alt", out alternativeText) && string.IsNullOrWhiteSpace(alternativeText) == false)
            {
                alternativeText = " alt=\"" + alternativeText.Trim('"') + "\"";
            }

            // check if we need to add an 'Edit-button' to the image (which will be displayed when mouse-hovering over the image)
            bool isEditable = false;
            string btnEditImageHtml = "...";
            if (fileObject.ContentType.Equals("image/drawio"))
            {
                if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet"))
                    && (browser.HttpServer.Customization.HidePlugins.ContainsKey("DiagramsNet") == false
                        || browser.HttpServer.Customization.HidePlugins["DiagramsNet"] == false
                        )
                   )
                {
                    isEditable = true;
                    imgClass += " diagram";
                    btnEditImageHtml = string.Format("<a class='button' href='diagrams.net/F{0}{1}' onclick='javascript:sessionStorage[\"originURL\"] = document.location.href; return true;'>" +
                                                          "<span class='phui-font-fa fa-sitemap'></span>" +
                                                     "</a>",
                        isTranslatedImage ? "TRAN" : "",
                        fileObject.ID);
                }
            }
            else
            if (fileObject.FileType == Phabricator.Data.File.FileStyle.Image)
            {
                if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.JSPaintImageEditor"))
                    && (browser.HttpServer.Customization.HidePlugins.ContainsKey("JSPaintImageEditor") == false
                        || browser.HttpServer.Customization.HidePlugins["JSPaintImageEditor"] == false
                        )
                   )
                {
                    isEditable = true;
                    imgClass += " editable-image";
                    btnEditImageHtml = string.Format("<a class='button' href='JSPaintImageEditor/F{0}{1}' onclick='javascript:sessionStorage[\"originURL\"] = document.location.href; return true;'>" +
                                                          "<span class='phui-font-fa fa-image'></span>" +
                                                     "</a>",
                        isTranslatedImage ? "TRAN" : "",
                        fileObject.ID);
                }
            }

            if (fileObjectOptions.Keys.Contains("float"))
            {
                imgLocatorStyles += "width: " + (fileObject.ImagePropertyPixelWidth + 10) + "px;";
            }
            else
            {
                if (imgClass.Contains("editable-image") == false)
                {
                    imgLocatorStyles += "width: " + fileObject.ImagePropertyPixelWidth + "px;";
                }
            }

            // return result
            if (isEditable)
            {
                if (isFullSize)
                {
                    /* TODO: margin-left aanpassen voor image-locator */
                    return string.Format("<div class='image-locator allow-full-screen {9}' style='{8}'>\n" +
                                         "  <div class='image-container'>\n" +
                                         "     <img rel='{0}' src='file/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>\n" +
                                         "     {7}\n" +
                                         "  </div>" +
                                         "</div>",
                        fileObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        fileObject.ID,
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
                                         "     <img rel='{0}' src='file/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>\n" +
                                         "     {7}\n" +
                                         "  </div>" +
                                         "</div>",
                        fileObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        fileObject.ID,
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
                                         "     <img rel='{0}' src='file/data/{1}{2}/' class='{3}' style='{4}'{5} onload='imageLoaded(this)'>\n" +
                                         "  </div>" +
                                         "</div>",
                        fileObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        fileObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        imgLocatorStyles,
                        imgLocatorClasses);
                }
                else
                {
                    return string.Format(@"<img rel='{0}' src='file/data/{1}{2}/' class='{3}' style='{4}'{5}{6} onload='imageLoaded(this)'>",
                        fileObject.FileName.Replace("'", ""),
                        isTranslatedImage ? "tran" : "",
                        fileObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction);
                }
            }
        }

        /// <summary>
        /// Returns the HTML code for a file which cannot be typed as an audio, image or video file.
        /// It will show an anchor link showing the filename, size and an icon based on its content-type
        /// </summary>
        /// <param name="fileObjectOptions">Remarkup options for the fiven file</param>
        /// <param name="fileObject">The file data as found in the SQLite database</param>
        /// <param name="browser">Reference to browser object</param>
        /// <returns>Anchor HTML tag</returns>
        private string ProcessGenericFile(Dictionary<string, string> fileObjectOptions, Phabricator.Data.File fileObject, Browser browser)
        {
            string formattedFileSize;
            if (fileObject.Size < 1024)
            {
                formattedFileSize = string.Format("{0} B", fileObject.Size);
            }
            else
            if (fileObject.Size < 1024 * 1024)
            {
                formattedFileSize = string.Format("{0} KB", fileObject.Size / 1024);
            }
            else
            {
                formattedFileSize = string.Format("{0} MB", fileObject.Size / (1024 * 1024));
            }

            return string.Format(
                        "<a class='remarkup-embed' href='file/data/{3}/'>\n" +
                        "  <div class='remarkup-embed-border'>\n" +
                        "      <span class='phui-font-fa {0}' style='top:0px'></span>\n" +
                        "      <span>\n" +
                        "          <span class='remarkup-embed-file-name'>{1}</span>\n" +
                        "          <span class='remarkup-embed-file-size'>{2}</span>\n" +
                        "      </span>\n" +
                        "      <span class='remarkup-embed-download-link'>\n" + Locale.TranslateText("FileReference.Download", browser.Session.Locale) + @"</span>" +
                        "  </div>\n" +
                        "</a>", fileObject.FontAwesomeIcon, fileObject.FileName, formattedFileSize, fileObject.ID);
        }

        /// <summary>
        /// Returns the HTML code for a video-typed file
        /// </summary>
        /// <param name="fileObjectOptions">Remarkup options for the fiven file</param>
        /// <param name="fileObject">The file data as found in the SQLite database</param>
        /// <returns>video HTML tag</returns>
        private string ProcessVideoFile(Dictionary<string, string> fileObjectOptions, Phabricator.Data.File fileObject)
        {
            string videoOptions = "";
            string videoPreload = "auto";
            foreach (string option in fileObjectOptions.Keys)
            {
                switch (option)
                {
                    case "autoplay":
                        videoOptions += " autoplay=\"autoplay\"";
                        break;

                    case "loop":
                        videoOptions += " loop=\"loop\"";
                        break;

                    default:
                        break;
                }
            }

            return string.Format(@"<video controls='controls' preload='{1}'{2}><source src='file/data/{0}/'{1}></video><br>", fileObject.ID, videoPreload, videoOptions);
        }
    }
}
