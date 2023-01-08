using ClosedXML.Excel;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using Phabrico.Parsers.BrokenXML;
using Phabrico.Parsers.Remarkup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Phabrico.ContentTranslation.Engines
{
    /// <summary>
    /// Translation engine for Excel import/export
    /// </summary>
    [Translation(Name = "excel")]
    public class ExcelTranslationEngine : TranslationEngine
    {
        private readonly string WorkSheetName = "Translation";

        public override bool IsRemoteTranslationService { get; } = false;

        protected override Dictionary<string, Translation> TranslationalDictionary { get; set; } = new Dictionary<string, Translation>();
        private Dictionary<string, bool> TranslationKeyProcessed = new Dictionary<string, bool>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiKey">N/A</param>
        public ExcelTranslationEngine(string apiKey)
            : base(apiKey)
        {
        }

        /// <summary>
        /// In case a word or sentence appears multiple times, this word or sentence should only appear once in the generated Excel file.
        /// The corresponding key will be appended with the new key. The keys are separated with a '|' character
        /// </summary>
        private void CorrectDuplicatedKeys()
        {
            var duplicatedTranslations = TranslationalDictionary.ToLookup(x => x.Value.OriginalText, x => x.Key)
                                                                .Where(x => x.Count() > 1)
                                                                .ToArray();
            foreach (var duplicatedTranslation in duplicatedTranslations)
            {
                string newKey = "";
                string originalText = TranslationalDictionary[duplicatedTranslation.FirstOrDefault()].OriginalText;
                string translatedText = TranslationalDictionary[duplicatedTranslation.FirstOrDefault()].TranslatedText;
                foreach (var duplicatedKey in duplicatedTranslation)
                {
                    newKey += "|" + duplicatedKey;
                    TranslationalDictionary.Remove(duplicatedKey);
                }

                newKey = newKey.TrimStart('|');
                TranslationalDictionary[newKey] = new Translation(originalText, translatedText);
            }
        }

        /// <summary>
        /// Returns the excel filedata in bytes
        /// </summary>
        /// <param name="worksheetName"></param>
        /// <returns></returns>
        private byte[] GenerateExcelData(string worksheetName)
        {
            CorrectDuplicatedKeys();

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add(worksheetName);

                        uint rowIndex = 1;

                        IXLCell cell = worksheet.Cell("A" + rowIndex);
                        cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                        cell.Style.Font.Bold = true;
                        cell.SetValue("Key");

                        cell = worksheet.Cell("B" + rowIndex);
                        cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                        cell.Style.Font.Bold = true;
                        cell.SetValue("Original text");

                        cell = worksheet.Cell("C" + rowIndex);
                        cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                        cell.Style.Font.Bold = true;
                        cell.SetValue("Translation");

                        foreach (var translation in TranslationalDictionary)
                        {
                            rowIndex++;

                            cell = worksheet.Cell("A" + rowIndex);
                            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                            cell.Style.Font.FontColor = XLColor.FromArgb(217, 217, 217);
                            cell.SetValue(translation.Key);

                            cell = worksheet.Cell("B" + rowIndex);
                            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                            cell.SetValue(translation.Value.OriginalText.Replace("\r", "").Replace("\n", "\r\n"));

                            cell = worksheet.Cell("C" + rowIndex);
                            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                            cell.SetValue(translation.Value.TranslatedText.Replace("\r", "").Replace("\n", "\r\n"));
                        }

                        worksheet.RangeUsed().SetAutoFilter(true);
                        worksheet.Columns().AdjustToContents();
                        worksheet.Columns(1, 1).Width = 5.43;
                        worksheet.Columns(3, 3).Width = 100;
                        worksheet.ActiveCell = worksheet.Cell("C2");
                        worksheet.SheetView.FreezeRows(1);
                        workbook.SaveAs(memoryStream);
                    }

                    return memoryStream.ToArray();
                }
            }
            finally
            {
                TranslationalDictionary = new Dictionary<string, Translation>();
            }
        }

        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the file content in a base64 encoding
        /// </summary>
        /// <returns>Stream containing base64 encoded file data</returns>
        public override Base64EIDOStream GetBase64EIDOStream()
        {
            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
            base64EIDOStream.WriteDecodedData(GenerateExcelData(WorkSheetName));
            return base64EIDOStream;
        }


        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the content-type of the file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override string GetContentType()
        {
            return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the name of the file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override string GetFileName()
        {
            return "Translation.xlsx";
        }

        /// <summary>
        /// Imports a translation file
        /// </summary>
        /// <param name="base64FileData">Base64-encoded file content</param>
        /// <param name="sourceLanguage">Current language</param>
        /// <param name="targetLanguage">Language to translate to</param>
        public override void ImportFile(string base64FileData, string sourceLanguage, string targetLanguage)
        {
            TranslationalDictionary = new Dictionary<string, Translation>();
            TranslationKeyProcessed = new Dictionary<string, bool>();

            using (Base64EIDOStream base64EIDOStream = new Base64EIDOStream(UTF8Encoding.UTF8.GetBytes(base64FileData)))
            {
                using (var workbook = new XLWorkbook(base64EIDOStream))
                {
                    var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name.Equals(WorkSheetName));
                    if (worksheet == null) throw new System.Exception(Locale.TranslateText("Invalid Excel file", sourceLanguage));

                    uint rowIndex = 1;  // skip header
                    while (true)
                    {
                        rowIndex++;
                        string key = (string)worksheet.Cell("A" + rowIndex).Value;
                        string originalText = System.Convert.ToString(worksheet.Cell("B" + rowIndex).Value);
                        string translatedText = System.Convert.ToString(worksheet.Cell("C" + rowIndex).Value);

                        if (string.IsNullOrWhiteSpace(key)) break;
                        if (string.IsNullOrWhiteSpace(originalText) || string.IsNullOrWhiteSpace(translatedText)) continue;

                        TranslationalDictionary[key] = new Translation(originalText, translatedText);
                        TranslationKeyProcessed[key] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Imports a translation dictionary, which was cretaed by the ImportFile method
        /// </summary>
        /// <param name="targetLanguage"></param>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="phrictionDocument"></param>
        /// <param name="translatedTitle"></param>
        /// <param name="translatedContent"></param>
        /// <returns>True if more underlyings documents need to be translated</returns>
        public override bool ImportTranslationDictionary(string targetLanguage, Storage.Database database, Http.Browser browser, Phabricator.Data.Phriction phrictionDocument, out string translatedTitle, out string translatedContent)
        {
            MD5 md5 = MD5.Create();
            int hashCounter = 0;

            translatedTitle = "";
            translatedContent = "";

            if (TranslationalDictionary.Any() == false) return false;

            string translationTitleKey = string.Join("",
                                            md5.ComputeHash(
                                                UTF8Encoding.UTF8.GetBytes(phrictionDocument.Token + phrictionDocument.Name)
                                            )
                                            .Select(b => b.ToString("X2"))
                                         )
                                         + 
                                         1.ToString("D4");
            if (TranslationalDictionary.ContainsKey(translationTitleKey))
            {
                TranslationKeyProcessed[translationTitleKey] = true;
                translatedTitle = TranslationalDictionary[translationTitleKey].TranslatedText;
            }
            else
            {
                translatedTitle = phrictionDocument.Name;
            }

            RemarkupEngine remarkup = new RemarkupEngine();
            RemarkupParserOutput remarkupParserOutput;
            remarkup.ToHTML(null, database, browser, "/", phrictionDocument.Content, out remarkupParserOutput, false);


            string xmlData = remarkupParserOutput.TokenList.ToXML(database, browser, "/");

            string unformattedContent = DecodeBrokenXmlFormatting(xmlData);

            Parsers.BrokenXML.BrokenXmlParser brokenXmlParser = new Parsers.BrokenXML.BrokenXmlParser();
            List<Parsers.BrokenXML.BrokenXmlToken> brokenXmlTokens = brokenXmlParser.Parse(unformattedContent)
                                                                                    .ToList();

            int previousMatchEndPosition = 0;
            int tokenIndex;
            bool inTableCell = false;
            for (tokenIndex = 0; tokenIndex < brokenXmlTokens.Count; tokenIndex++)
            {
                Parsers.BrokenXML.BrokenXmlToken brokenXmlToken = brokenXmlTokens[tokenIndex];

                if (inTableCell == false)
                {
                    if (brokenXmlToken.Value.Equals("<td>", StringComparison.OrdinalIgnoreCase))
                    {
                        inTableCell = true;
                    }
                }
                else
                {
                    if (brokenXmlToken.Value.Equals("</td>", StringComparison.OrdinalIgnoreCase))
                    {
                        inTableCell = false;
                    }
                }

                Parsers.BrokenXML.BrokenXmlText brokenXmlTextToken = brokenXmlToken as Parsers.BrokenXML.BrokenXmlText;
                if (brokenXmlTextToken != null
                    && RegexSafe.IsMatch(brokenXmlTextToken.Value, @"^{[FTM][^}]*}$", RegexOptions.Singleline) == false
                    && (RegexSafe.IsMatch(brokenXmlTextToken.Value, @"[a-zA-Z]", RegexOptions.Singleline)
                        || RegexSafe.IsMatch(brokenXmlTextToken.Value, @"[\p{IsCJKUnifiedIdeographs}\p{IsThai}]", RegexOptions.Singleline)
                       )
                )
                {
                    // calculate prefix/postfix which identify identify the number of whitespace characters at the front and back
                    int prefix = 0, postfix = 0;
                    while (char.IsWhiteSpace(brokenXmlTextToken.Value[prefix])) prefix++;
                    while (char.IsWhiteSpace(brokenXmlTextToken.Value[brokenXmlTextToken.Value.Length - (postfix + 1)])) postfix++;

                    string text = brokenXmlTextToken.Value.Substring(prefix, brokenXmlTextToken.Value.Length - (prefix + postfix));
                    if (inTableCell)
                    {
                        int nextTokenIndex = tokenIndex + 1;
                        while (nextTokenIndex < brokenXmlTokens.Count)
                        {
                            if (brokenXmlTokens[nextTokenIndex].Value.Equals("<N>") == false) break;

                            while (brokenXmlTokens[nextTokenIndex].Value.Equals("<N>"))
                            {
                                postfix = postfix - brokenXmlTokens.Skip(nextTokenIndex)
                                                                   .Take(3)
                                                                   .Sum(token => token.Length);
                                brokenXmlTokens.RemoveRange(nextTokenIndex, 3);  // skip Newline tags
                            }

                            Parsers.BrokenXML.BrokenXmlText brokenXmlTextNextToken = brokenXmlTokens[nextTokenIndex] as Parsers.BrokenXML.BrokenXmlText;
                            if (brokenXmlTextNextToken == null) break;

                            text += brokenXmlTextNextToken.Value;
                            postfix = postfix - brokenXmlTextNextToken.Length;
                            brokenXmlTokens.RemoveAt(nextTokenIndex);
                            tokenIndex = nextTokenIndex;
                        }
                    }

                    // in case multiple sentences in 1 translation -> put each sentence on a separate line
                    text = Regex.Replace(text, @"([^.].[.]) ", "$1\r\n");

                    bool hasNewlineAtEnd = text.EndsWith("\n");
                    if (hasNewlineAtEnd)
                    {
                        text = text.TrimEnd('\r', '\n');
                    }

                    string hashKey = string.Join("",
                                        md5.ComputeHash(
                                            UTF8Encoding.UTF8.GetBytes(phrictionDocument.Token + text)
                                        )
                                        .Select(b => b.ToString("X2"))
                                     );

                    Translation translation;
                    hashCounter++;
                    string translationKey = hashKey + hashCounter.ToString("D4");
                    if (TranslationalDictionary.TryGetValue(translationKey, out translation) == false)
                    {
                        // key not found -> search for combined key
                        string combinedKey = TranslationalDictionary.Keys.FirstOrDefault(key => key.Contains(translationKey));
                        if (combinedKey == null)
                        {
                            translation = new Translation(text, text);
                        }
                        else
                        {
                            translation = TranslationalDictionary[combinedKey];
                            TranslationKeyProcessed[combinedKey] = true;
                        }
                    }
                    else
                    {
                        TranslationKeyProcessed[translationKey] = true;
                    }

                    if (inTableCell)
                    {
                        translatedContent += unformattedContent.Substring(previousMatchEndPosition, brokenXmlTextToken.Index - previousMatchEndPosition)
                                           + translation.TranslatedText.Replace("\r", "");
                    }
                    else
                    {
                        translatedContent += unformattedContent.Substring(previousMatchEndPosition, brokenXmlTextToken.Index - previousMatchEndPosition)
                                           + translation.TranslatedText.Replace("\r", "").Replace("\n", " ");
                    }

                    if (hasNewlineAtEnd)
                    {
                        translatedContent += "\n";
                    }

                    previousMatchEndPosition = brokenXmlTextToken.Index + brokenXmlTextToken.Length - postfix;

                    continue;
                }
                else
                if (brokenXmlToken.Value.StartsWith("<A "))
                {
                    // hyperlink: check if we have a custom text in this hyperlink
                    Match matchHyperlinkWithCustomText = RegexSafe.Match(brokenXmlToken.Value, "^(<A f=\".\" u=\"%5B%5B.*%7C)(.*)(%5D%5D\">)$", RegexOptions.None);
                    if (matchHyperlinkWithCustomText.Success)
                    {
                        string hyperlinkPreText = matchHyperlinkWithCustomText.Groups[1].Value;
                        string text = System.Web.HttpUtility.UrlDecode(matchHyperlinkWithCustomText.Groups[2].Value);
                        string hyperlinkPostText = " " + matchHyperlinkWithCustomText.Groups[3].Value;

                        // calculate prefix/postfix which identify identify the number of whitespace characters at the front and back
                        int prefix = 0, postfix = 0;
                        while (char.IsWhiteSpace(text[prefix])) prefix++;
                        while (char.IsWhiteSpace(text[text.Length - (postfix + 1)])) postfix++;

                        text = text.Substring(prefix, text.Length - (prefix + postfix));

                        // in case multiple sentences in 1 translation -> put each sentence on a separate line
                        text = Regex.Replace(text, @"([^.].[.]) ", "$1\r\n");

                        string hashKey = string.Join("",
                                            md5.ComputeHash(
                                                UTF8Encoding.UTF8.GetBytes(phrictionDocument.Token + text)
                                            )
                                            .Select(b => b.ToString("X2"))
                                         );

                        Translation translation;
                        hashCounter++;
                        string translationKey = hashKey + hashCounter.ToString("D4");
                        if (TranslationalDictionary.TryGetValue(translationKey, out translation) == false)
                        {
                            // key not found -> search for combined key
                            string combinedKey = TranslationalDictionary.Keys.FirstOrDefault(key => key.Contains(translationKey));
                            if (combinedKey == null)
                            {
                                translation = new Translation(text, text);
                            }
                            else
                            {
                                translation = TranslationalDictionary[combinedKey];
                                TranslationKeyProcessed[combinedKey] = true;
                            }
                        }
                        else
                        {
                            TranslationKeyProcessed[translationKey] = true;
                        }

                        translatedContent += unformattedContent.Substring(previousMatchEndPosition, brokenXmlToken.Index - previousMatchEndPosition)
                                           + hyperlinkPreText
                                           + translation.TranslatedText
                                           + hyperlinkPostText;

                        previousMatchEndPosition = brokenXmlToken.Index + brokenXmlToken.Length - postfix;

                        continue;
                    }
                }

                translatedContent += unformattedContent.Substring(previousMatchEndPosition, brokenXmlToken.Index - previousMatchEndPosition);
                translatedContent += brokenXmlToken.Value;

                previousMatchEndPosition = brokenXmlToken.Index + brokenXmlToken.Length;
            }

            translatedContent += unformattedContent.Substring(previousMatchEndPosition);

            // convert to remarkup
            Language originalLanguage = browser.Session.Locale;
            browser.Session.Locale = targetLanguage;
            translatedContent = remarkupParserOutput.TokenList.FromXML(database, browser, "/", translatedContent);
            browser.Session.Locale = originalLanguage;

            // check if everything is translated
            bool moreTranslationsNeeded = TranslationKeyProcessed.Values.Any(value => value == false);
            return moreTranslationsNeeded;
        }

        /// <summary>
        /// Converts some BrokenXml formatting into Rmarkup formatting
        /// </summary>
        /// <param name="brokenXml"></param>
        /// <returns></returns>
        private string DecodeBrokenXmlFormatting(string brokenXml)
        {
            string result = brokenXml;

            result = RegexSafe.Replace(result, "</?B>", "**");
            result = RegexSafe.Replace(result, "</?I>", "//");
            result = RegexSafe.Replace(result, "</?U>", "__");
            result = RegexSafe.Replace(result, "</?S>", "~~");
            result = RegexSafe.Replace(result, "</?HL>", "!!");
            result = RegexSafe.Replace(result, "<M>(`[^`]*`)</M>", "$1");

            // convert <BR></BR> to brackets
            while (true)
            {
                Match match = RegexSafe.Match(result,
                                              @"<BR>                        # opening (
                                               (                            # begin of content
                                                   (?>                      # now match...
                                                      <BR>   (?<DEPTH>)     # a {, increasing the depth counter
                                                   |                        # or
                                                      </BR>  (?<-DEPTH>)    # a }, decreasing the depth counter
                                                   |                        # or
                                                      .                     # any characters except braces
                                                   )*                       # any number of times
                                                   (?(DEPTH)(?!))           # until the depth counter is zero again
                                               )                            # end of content
                                             </BR>                          # then match the closing )",
                                              RegexOptions.IgnorePatternWhitespace);
                if (match.Success == false) break;

                result = result.Substring(0, match.Index)
                       + "("
                       + result.Substring(match.Index + "<BR>".Length, match.Length - "<BR></BR>".Length)
                       + ")"
                       + result.Substring(match.Index + match.Length);
            }

            return result;
        }

        /// <summary>
        /// Translates some content from one language to another language
        /// </summary>
        /// <param name="sourceLanguage">Language of content</param>
        /// <param name="destinationLanguage">Language of translated content</param>
        /// <param name="content">Content to be translated</param>
        /// <param name="previouslyTranslatedContent">Translated content. Can be empty if this is the first translation time or it can contain a translation from a previous call</param>
        /// <param name="origin">Location where the content can be found (i.e. a token)</param>
        /// <returns>Translated content</returns>
        protected override string Translate(string sourceLanguage, string destinationLanguage, string content, string previouslyTranslatedContent, string origin)
        {
            MD5 md5 = MD5.Create();
            int hashCounter = 1;

            string unformattedContent = DecodeBrokenXmlFormatting(content);
            string unformattedPreviouslyTranslatedContent = DecodeBrokenXmlFormatting(previouslyTranslatedContent);

            Parsers.BrokenXML.BrokenXmlParser brokenXmlParser = new Parsers.BrokenXML.BrokenXmlParser();

            List<Parsers.BrokenXML.BrokenXmlToken> brokenXmlTokens = brokenXmlParser.Parse(unformattedContent)
                                                                                    .ToList();
            List<Parsers.BrokenXML.BrokenXmlToken> brokenXmlTokensPreviousTranslation = brokenXmlParser.Parse(unformattedPreviouslyTranslatedContent)
                                                                                                       .ToList();
            // search for table cells which have newlines in them
            DecodeMultilinedTableCells(ref brokenXmlTokens);
            DecodeMultilinedTableCells(ref brokenXmlTokensPreviousTranslation);

            if (brokenXmlTokensPreviousTranslation.Count != brokenXmlTokens.Count)
            {
                brokenXmlTokensPreviousTranslation = null;
            }

            List<Parsers.BrokenXML.BrokenXmlText> brokenXmlTextTokens = brokenXmlTokens.OfType<Parsers.BrokenXML.BrokenXmlText>()
                                                                                   .Where(token => RegexSafe.IsMatch(token.Value, @"^{[FTM][^}]*}$", RegexOptions.Singleline) == false)
                                                                                   .Where(token => RegexSafe.IsMatch(token.Value, @"[a-zA-Z]", RegexOptions.Singleline)
                                                                                                || RegexSafe.IsMatch(token.Value, @"[\p{IsCJKUnifiedIdeographs}\p{IsThai}]", RegexOptions.Singleline)
                                                                                         )
                                                                                   .ToList();

            foreach (Parsers.BrokenXML.BrokenXmlText brokenXmlTextToken in brokenXmlTextTokens)
            {
                // calculate prefix/postfix which identify identify the number of whitespace characters at the front and back
                int prefix = 0, postfix = brokenXmlTextToken.Value.Length - 1;
                while (char.IsWhiteSpace(brokenXmlTextToken.Value[prefix])) prefix++;
                while (postfix > prefix && char.IsWhiteSpace(brokenXmlTextToken.Value[postfix])) postfix--;
                if (postfix == prefix) continue;

                string text = brokenXmlTextToken.Value.Substring(prefix, 1 + postfix - prefix);

                // in case multiple sentences in 1 translation -> put each sentence on a separate line
                text = Regex.Replace(text, @"([^.].[.]) ", "$1\r\n");

                string hashKey = string.Join("",
                                    md5.ComputeHash(
                                        UTF8Encoding.UTF8.GetBytes(origin + text)
                                    )
                                    .Select(b => b.ToString("X2"))
                                 );

                string previouslyTranslatedText = "";
                if (brokenXmlTokensPreviousTranslation != null &&
                    brokenXmlTokens.IndexOf(brokenXmlTextToken) >= 0)
                {
                    previouslyTranslatedText = brokenXmlTokensPreviousTranslation[brokenXmlTokens.IndexOf(brokenXmlTextToken)].Value;

                    // in case multiple sentences in 1 translation -> put each sentence on a separate line
                    previouslyTranslatedText = Regex.Replace(previouslyTranslatedText, @"([^.].[.]) ", "$1\r\n");

                    previouslyTranslatedText = previouslyTranslatedText.Trim('\r', '\n');
                }

                TranslationalDictionary[hashKey + hashCounter.ToString("D4")] = new Translation(text, previouslyTranslatedText);
                hashCounter++;
            }
            return "";
        }

        private void DecodeMultilinedTableCells(ref List<BrokenXmlToken> brokenXmlTokens)
        {
            int startTableCell = 0;
            while (true)
            {
                while (startTableCell < brokenXmlTokens.Count && brokenXmlTokens[startTableCell].Value.Equals("<td>", StringComparison.OrdinalIgnoreCase) == false) startTableCell++;
                if (startTableCell >= brokenXmlTokens.Count) break;

                int endTableCell = startTableCell + 1;
                while (endTableCell < brokenXmlTokens.Count && brokenXmlTokens[endTableCell].Value.Equals("</td>", StringComparison.OrdinalIgnoreCase) == false) endTableCell++;
                if (endTableCell >= brokenXmlTokens.Count) break;

                int tableCellContentToken = startTableCell + 1;
                while (tableCellContentToken < endTableCell)
                {
                    if (brokenXmlTokens[tableCellContentToken].Value.Equals("<N>"))
                    {
                        for (int previousTextToken = tableCellContentToken - 1; previousTextToken >= 0; previousTextToken--)
                        {
                            Parsers.BrokenXML.BrokenXmlText precedingTextToken = brokenXmlTokens[previousTextToken] as Parsers.BrokenXML.BrokenXmlText;
                            if (precedingTextToken != null)
                            {
                                brokenXmlTokens.RemoveRange(tableCellContentToken, 3);
                                endTableCell -= 3;

                                int nextToken = previousTextToken + 1;
                                if (nextToken < brokenXmlTokens.Count && brokenXmlTokens[nextToken] is Parsers.BrokenXML.BrokenXmlText)
                                {
                                    precedingTextToken.Value += brokenXmlTokens[nextToken].Value;
                                    brokenXmlTokens.RemoveAt(nextToken);
                                    endTableCell--;
                                    tableCellContentToken--;
                                }
                                break;
                            }
                        }
                    }

                    tableCellContentToken++;
                }

                startTableCell = endTableCell + 1;
            }
        }
    }
}