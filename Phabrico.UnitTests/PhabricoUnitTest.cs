using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace Phabrico.UnitTests
{
    public class PhabricoUnitTest : IDisposable
    {
        public Storage.Database Database;
        public Http.Server HttpServer;
        public Miscellaneous.HttpListenerContext HttpListenerContext;
        public string EncryptionKey = "0123456789abcdef";
        public Http.SessionManager.Token Token;
        public Phabricator.Data.User userWhoAmI;
        public Phabricator.Data.Account accountWhoAmI;

        public PhabricoUnitTest()
        {
            // set local database filename
            Storage.Database.DataSource = ".\\TestRemarkupEngine";

            // delete old database file if it exists
            if (System.IO.File.Exists(Storage.Database.DataSource))
            {
                System.IO.File.Delete(Storage.Database.DataSource);
            }

            // create database and HTTP server settings
            Storage.Database._dbVersionInDataFile = 0;
            Database = new Storage.Database(EncryptionKey);
            Database.PrivateEncryptionKey = EncryptionKey;
            HttpServer = new Http.Server(false, 13468);
            HttpListenerContext = new Miscellaneous.HttpListenerContext();
            Token = HttpServer.Session.CreateToken(EncryptionKey, null);
            Token.EncryptionKey = EncryptionKey;
            

            // initialize database with test content
            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.User userStorage = new Storage.User();

            userWhoAmI = new Phabricator.Data.User();
            userWhoAmI.DateSynchronized = DateTimeOffset.MinValue;
            userWhoAmI.RealName = "Johnny Birddog";
            userWhoAmI.UserName = "johnny";
            userWhoAmI.Selected = true;
            userWhoAmI.Token = "PHID-USER-e807f1fcf82d132f9bb0";
            userStorage.Add(Database, userWhoAmI);

            accountWhoAmI = new Phabricator.Data.Account();
            accountWhoAmI.ConduitAPIToken = "api-sat642eqloqv72ecax9qfkf54e61";
            accountWhoAmI.PhabricatorUrl = "http://localhost";
            accountWhoAmI.Theme = "light";
            accountWhoAmI.Parameters.AutoLogOutAfterMinutesOfInactivity = 0;
            accountWhoAmI.Parameters.ColumnHeadersToHide = new string[0];
            accountWhoAmI.Parameters.DarkenBrightImages = Phabricator.Data.Account.DarkenImageStyle.Disabled;
            accountWhoAmI.Parameters.LastSynchronizationTimestamp = DateTimeOffset.MinValue;
            accountWhoAmI.Parameters.Synchronization = Phabricator.Data.Account.SynchronizationMethod.All;
            accountWhoAmI.Parameters.ClipboardCopyForCodeBlock = true;
            accountWhoAmI.Parameters.UserToken = userWhoAmI.Token;
            accountWhoAmI.Token = EncryptionKey;
            accountWhoAmI.PublicXorCipher = new UInt64[] { 0, 0, 0, 0 };
            accountWhoAmI.PrivateXorCipher = new UInt64[] { 0, 0, 0, 0 };
            accountStorage.Add(Database, accountWhoAmI);

            Phabricator.Data.Project project = new Phabricator.Data.Project();
            project.DateSynchronized = DateTimeOffset.MinValue;
            project.InternalName = "myproject";
            project.Name = "Johnny was a joker";
            project.Selected = Phabricator.Data.Project.Selection.Selected;
            project.Token = "PHID-PROJ-life";
            projectStorage.Add(Database, project);

            Phabricator.Data.Phriction phrictionDocument = new Phabricator.Data.Phriction();
            phrictionDocument.Author = userWhoAmI.Token;
            phrictionDocument.Content = "Once upon a time, I was reading this story over and over again";
            phrictionDocument.DateModified = DateTimeOffset.MinValue;
            phrictionDocument.Name = "Story of my life";
            phrictionDocument.Path = "/";
            phrictionDocument.Projects = project.Token;
            phrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            phrictionDocument.Token = "PHID-WIKI-MYSTORY";
            phrictionStorage.Add(Database, phrictionDocument);

            phrictionDocument = new Phabricator.Data.Phriction();
            phrictionDocument.Author = userWhoAmI.Token;
            phrictionDocument.Content = "Once upon a time, I was reading my dad's story over and over again";
            phrictionDocument.DateModified = DateTimeOffset.MinValue;
            phrictionDocument.Name = "Story of my dad's life";
            phrictionDocument.Path = "x/";
            phrictionDocument.Projects = project.Token;
            phrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            phrictionDocument.Token = "PHID-WIKI-DADSSTORY";
            phrictionStorage.Add(Database, phrictionDocument);

            phrictionDocument = new Phabricator.Data.Phriction();
            phrictionDocument.Author = userWhoAmI.Token;
            phrictionDocument.Content = "Once upon a time, I was reading my grandfather's story over and over again";
            phrictionDocument.DateModified = DateTimeOffset.MinValue;
            phrictionDocument.Name = "Story of my grandfather's life";
            phrictionDocument.Path = "herald/transcript/";
            phrictionDocument.Projects = project.Token;
            phrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            phrictionDocument.Token = "PHID-WIKI-GRANDADDYSSTORY";
            phrictionStorage.Add(Database, phrictionDocument);

            Phabricator.Data.File file = new Phabricator.Data.File();
            file.DateModified = DateTimeOffset.MinValue;
            file.FileName = "small.png";
            file.ID = 1234;
            file.MacroName = ":mymacro";
            file.Token = "PHID-FILE-SMALLPNG";
            file.Size = 128;
            file.Data = ASCIIEncoding.ASCII.GetBytes("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAVSURBVBhXY/zPwMBwA4j/KzKo/QcAG3EEHrqBr2AAAAAASUVORK5CYII=");
            fileStorage.Add(Database, file);
        }

        public JsonConfiguration.UnitTest ParseTestConfiguration(string jsonFile)
        {
            string jsonData = File.ReadAllText(jsonFile);
            JsonConfiguration.UnitTest result = JsonConvert.DeserializeObject<JsonConfiguration.UnitTest>(jsonData);

            return result;
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        public virtual void Dispose()
        {
            Database.Dispose();
            HttpServer.Stop();

            System.IO.File.Delete(Storage.Database.DataSource);
        }
    }
}
