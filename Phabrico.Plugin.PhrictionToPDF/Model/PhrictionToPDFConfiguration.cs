using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

using Newtonsoft.Json;

using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;

namespace Phabrico.Plugin.Model
{
    public class PhrictionToPDFConfiguration
    {
        public class HeaderFooterData
        {
            public string Font { get; set; } = "Arial";
            public int FontSize { get; set; } = 16;
            public string Text1 { get; set; } = "";
            public string Size1 { get; set; } = "33%";
            public string Align1 { get; set; } = "left";
            public string Text2 { get; set; } = "";
            public string Size2 { get; set; } = "33%";
            public string Align2 { get; set; } = "center";
            public string Text3 { get; set; } = "";
            public string Align3 { get; set; } = "right";
        }

        private string _pageHeaderJson = "{}";
        private string _pageFooterJson = "{}";

        public HeaderFooterData HeaderData { get; set; } = new HeaderFooterData();
        public HeaderFooterData FooterData { get; set; } = new HeaderFooterData();
        public Phriction PhrictionDocument;

        public PhrictionToPDFConfiguration(Phriction phrictionDocument)
        {
            PhrictionDocument = phrictionDocument;
        }

        public string PageHeaderJson
        {
            get
            {
                return _pageHeaderJson;
            }

            set
            {
                if (value != null)
                {
                    _pageHeaderJson = value;

                    PhrictionToPDFConfiguration configuration = JsonConvert.DeserializeObject<PhrictionToPDFConfiguration>(_pageHeaderJson);
                    HeaderData = new HeaderFooterData();
                    HeaderData.Font = configuration.Font;
                    HeaderData.FontSize = configuration.FontSize;
                    HeaderData.Size1 = configuration.Size1;
                    HeaderData.Size2 = configuration.Size2;
                    HeaderData.Align1 = configuration.Align1;
                    HeaderData.Align2 = configuration.Align2;
                    HeaderData.Align3 = configuration.Align3;

                    string[] textLines = configuration.Text1.Split('\n');
                    if (textLines.Length > 1)
                    {
                        HeaderData.Text1 = string.Join("&#13;", textLines);
                    }
                    else
                    {
                        HeaderData.Text1 = configuration.Text1;
                    }

                    textLines = configuration.Text2.Split('\n');
                    if (textLines.Length > 1)
                    {
                        HeaderData.Text2 = string.Join("&#13;", textLines);
                    }
                    else
                    {
                        HeaderData.Text2 = configuration.Text2;
                    }

                    textLines = configuration.Text3.Split('\n');
                    if (textLines.Length > 1)
                    {
                        HeaderData.Text3 = string.Join("&#13;", textLines);
                    }
                    else
                    {
                        HeaderData.Text3 = configuration.Text3;
                    }
                }
            }
        }

        public string PageFooterJson
        {
            get
            {
                return _pageFooterJson;
            }

            set
            {
                if (value != null)
                {
                    _pageFooterJson = value;

                    PhrictionToPDFConfiguration configuration = JsonConvert.DeserializeObject<PhrictionToPDFConfiguration>(_pageFooterJson);
                    FooterData = new HeaderFooterData();
                    FooterData.Font = configuration.Font;
                    FooterData.FontSize = configuration.FontSize;
                    FooterData.Size1 = configuration.Size1;
                    FooterData.Size2 = configuration.Size2;
                    FooterData.Text1 = configuration.Text1;
                    FooterData.Text2 = configuration.Text2;
                    FooterData.Text3 = configuration.Text3;
                    FooterData.Align1 = configuration.Align1;
                    FooterData.Align2 = configuration.Align2;
                    FooterData.Align3 = configuration.Align3;
                }
            }
        }

        public string PageHeaderHtml
        {
            get
            {
                PhrictionToPDFConfiguration configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<PhrictionToPDFConfiguration>(PageHeaderJson);
                configuration.PhrictionDocument = PhrictionDocument;

                if (PhrictionDocument == null)
                {
                    return "";
                }
                else
                {
                    return string.Format(@"
                        <table style=""width: 100%; padding-bottom:25px;"">
                          <tbody>
                            <tr style=""font-family: {0}; font-size: {1}px"">
                              <td style=""text-align:{2}; vertical-align: top; width: {3}; position: relative;"">{4}</td>
                              <td style=""text-align:{5}; vertical-align: top; width: {6}; position: relative;"">{7}</td>
                              <td style=""text-align:{8}; vertical-align: top;"">{9}<br></td>
                            </tr>
                          </tbody>
                        </table>",
                        FormatFont(HeaderData.Font),
                        HeaderData.FontSize,
                        HeaderData.Align1,
                        HeaderData.Size1,
                        FormatText(HeaderData.Text1),
                        HeaderData.Align2,
                        HeaderData.Size2,
                        FormatText(HeaderData.Text2),
                        HeaderData.Align3,
                        FormatText(HeaderData.Text3)
                    );
                }
            }
        }

        public string PageFooterHtml
        {
            get
            {
                PhrictionToPDFConfiguration configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<PhrictionToPDFConfiguration>(PageFooterJson);
                configuration.PhrictionDocument = PhrictionDocument;

                if (PhrictionDocument == null)
                {
                    return "";
                }
                else
                {
                    return string.Format(@"
                        <table style=""width: 100%; padding-top:25px;"">
                          <tbody>
                            <tr style=""font-family: {0}; font-size: {1}px"">
                              <td style=""text-align:{2}; vertical-align: top; width: {3}; position: relative;"">{4}</td>
                              <td style=""text-align:{5}; vertical-align: top; width: {6}; position: relative;"">{7}</td>
                              <td style=""text-align:{8}; vertical-align: top;"">{9}<br></td>
                            </tr>
                          </tbody>
                        </table>",
                        FormatFont(FooterData.Font),
                        FooterData.FontSize,
                        FooterData.Align1,
                        FooterData.Size1,
                        FormatText(FooterData.Text1),
                        FooterData.Align2,
                        FooterData.Size2,
                        FormatText(FooterData.Text2),
                        FooterData.Align3,
                        FormatText(FooterData.Text3)
                    );
                }
            }
        }

        private string FormatFont(string font)
        {
            if (font.Equals("arial"))
                return "Arial, sans-serif;";

            if (font.Equals("times-new-roman"))
                return "Times New Roman, serif;";

            if (font.Equals("garamond"))
                return "Garamond, serif;";

            if (font.Equals("courier-new"))
                return "Courier New, monospace;";

            if (font.Equals("brush-script"))
                return "Brush Script MT, cursive;";

            return "Arial, sans-serif;";
        }

        private string FormatText(string unformattedText)
        {
            string formattedText = unformattedText.Replace("{PAGE}", "<span class='page'></span>");
            formattedText = formattedText.Replace("{NUMPAGES}", "<span class='topage'></span>");
            formattedText = formattedText.Replace("{TITLE}", HttpUtility.HtmlEncode(PhrictionDocument.Name));
            formattedText = formattedText.Replace("{LAST-MODIFIED}", PhrictionDocument.DateModified.ToLocalTime().ToString("g"));

            string[] questions = RegexSafe.Matches(formattedText, "{ASK ([^}]+)}").OfType<Match>().Select(match => match.Groups[1].Value).ToArray();

            return formattedText;
        }

        public string Font { get; set; }
        public int FontSize { get; set; }
        public string Text1 { get; set; }
        public string Align1 { get; set; }
        public string Size1 { get; set; }
        public string Text2 { get; set; }
        public string Align2 { get; set; }
        public string Size2 { get; set; }
        public string Text3 { get; set; }
        public string Align3 { get; set; }
    }
}
