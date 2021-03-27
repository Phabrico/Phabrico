using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for File objects
    /// </summary>
    public class RuleReferenceFile : RemarkupRule
    {
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
            Match match = RegexSafe.Match(remarkup, @"^{F(-?[0-9]+)([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            Storage.Stage stageStorage = new Storage.Stage();

            string tokenId = browser.GetCookie("token");
            if (tokenId != null)
            {
                string encryptionKey = SessionManager.GetToken(browser)?.EncryptionKey;
                if (string.IsNullOrEmpty(encryptionKey) == false)
                {
                    SessionManager.Token token = SessionManager.GetToken(browser);

                        // unmask private encryption key
                        if (token.PrivateEncryptionKey != null)
                        {
                            UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                            database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);
                        }

                        Account existingAccount = accountStorage.Get(database, SessionManager.GetToken(browser));

                        int fileObjectID = Int32.Parse(match.Groups[1].Value);
                        Dictionary<string, string> fileObjectOptions = match.Groups[2]
                                                                                     .Value
                                                                                     .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                                     .ToDictionary(key => key.Split('=')
                                                                                                             .FirstOrDefault()
                                                                                                             .ToLower()
                                                                                                             .Trim(),
                                                                                                   value => value.Contains('=') ? value.Split('=')[1] : "");

                        bool showImageAsLink = false;
                        if (fileObjectOptions.ContainsKey("layout") == false)
                        {
                            fileObjectOptions["layout"] = "left";
                        }
                        else
                        if (fileObjectOptions["layout"].ToLower().Equals("link"))
                        {
                            showImageAsLink = true;
                        }

                        Phabricator.Data.File fileObject = fileStorage.GetByID(database, fileObjectID, true);
                        if (fileObject == null)
                        {
                            fileObject = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, fileObjectID, false);
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

                                    if (fileObjectOptions.ContainsKey("name"))
                                    {
                                        fileObject.FileName = fileObjectOptions["name"];
                                    }

                                    html = ProcessGenericFile(fileObjectOptions, fileObject, browser);
                                }
                                else
                                {
                                    html = ProcessImageFile(database, browser, fileObjectOptions, existingAccount, fileObject, fileObjectID);
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

                    remarkup = remarkup.Substring(match.Length);
                    
                    Length = match.Length;

                    return true;
                }
            }

            return false;
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

            return string.Format(@"<audio controls='controls' preload='{1}'{2}><source src='/file/data/{0}/' type='audio/x-wav'></audio><br>", fileObject.ID, audioPreload, audioOptions);
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
        /// <returns>img HTML tag</returns>
        private string ProcessImageFile(Storage.Database database, Browser browser, Dictionary<string, string> fileObjectOptions, Account existingAccount, Phabricator.Data.File fileObject, int fileObjectID)
        {
            Storage.File fileStorage = new Storage.File();
            string imgClass = "";

            // check if file content was also loaded, if not load it
            if (fileObject.DataStream == null)
            {
                Storage.Stage stageStorage = new Storage.Stage();
                fileObject = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, fileObjectID, true);
                if (fileObject == null)
                {
                    // file was not loaded from staging area, so content was not loaded -> load file content
                    fileObject = fileStorage.GetByID(database, fileObjectID, false);
                }
            }

            if (existingAccount.Theme == "dark" && existingAccount.Parameters.DarkenBrightImages != Account.DarkenImageStyle.Disabled)
            {
                try
                {
                    // load image and count the 16 boundary colors of the image (10x10 pixels for each corner) + center
                    fileObject.DataStream.Seek(0, SeekOrigin.Begin);
                    System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(fileObject.DataStream);
                    Dictionary<uint, int> boundaryColorCount = new Dictionary<uint, int>();
                    List<KeyValuePair<uint, int>> centerColorCount = new List<KeyValuePair<uint, int>>();
                    if (bitmap.Height >= 10 && bitmap.Width >= 10)
                    {
                        // count colors in corners and center of bitmap
                        boundaryColorCount = CountNumberOfColorsInCorners(bitmap);
                        centerColorCount = CountNumberOfColorsInCenter(bitmap);

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
                catch
                {
                }
            }

            string imgStyles = "";
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
                            imgStyles += "display:block; margin-left: auto; margin-right: auto;";
                        }
                        else
                        {
                            imgStyles += string.Format("float: {0};", fileObjectOptions["layout"]);
                        }
                        break;

                    case "layout":
                        if (fileObjectOptions.ContainsKey("float") == false)
                        {
                            if (fileObjectOptions["layout"].ToLower().Equals("center"))
                            {
                                imgStyles += "margin-left: auto; margin-right: auto;";
                            }
                            else
                            if (fileObjectOptions["layout"].ToLower().Equals("right"))
                            {
                                imgStyles += "margin-left: auto;";
                            }
                            else
                            if (fileObjectOptions["layout"].ToLower().Equals("inline"))
                            {
                                imgStyles += "margin-top: 0px; margin-bottom: -20px;";
                            }
                        }
                        break;

                    case "width":
                        imgStyles += string.Format("max-width: {0}px; cursor:pointer;", fileObjectOptions[style]);
                        clickAction = string.Format("onclick=\"resizeImage('width', this, {0})\"", fileObjectOptions[style]);
                        break;

                    case "height":
                        imgStyles += string.Format("max-height: {0}px; cursor:pointer;", fileObjectOptions[style]);
                        clickAction = string.Format("onclick=\"resizeImage('height', this, {0})\"", fileObjectOptions[style]);
                        break;

                    case "size":
                        if (fileObjectOptions[style].ToLower().Equals("full"))
                        {
                            isFullSize = true;
                            imgStyles += "max-width: max-content;";
                        }
                        break;

                    default:
                        break;
                }
            }

            // check if alternative text should be displayed
            if (fileObjectOptions.ContainsKey("alt") && string.IsNullOrWhiteSpace(fileObjectOptions["alt"]) == false)
            {
                alternativeText = " alt=\"" + fileObjectOptions["alt"] + "\"";
            }

            // check if we need to add an 'Edit-button' to the image (which will be displayed when mouse-hovering over the image)
            bool isEditable = false;
            string btnEditImageHtml = "...";
            if (fileObject.ContentType.Equals("image/drawio"))
            {
                if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet")))
                {
                    isEditable = true;
                    imgClass += " diagram";
                    btnEditImageHtml = string.Format("<a class='button' href='/diagrams.net/F{0}' onclick='javascript:sessionStorage[\"originURL\"] = document.location.href; return true;'>" +
                                                          "<span class='phui-font-fa fa-sitemap'></span>" +
                                                     "</a>", 
                        fileObject.ID);
                }
            }

            // return result
            if (isEditable )
            {
                if (isFullSize)
                {
                    return string.Format(@"<div class='image-container allow-full-screen'>
                                          <img rel='{0}' src='/file/data/{1}/' class='{2}' style='{3}'{4}{5}>
                                          {6}
                                       </div>",
                        fileObject.FileName.Replace("'", ""),
                        fileObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction,
                        btnEditImageHtml);
                }
                else
                {
                    return string.Format(@"<div class='image-container'>
                                          <img rel='{0}' src='/file/data/{1}/' class='{2}' style='{3}'{4}{5}>
                                          {6}
                                       </div>",
                        fileObject.FileName.Replace("'", ""),
                        fileObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText,
                        clickAction,
                        btnEditImageHtml);
                }
            }
            else
            {
                if (isFullSize)
                {
                    return string.Format(@"<div class='image-container allow-full-screen'>
                                          <img rel='{0}' src='/file/data/{1}/' class='{2}' style='{3}'{4}>
                                       </div>",
                        fileObject.FileName.Replace("'", ""),
                        fileObject.ID,
                        imgClass,
                        imgStyles,
                        alternativeText);
                }
                else
                {
                    return string.Format(@"<img rel='{0}' src='/file/data/{1}/' class='{2}' style='{3}'{4}{5}>",
                        fileObject.FileName.Replace("'", ""),
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
                        @"<a class='remarkup-embed' href='/file/data/{3}/'>
                        <div class='remarkup-embed-border'>
                            <span class='phui-font-fa {0}' style='top:0px'></span>
                            <span>
                                <span class='remarkup-embed-file-name'>{1}</span>
                                <span class='remarkup-embed-file-size'>{2}</span>
                            </span>
                            <span class='remarkup-embed-download-link'>" + Locale.TranslateText("FileReference.Download", browser.Session.Locale) + @"</span>
                        </div>
                        </a>", fileObject.FontAwesomeIcon, fileObject.FileName, formattedFileSize, fileObject.ID);
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

            return string.Format(@"<video controls='controls' preload='{1}'{2}><source src='/file/data/{0}/'{1}></video><br>", fileObject.ID, videoPreload, videoOptions);
        }
    }
}
