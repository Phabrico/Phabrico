using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;
using Phabrico.Miscellaneous;

namespace Phabrico.UnitTests
{
    public class PhabricoUnitTest : IDisposable
    {
        public Storage.Database Database;
        public Http.Server HttpServer;
        public Miscellaneous.HttpListenerContext HttpListenerContext;
        public string EncryptionKey = "q%9#kpdw8,dp%k/+&hs3/<spt%-|//e;";
        public static string Username = "johnny";
        public static string Password = "My-Secret-Password-0123456789";
        public static string PrivateEncryptionKey = "enrr%8.rzwjc1s;4,#jitt/=\"f2w1ru9";
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
            Database.PrivateEncryptionKey = PrivateEncryptionKey;
            HttpServer = new Http.Server(false, 13468);
            HttpListenerContext = new Miscellaneous.HttpListenerContext();
            Token = HttpServer.Session.CreateToken(EncryptionKey, null);
            Token.EncryptionKey = EncryptionKey;
            

            // initialize database with test content
            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();
            Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.User userStorage = new Storage.User();

            userWhoAmI = new Phabricator.Data.User();
            userWhoAmI.DateSynchronized = DateTimeOffset.MinValue;
            userWhoAmI.RealName = "Johnny Birddog";
            userWhoAmI.UserName = Username;
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
            accountWhoAmI.Token = "a'=jm+ul#`~9'&mb;\" ;k,dqkowxo4,5";
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

            Phabricator.Data.Phriction myPhrictionDocument = new Phabricator.Data.Phriction();
            myPhrictionDocument.Author = userWhoAmI.Token;
            myPhrictionDocument.Content = "Once upon a time, I was reading this story over and over again";
            myPhrictionDocument.DateModified = DateTimeOffset.MinValue;
            myPhrictionDocument.Name = "Story of my life";
            myPhrictionDocument.Path = "/";
            myPhrictionDocument.Projects = project.Token;
            myPhrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            myPhrictionDocument.Token = "PHID-WIKI-MYSTORY";
            phrictionStorage.Add(Database, myPhrictionDocument);

            Phabricator.Data.Phriction daddysPhrictionDocument = new Phabricator.Data.Phriction();
            daddysPhrictionDocument.Author = userWhoAmI.Token;
            daddysPhrictionDocument.Content = "Once upon a time, I was reading my dad's story over and over again";
            daddysPhrictionDocument.DateModified = DateTimeOffset.MinValue;
            daddysPhrictionDocument.Name = "Story of my dad's life";
            daddysPhrictionDocument.Path = "x/";
            daddysPhrictionDocument.Projects = project.Token;
            daddysPhrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            daddysPhrictionDocument.Token = "PHID-WIKI-DADSSTORY";
            phrictionStorage.Add(Database, daddysPhrictionDocument);
            Database.DescendTokenFrom(myPhrictionDocument.Token, daddysPhrictionDocument.Token);

            Phabricator.Data.Phriction grandaddysPhrictionDocument = new Phabricator.Data.Phriction();
            grandaddysPhrictionDocument.Author = userWhoAmI.Token;
            grandaddysPhrictionDocument.Content = "Once upon a time, I was reading my grandfather's story over and over again";
            grandaddysPhrictionDocument.DateModified = DateTimeOffset.MinValue;
            grandaddysPhrictionDocument.Name = "Story of my grandfather's life";
            grandaddysPhrictionDocument.Path = "herald/transcript/";
            grandaddysPhrictionDocument.Projects = project.Token;
            grandaddysPhrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            grandaddysPhrictionDocument.Token = "PHID-WIKI-GRANDADDYSSTORY";
            phrictionStorage.Add(Database, grandaddysPhrictionDocument);
            Database.DescendTokenFrom(daddysPhrictionDocument.Token, grandaddysPhrictionDocument.Token);

            Phabricator.Data.ManiphestPriority maniphestPriority = new Phabricator.Data.ManiphestPriority();
            maniphestPriority.Name = "High";
            maniphestPriority.Priority = 80;
            maniphestPriorityStorage.Add(Database, maniphestPriority);

            Phabricator.Data.ManiphestStatus maniphestStatus = new Phabricator.Data.ManiphestStatus();
            maniphestStatus.Name = "Open";
            maniphestStatus.Value = "open";
            maniphestStatusStorage.Add(Database, maniphestStatus);

            Phabricator.Data.Maniphest maniphestTask = new Phabricator.Data.Maniphest();
            maniphestTask.ID = "1247";
            maniphestTask.Token = "PHID-TASK-PIXIES";
            maniphestTask.Name = "Mind searching";
            maniphestTask.Description = "# Put your feet in the air\n# Put your head on the ground\n# Spin it";
            maniphestTask.Author = userWhoAmI.Token;
            maniphestTask.DateModified = DateTimeOffset.MinValue;
            maniphestTask.IsOpen = true;
            maniphestTask.IsOpen = true;
            maniphestTask.Owner = userWhoAmI.Token;
            maniphestTask.Priority = "80";
            maniphestTask.Projects = "";
            maniphestTask.Status = "open";
            maniphestTask.Subscribers = "";
            maniphestStorage.Add(Database, maniphestTask);

            maniphestTask = new Phabricator.Data.Maniphest();
            maniphestTask.ID = "2145";
            maniphestTask.Token = "PHID-TASK-DEEPPURPLE";
            maniphestTask.Name = "Play the intro of Child In Time";
            maniphestTask.Description = "G2 G2 A2";
            maniphestTask.Author = userWhoAmI.Token;
            maniphestTask.DateModified = DateTimeOffset.MinValue;
            maniphestTask.IsOpen = true;
            maniphestTask.Owner = userWhoAmI.Token;
            maniphestTask.Priority = "80";
            maniphestTask.Projects = "";
            maniphestTask.Status = "open";
            maniphestTask.Subscribers = "";
            maniphestStorage.Add(Database, maniphestTask);

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
