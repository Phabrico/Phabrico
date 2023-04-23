using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Controllers;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Storage
{
    /// <summary>
    /// Storage class for words in a document or in a task.
    /// Each word that appears in a document or task is stored in the keywordInfo table
    /// This storage class is mainly used for the search functionality
    /// </summary>
    public class Keyword : PhabricatorObject<Phabricator.Data.Keyword>
    {
        private static readonly Regex regLanguagesWithoutWordSegmentation = new Regex(@"[\p{IsCJKUnifiedIdeographs}\p{IsThai}]+");

        /// <summary>
        /// Inner class for filtering out the (max) top 10 search results
        /// This is used in a Dictionary where the token is the key and WordsArraySearchResult is the value
        /// </summary>
        public class WordsArraySearchResult
        {
            /// <summary>
            /// In case the search field contains multiple words, each word is searched separately in the database.
            /// For each word that is found, a bit in this Bitmask property will be set to 1.
            /// In the end, only the WordsArraySearchResult records where all bits of Bitmask set to 1 are taken
            /// in the final result
            /// </summary>
            public Int64 Bitmask { get; set; }

            /// <summary>
            /// True if at least one of the words in the search field is found as an exact match
            /// </summary>
            public bool ExactWord { get; set; }

            /// <summary>
            /// Number of occurrences of all (partial and exact) words in the search field found.
            /// </summary>
            public int NumberOccurrences { get; set; }

            /// <summary>
            /// First word in the search field.
            /// This is somehow used as a hack for searching maniphest tasks by their T-ID and sorting them numerically.
            /// E.g. T5 should show up before after T34
            /// </summary>
            public string FirstWord { get; set; }

            /// <summary>
            /// Initializes a new instance of WordsArraySearchResult
            /// </summary>
            /// <param name="bitmask"></param>
            /// <param name="exactWord"></param>
            /// <param name="numberOfOccurrences"></param>
            /// <param name="firstWord"></param>
            public WordsArraySearchResult(Int64 bitmask, bool exactWord, int numberOfOccurrences, string firstWord)
            {
                Bitmask = bitmask;
                ExactWord = exactWord;
                NumberOccurrences = numberOfOccurrences;
                FirstWord = firstWord;
            }
        }

        /// <summary>
        /// Inserts or updates a Keyword record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="keyword"></param>
        /// <param name="language"></param>
        public override void Add(Database database, Phabricator.Data.Keyword keyword)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO keywordInfo(name, token, language, description, nbrocc) 
                           VALUES (@name, @token, @language, @description, @numberOccurrences);
                       ", database.Connection, transaction))
                {

                    string encodedKeyword = database.PolyCharacterCipherEncrypt(keyword.Name.ToUpper());
                    database.AddParameter(dbCommand, "name", encodedKeyword, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "token", keyword.Token);
                    database.AddParameter(dbCommand, "language", keyword.Language, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "description", keyword.Description);
                    database.AddParameter(dbCommand, "numberOccurrences", keyword.NumberOccurrences.ToString());
                    dbCommand.ExecuteNonQuery();

                    Database.IsModified = true;

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Parses all words out from the given phabricatorObject (e.g. phriction document or maniphest task) and stores them in the SQLite keywordInfo table
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="database"></param>
        /// <param name="phabricatorObject"></param>
        public void AddPhabricatorObject(Controller controller, Database database, Phabricator.Data.PhabricatorObject phabricatorObject)
        {
            string blobContent = "";

            Storage.Keyword keywordStorage = new Storage.Keyword();
            Content content = new Content(database);
            foreach (Language language in Content.GetAvailableLanguages(database, phabricatorObject))
            {
                Phabricator.Data.Keyword keyword = new Phabricator.Data.Keyword();
                keyword.Token = phabricatorObject.Token;
                keyword.Language = language;

                Content.Translation translation = content.GetTranslation(phabricatorObject.Token, language);
                if (translation != null)
                {
                    // check if we have a staged translation
                    Storage.Stage stageStorage = new Storage.Stage();
                    Phabricator.Data.PhabricatorObject stagedTranslation = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, phabricatorObject.Token, language);
                    if (stagedTranslation != null)
                    {
                        // update translation content
                        Phabricator.Data.Phriction stagedPhrictionDocument = stagedTranslation as Phabricator.Data.Phriction;
                        Phabricator.Data.Maniphest stagedManiphestTask = stagedTranslation as Phabricator.Data.Maniphest;
                        Phabricator.Data.PhamePost stagedBlogPost = stagedTranslation as Phabricator.Data.PhamePost;

                        if (stagedPhrictionDocument != null)
                        {
                            translation.TranslatedRemarkup = stagedPhrictionDocument.Content;
                        }
                        if (stagedManiphestTask != null)
                        {
                            translation.TranslatedRemarkup = stagedManiphestTask.Description;
                        }
                        if (stagedBlogPost != null)
                        {
                            translation.TranslatedRemarkup = stagedBlogPost.Content;
                        }

                    }
                }

                Phabricator.Data.Phriction phrictionDocument = phabricatorObject as Phabricator.Data.Phriction;
                Phabricator.Data.Maniphest maniphestTask = phabricatorObject as Phabricator.Data.Maniphest;
                Phabricator.Data.PhamePost blogPost = phabricatorObject as Phabricator.Data.PhamePost;

                if (phrictionDocument != null)
                {
                    if (translation != null)
                    {
                        phrictionDocument.Name = translation.TranslatedTitle;
                        phrictionDocument.Content = translation.TranslatedRemarkup;
                    }

                    string urlCrumbs = Controllers.Phriction.GenerateCrumbs(database, phrictionDocument, language);
                    JArray crumbs = JsonConvert.DeserializeObject(urlCrumbs) as JArray;
                    urlCrumbs = string.Join(" ", crumbs.Where(t => ((JValue)t["inexistant"]).Value.Equals(false))
                                                       .Select(t => ((JValue)t["name"]).Value).ToArray()
                                           );
                    
                    RemarkupParserOutput remarkupParserOutput;
                    controller.ConvertRemarkupToHTML(database, "", phrictionDocument.Content, out remarkupParserOutput, false);

                    blobContent = phrictionDocument.Name + " ";         // phriction document title
                    blobContent += urlCrumbs;                           // crumbs from phriction document url
                    blobContent += remarkupParserOutput.Text;           // phriction document content
                    foreach (var header in remarkupParserOutput.TokenList.OfType<Parsers.Remarkup.Rules.RuleHeader>().Where(h => h.Depth < 3)) // insert duplicates for header
                    {                                                                                                                          // content, so the word count
                        for (int depth = 0; depth < 3 - header.Depth; depth++)                                                                 // will be larger for words found
                        {                                                                                                                      // in headers
                            blobContent += header.Text;                                                                                        // The bigger the header is, the
                        }                                                                                                                      // larger the word count is
                    }                                                                                                                          //
                    keyword.Description = phrictionDocument.Name;
                }

                if (maniphestTask != null)
                {
                    RemarkupParserOutput remarkupParserOutput;
                    controller.ConvertRemarkupToHTML(database, "", maniphestTask.Description, out remarkupParserOutput, false);

                    blobContent = maniphestTask.Name + " ";                     // maniphest task title
                    blobContent += remarkupParserOutput.Text + " ";             // maniphest task description
                    blobContent += string.Format("T{0}", maniphestTask.ID);     // maniphest task T-identifier
                    keyword.Description = maniphestTask.Name;
                }

                if (blogPost != null)
                {
                    RemarkupParserOutput remarkupParserOutput;
                    controller.ConvertRemarkupToHTML(database, "", blogPost.Content, out remarkupParserOutput, false);

                    blobContent = blogPost.Title + " ";                         // blog post title
                    blobContent += remarkupParserOutput.Text + " ";             // blog post content
                    blobContent += string.Format("J{0}", blogPost.ID);          // blog post J-identifier
                    keyword.Description = blogPost.Title;
                }

                if (string.IsNullOrEmpty(blobContent) == false)
                {
                    Regex regManiphestTaskOrBlogPost = new Regex(@"\b[JT]-?[0-9]+\b", RegexOptions.IgnoreCase);
                    IEnumerable<string> taskOrBlogPostReferences = regManiphestTaskOrBlogPost.Matches(blobContent)
                                                                                             .OfType<Match>()
                                                                                             .Select(match => match.Value);

                    List<Match> assemblyLikeWords = RegexSafe.Matches(blobContent, "[A-Za-z0-9_]{1,255}([.][A-Za-z0-9_]{1,255})+", RegexOptions.Singleline)    // store also all assembly-like or url-like words (i.e. words with a dot in)
                                                             .OfType<Match>()                                                                                  //   for example:  System.Net,  www.phacility.com
                                                             .ToList();                                                                                        //

                    List<string> emailAddresses = RegexSafe.Matches(blobContent, @"((?=.{0,64}@.{0,255})(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*""))@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9]))\.){3}(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9])|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])", RegexOptions.Singleline)
                                                           .OfType<Match>()
                                                           .Select(m => m.Value)
                                                           .ToList();


                    string nonCJKBlobContent = RegexSafe.Replace(blobContent, @"[\p{IsCJKUnifiedIdeographs}\p{IsThai}!#$%&'()*+,-./:;<=>?@[\]^`{|}~]", " ", RegexOptions.Singleline);
                    List<string> words = nonCJKBlobContent.Split(new char[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                                          .Where(word => word.Length >= 2)                                                              // only store words which are at least 2 characters and ...
                                                          .Where(word => word.Length <= 50)                                                             // ... at maximum 50 characters long
                                                          .Where(word => word.All(ch => ch == '-' || ch == '_' || ch == '*' || ch == '#') == false)     // do not store words which do only contain some specific symbols
                                                          .Concat(assemblyLikeWords.Select(m => m.Value))                                               // store also all assembly-like or url-like words (i.e. words with a dot in)
                                                          .Concat(assemblyLikeWords.Select(m => m.Groups.OfType<Group>().Last().Value.Substring(1)))    // store the last word of an assembly-like word (e.g.  databaseName.dbo.tableName -> tableName)
                                                          .Concat(taskOrBlogPostReferences)
                                                          .Concat(emailAddresses)
                                                          .Select(word => word.ToLower())
                                                          .ToList();

                    // add division-like formulas (e.g. U/I)
                    words.AddRange(RegexSafe.Matches(blobContent, "[A-Za-z]+[A-Za-z0-9]*/[A-Za-z0-9]+")
                                            .OfType<Match>()
                                            .Select(m => m.Value)
                                  );

                    // store everything in lowercase
                    foreach (var wordCount in words.GroupBy(key => key))
                    {
                        keyword.Name = wordCount.Key;
                        keyword.NumberOccurrences = wordCount.Count();
                        keywordStorage.Add(database, keyword);
                    }

                    // check if content contains characters from a language without word segmentation (e.g. Chinese, Japanese, Korean, Thai)
                    MatchCollection matchesLanguagesWithoutWordSegmentation = regLanguagesWithoutWordSegmentation.Matches(blobContent);
                    if (matchesLanguagesWithoutWordSegmentation.Count > 0)
                    {
                        // we'll store for each character sequence all possible "words" that can be generated from this sequence
                        Dictionary<string, int> wordCount = new Dictionary<string, int>();

                        foreach (Match matchLanguagesWithoutWordSegmentation in matchesLanguagesWithoutWordSegmentation)
                        {
                            for (int wordLengthLanguagesWithoutWordSegmentation = 1; wordLengthLanguagesWithoutWordSegmentation <= matchLanguagesWithoutWordSegmentation.Length; wordLengthLanguagesWithoutWordSegmentation++)
                            {
                                for (int startPositionLanguageWithoutWordSegmentation = 0; startPositionLanguageWithoutWordSegmentation <= matchLanguagesWithoutWordSegmentation.Length - wordLengthLanguagesWithoutWordSegmentation; startPositionLanguageWithoutWordSegmentation++)
                                {
                                    int count;
                                    string wordLanguageWithoutWordSegmentation = matchLanguagesWithoutWordSegmentation.Value.Substring(startPositionLanguageWithoutWordSegmentation, wordLengthLanguagesWithoutWordSegmentation);

                                    if (wordCount.TryGetValue(wordLanguageWithoutWordSegmentation, out count) == false)
                                    {
                                        count = 0;
                                    }

                                    wordCount[wordLanguageWithoutWordSegmentation] = count + 1;
                                }
                            }
                        }

                        foreach (var word in wordCount)
                        {
                            keyword.Name = word.Key;
                            keyword.NumberOccurrences = word.Value;
                            keywordStorage.Add(database, keyword);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all linked keywords of a given phabricatorObject (e.g. phriction document or maniphest task) from the SQLite database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phabricatorObject"></param>
        public void DeletePhabricatorObject(Database database, Phabricator.Data.PhabricatorObject phabricatorObject)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       DELETE FROM keywordInfo
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", phabricatorObject.Token);
                if (dbCommand.ExecuteNonQuery() > 0)
                {
                    Database.IsModified = true;
                }
            }
        }

        /// <summary>
        /// This overridden method is not used
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Keyword Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            return null;
        }

        /// <summary>
        /// Returns all available keywords from the SQLite database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Keyword> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, token, description, nbrocc
                       FROM keywordInfo
                       WHERE language = @language;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "language", language, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Keyword record = new Phabricator.Data.Keyword();
                        record.Name = (string)reader["name"];
                        record.Token = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["token"]);
                        record.Description = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["description"]);
                        record.NumberOccurrences = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["nbrocc"]));

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all available tokens from the SQLite database which contain a specific word
        /// </summary>
        /// <param name="database"></param>
        /// <param name="word"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public IEnumerable<string> GetTokensByExactWord(Database database, string word, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, token, description, nbrocc
                       FROM keywordInfo
                       WHERE name = @name
                         AND language = @language;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "name", database.PolyCharacterCipherEncrypt(word.ToUpper()));
                database.AddParameter(dbCommand, "language", language, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return Encryption.Decrypt(database.EncryptionKey, (byte[])reader["token"]);
                    }
                }
            }
        }

        /// <summary>
        /// Returns all tokens which contain all specified keywords
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="words">keywords that exist in the document or task</param>
        /// <param name="tokenPrefix">first characters of tokens. Can be used to filter type of Phabricator objects (only wiki-documents for example). If empty, all objects are returned</param>
        /// <param name="language">language of the keywords</param>
        /// <returns></returns>
        public IEnumerable<string> GetTokensByWords(Database database, string[] words, string tokenPrefix, Language language)
        {
            string sqlStatement;
            SQLiteCommand dbCommand;
            Dictionary<string, WordsArraySearchResult> wordsArraySearchResults = new Dictionary<string, WordsArraySearchResult>();
            List<string> invalidTokens = new List<string>();

            // collect all words in a double dictionary
            for (int wordIndex = 0; wordIndex < words.Length && wordIndex < 63; wordIndex++)
            {
                string word = words[wordIndex];
                bool searchInTitleOnly = false;
                bool negation = word.StartsWith("-");

                if (negation)
                {
                    word = word.Substring(1);
                }

                if (word.StartsWith("title:"))
                {
                    word = word.Substring("title:".Length);
                    searchInTitleOnly = true;
                }

                string encryptedWord = database.PolyCharacterCipherEncrypt(word.ToUpper());

                sqlStatement = string.Format(@"
                    SELECT keywordInfo.name,
                           keywordInfo.token,
                           keywordInfo.nbrocc,
                           keywordInfo.description,
                           keywordInfo.language
                    FROM keywordInfo
                    INNER JOIN (
                        SELECT MIN(level) level, token
                        FROM (
                            SELECT 1 level, token
                            FROM keywordInfo
                            WHERE name LIKE '{0}%'
                              AND language = @language

                            UNION

                            SELECT 2 level, token
                            FROM keywordInfo
                            WHERE name LIKE '{0}%'
                              AND language = @notApplicable
                        ) drv
                        GROUP BY token
                    ) selection
                    ON keywordInfo.token = selection.token
                    AND keywordInfo.name LIKE '{0}%'
                    AND ((selection.level = 1 AND keywordInfo.language = @language)         -- translation available
                      OR (selection.level = 2 AND keywordInfo.language = @notApplicable)    -- no translation available
                        ) 
                ", encryptedWord);

                using (dbCommand = new SQLiteCommand(sqlStatement, database.Connection))
                {
                    database.AddParameter(dbCommand, "language", language, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "notApplicable", Language.NotApplicable, Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            WordsArraySearchResult wordSearchResult;
                            bool exactWord;
                            string token = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["token"]);
                            if (token.StartsWith(tokenPrefix) == false) continue;

                            int numberOccurrences = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["nbrocc"]));
                            word = (string)reader["name"];

                            if (searchInTitleOnly)
                            {
                                string description = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["description"]);
                                string decryptedWord = database.PolyCharacterCipherDecrypt(word);
                                if (description.ToUpper().Contains(decryptedWord) == false) continue;
                            }

                            // determine which searchResults dictionary should be used
                            if (negation)
                            {
                                string decryptedWord = database.PolyCharacterCipherDecrypt(word);
                                invalidTokens.AddRange( GetTokensByExactWord(database, decryptedWord, language) );
                            }
                            else
                            {
                                string firstWord;

                                // complete searchResults dictionary
                                if (wordsArraySearchResults.TryGetValue(token, out wordSearchResult) == false)
                                {
                                    string decryptedWord = database.PolyCharacterCipherDecrypt(word);
                                    exactWord = decryptedWord.Equals(words[wordIndex], StringComparison.OrdinalIgnoreCase);
                                    firstWord = decryptedWord;

                                    wordsArraySearchResults[token] = new WordsArraySearchResult(0, exactWord, 0, firstWord);
                                }
                                else
                                {
                                    exactWord = wordsArraySearchResults[token].ExactWord;
                                    firstWord = wordsArraySearchResults[token].FirstWord;
                                }

                                wordsArraySearchResults[token] = new WordsArraySearchResult(wordsArraySearchResults[token].Bitmask | (Int64)Math.Pow(2, wordIndex),
                                                                                            exactWord,
                                                                                            wordsArraySearchResults[token].NumberOccurrences + numberOccurrences,
                                                                                            firstWord);
                            }
                        }
                    }
                }
            }

            // remove items from dictionary where one of the words was not found
            Int64 allWordsBitMask = (Int64)Math.Pow(2, words.Length) - 1;
            int bitValue = 1;
            foreach (string word in words)
            {
                if (word.StartsWith("-"))
                {
                    allWordsBitMask ^= bitValue;
                }

                bitValue <<= 1;
            }

            foreach (KeyValuePair<string, WordsArraySearchResult> kvp in wordsArraySearchResults.Where(item => item.Value.Bitmask != allWordsBitMask).ToList())
            {
                wordsArraySearchResults.Remove(kvp.Key);
            }

            // remove items from dictionary which are found in the invalidTokens array
            foreach (string invalidToken in invalidTokens)
            {
                wordsArraySearchResults.Remove(invalidToken);
            }

            foreach (string token in wordsArraySearchResults.OrderByDescending(item => item.Value.ExactWord ? 1 : 0)
                                                            .ThenByDescending(item => item.Value.NumberOccurrences)
                                                            .ThenBy(item => RegexSafe.IsMatch(item.Value.FirstWord, "T-?[0-9]+", RegexOptions.None)
                                                                          ? string.Format("{0:D10}", Int32.Parse(RegexSafe.Match(item.Value.FirstWord, "T-?([0-9]+)", RegexOptions.None).Groups[1].Value))
                                                                          : item.Value.FirstWord
                                                                   )
                                                            .Select(item => item.Key)
                                                            .Distinct())
            {
                yield return token;
            }
        }
    }
}
