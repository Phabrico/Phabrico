using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Phabrico
{
    public class ApplicationCustomization
    {
        public enum ApplicationAuthenticationFactor
        {
            /// <summary>
            /// No authentication is needed
            /// </summary>
            Public,

            /// <summary>
            /// Authentication happens by means of a username and a password
            /// </summary>
            Knowledge
        }

        public enum ApplicationTheme
        {
            Auto,

            [Description("light")]
            Light,

            [Description("dark")]
            Dark
        }

        /// <summary>
        /// Logo of the application that should be shown in the top left corner
        /// </summary>
        private Image customApplicationLogo = null;

        /// <summary>
        /// Icon that should be shown in the browser tab
        /// </summary>
        private Image customFavIcon = null;

        /// <summary>
        /// If false, Phabricator is not accessible
        /// </summary>
        private bool masterDataIsAccessible = true;

        /// <summary>
        /// Base64 data of logo of the application that should be shown in the top left corner
        /// </summary>
        internal string CustomApplicationLogoBase64 { get; private set; } = null;

        /// <summary>
        /// Base64 data of icon that should be shown in the browser tab
        /// </summary>
        internal string CustomFavIconBase64 { get; private set; } = null;

        /// <summary>
        /// Global cascading style sheets which are injected in each page
        /// </summary>
        public string ApplicationCSS { get; set; }

        /// <summary>
        /// The logo that should be shown in the top left corner
        /// </summary>
        public Image ApplicationLogo
        {
            get
            {
                return customApplicationLogo;
            }

            set
            {
                customApplicationLogo = value;

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    Image image = new Bitmap(customApplicationLogo);
                    int newWidth = image.Width;
                    int newHeight = image.Height;

                    // resize image if height too large (height should be 2 x height header on top)
                    if (newHeight > 2*36)
                    {
                        newWidth = (newWidth * 2*36) / newHeight;
                        newHeight = 2*36;

                        var destRect = new Rectangle(0, 0, newWidth, newHeight);
                        var newImage = new Bitmap(newWidth, newHeight);

                        newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                        using (Graphics graphics = Graphics.FromImage(newImage))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                            graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);

                            image.Dispose();
                            image = new Bitmap(newImage);
                        }
                    }

                    // convert image to base64
                    image.Save(memoryStream, ImageFormat.Png);
                    byte[] imageData = memoryStream.ToArray();
                    CustomApplicationLogoBase64 = "data:image/png;base64," + Convert.ToBase64String(imageData);
                    image.Dispose();
                }
            }
        }

        /// <summary>
        /// CSS styles for formatting the ApplicationName
        /// </summary>
        public Dictionary<string, string> ApplicationLogoStyle { get; set; } = new Dictionary<string, string>();

        
        /// <summary>
        /// The name of the application that should be shown in the top left corner
        /// </summary>
        public string ApplicationName { get; set; } = "Phabrico";

        /// <summary>
        /// CSS styles for formatting the ApplicationName
        /// </summary>
        public Dictionary<string, string> ApplicationNameStyle { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// How a user can authenticate themselves in Phabrico
        /// </summary>
        public ApplicationAuthenticationFactor AuthenticationFactor { get; set; } = ApplicationAuthenticationFactor.Knowledge;

        /// <summary>
        /// The icon that should be shown in the browser tab
        /// </summary>
        public Image FavIcon
        {
            get
            {
                return customFavIcon;
            }

            set
            {
                customFavIcon = value;

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    Image image = new Bitmap(customFavIcon);
                    int newWidth = image.Width;
                    int newHeight = image.Height;

                    // resize image if height too large (height should be 2 x height header on top)
                    if (newHeight > 2*36)
                    {
                        newWidth = (newWidth * 2*36) / newHeight;
                        newHeight = 2*36;

                        var destRect = new Rectangle(0, 0, newWidth, newHeight);
                        var newImage = new Bitmap(newWidth, newHeight);

                        newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                        using (Graphics graphics = Graphics.FromImage(newImage))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                            graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);

                            image.Dispose();
                            image = new Bitmap(newImage);
                        }
                    }

                    // convert image to base64
                    image.Save(memoryStream, ImageFormat.Png);
                    byte[] imageData = memoryStream.ToArray();
                    CustomFavIconBase64 = "data:image/png;base64," + Convert.ToBase64String(imageData);
                    image.Dispose();
                }
            }
        }

        /// <summary>
        /// If true, Config screen will not be accessible
        /// </summary>
        public bool HideConfig { get; set; } = false;

        /// <summary>
        /// If true, Files screen will not be accessible
        /// </summary>
        public bool HideFiles { get; set; } = false;

        /// <summary>
        /// If true, Offline Changes screen will not be accessible
        /// </summary>
        public bool HideOfflineChanges { get; set; } = false;

        /// <summary>
        /// If true, Maniphest tasks will not be accessible
        /// </summary>
        public bool HideManiphest { get; set; } = false;

        /// <summary>
        /// If true, the tooltips for the menu items in the homepage will not be shown
        /// </summary>
        public bool HideNavigatorTooltips { get; set; } = false;

        /// <summary>
        /// If true, Phriction/wiki documents will not be accessible
        /// </summary>
        public bool HidePhriction { get; set; } = false;

        /// <summary>
        /// If true, the menu on the right side of the Phriction documents is no longer visible
        /// </summary>
        public bool HidePhrictionActionMenu { get; set; } = false;

        /// <summary>
        /// If true, the changes made in Phriction/wiki documents can not be seen or undone
        /// </summary>
        public bool HidePhrictionChanges { get; set; } = false;

        /// <summary>
        /// If true, Phriction/wiki documents can not be marked as favorite
        /// </summary>
        public bool HidePhrictionFavorites { get; set; } = false;

        /// <summary>
        /// If true, Phabricator projects will not be accessible
        /// </summary>
        public bool HideProjects { get; set; } = false;

        /// <summary>
        /// If true, Phabricator users will not be accessible
        /// </summary>
        public bool HideUsers { get; set; } = false;

        /// <summary>
        /// If true, Search field will not be accessible
        /// </summary>
        public bool HideSearch { get; set; } = false;

        /// <summary>
        /// If true, no Phriction document or Maniphest task can be edited
        /// </summary>
        public bool IsReadonly { get; set; } = false;

        /// <summary>
        /// Language code for Phabrico application.
        /// (Content of Phriction documents or Maniphest tasks will not be translated)
        /// </summary>
        public string Language { get; set; } = null;

        /// <summary>
        /// If false, the master data on Phabricator is not accessible via Phabrico.
        /// If IsReadonly is true, MasterDataIsAccessible wil be false
        /// </summary>
        public bool MasterDataIsAccessible
        {
            get
            {
                return IsReadonly == false
                    && masterDataIsAccessible;
            }

            set
            {
                masterDataIsAccessible = value;
            }
        }

        /// <summary>
        /// Graphical appearance
        /// </summary>
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.Auto;
    }
}