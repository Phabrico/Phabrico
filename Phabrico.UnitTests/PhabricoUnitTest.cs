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
        public UInt64[] PublicXorCipher = new UInt64[] { 1, 2, 3, 4 };
        public UInt64[] PrivateXorCipher = new UInt64[] { 5, 6, 7, 8 };

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
            Token.EncryptionKey = Encryption.XorString(EncryptionKey, PublicXorCipher);
            

            // initialize database with test content
            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();
            Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.Transaction transactionStorage = new Storage.Transaction();
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
            accountWhoAmI.PhabricatorUrl = "http://127.0.0.2:46975";
            accountWhoAmI.Theme = "light";
            accountWhoAmI.Parameters.AutoLogOutAfterMinutesOfInactivity = 0;
            accountWhoAmI.Parameters.ColumnHeadersToHide = new string[0];
            accountWhoAmI.Parameters.DarkenBrightImages = Phabricator.Data.Account.DarkenImageStyle.Disabled;
            accountWhoAmI.Parameters.LastSynchronizationTimestamp = DateTimeOffset.MinValue;
            accountWhoAmI.Parameters.Synchronization = Phabricator.Data.Account.SynchronizationMethod.All;
            accountWhoAmI.Parameters.ClipboardCopyForCodeBlock = true;
            accountWhoAmI.Parameters.UserToken = userWhoAmI.Token;
            accountWhoAmI.Token = "a'=jm+ul#`~9'&mb;\" ;k,dqkowxo4,5";
            accountWhoAmI.PublicXorCipher = PublicXorCipher;
            accountWhoAmI.PrivateXorCipher = PrivateXorCipher;
            accountStorage.Add(Database, accountWhoAmI);

            Phabricator.Data.Project project = new Phabricator.Data.Project();
            project.DateSynchronized = DateTimeOffset.MinValue;
            project.InternalName = "myproject";
            project.Name = "Johnny was a joker";
            project.Selected = Phabricator.Data.Project.Selection.Selected;
            project.Token = "PHID-PROJ-life";
            projectStorage.Add(Database, project);

            project = new Phabricator.Data.Project();
            project.DateSynchronized = DateTimeOffset.MinValue;
            project.InternalName = "myproject2";
            project.Name = "Classic";
            project.Selected = Phabricator.Data.Project.Selection.Selected;
            project.Token = "PHID-PROJ-classic";
            projectStorage.Add(Database, project);

            project = new Phabricator.Data.Project();
            project.DateSynchronized = DateTimeOffset.MinValue;
            project.InternalName = "myproject3";
            project.Name = "Music";
            project.Selected = Phabricator.Data.Project.Selection.Selected;
            project.Token = "PHID-PROJ-music";
            projectStorage.Add(Database, project);

            Phabricator.Data.Phriction myPhrictionDocument = new Phabricator.Data.Phriction();
            myPhrictionDocument.Author = userWhoAmI.Token;
            myPhrictionDocument.Content = "Once upon a time, I was reading this story over and over again";
            myPhrictionDocument.DateModified = DateTimeOffset.Now;
            myPhrictionDocument.Name = "Story of my life";
            myPhrictionDocument.Path = "/";
            myPhrictionDocument.Projects = project.Token;
            myPhrictionDocument.Subscribers = userWhoAmI.Token + "," + project.Token;
            myPhrictionDocument.Token = "PHID-WIKI-MYSTORY";
            phrictionStorage.Add(Database, myPhrictionDocument);

            Phabricator.Data.Phriction daddysPhrictionDocument = new Phabricator.Data.Phriction();
            daddysPhrictionDocument.Author = userWhoAmI.Token;
            daddysPhrictionDocument.Content = "Once upon a time, I was reading my dad's story over and over again";
            daddysPhrictionDocument.DateModified = DateTimeOffset.Now;
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
            grandaddysPhrictionDocument.DateModified = DateTimeOffset.Now;
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
            maniphestTask.DateModified = DateTimeOffset.Now;
            maniphestTask.IsOpen = true;
            maniphestTask.IsOpen = true;
            maniphestTask.Owner = userWhoAmI.Token;
            maniphestTask.Priority = "80";
            maniphestTask.Projects = "";
            maniphestTask.Status = "open";
            maniphestTask.Subscribers = "";
            maniphestTask.Transactions = new Phabricator.Data.Transaction[] {
                new Phabricator.Data.Transaction {
                    Author = userWhoAmI.Token,
                    DateModified = DateTimeOffset.Now,
                    ID = "150175",
                    IsStaged = false,
                    NewValue = userWhoAmI.Token,
                    OldValue = "",
                    Token = maniphestTask.Token,
                    Type = "owner"
                },
                new Phabricator.Data.Transaction {
                    Author = userWhoAmI.Token,
                    DateModified = DateTimeOffset.Now,
                    ID = "150176",
                    IsStaged = false,
                    NewValue = maniphestTask.Priority,
                    OldValue = "",
                    Token = maniphestTask.Token,
                    Type = "priority"
                },
            };
            maniphestStorage.Add(Database, maniphestTask);
            foreach (var transaction in maniphestTask.Transactions)
            {
                transactionStorage.Add(Database, transaction);
            }


            maniphestTask = new Phabricator.Data.Maniphest();
            maniphestTask.ID = "2145";
            maniphestTask.Token = "PHID-TASK-DEEPPURPLE";
            maniphestTask.Name = "Play the intro of Child In Time";
            maniphestTask.Description = "G2 G2 A2";
            maniphestTask.Author = userWhoAmI.Token;
            maniphestTask.DateModified = DateTimeOffset.Now;
            maniphestTask.IsOpen = true;
            maniphestTask.Owner = userWhoAmI.Token;
            maniphestTask.Priority = "80";
            maniphestTask.Projects = "PHID-PROJ-music";
            maniphestTask.Status = "open";
            maniphestTask.Subscribers = "";
            maniphestTask.Transactions = new Phabricator.Data.Transaction[] {
                new Phabricator.Data.Transaction {
                    Author = userWhoAmI.Token,
                    DateModified = DateTimeOffset.Now,
                    ID = "150185",
                    IsStaged = false,
                    NewValue = userWhoAmI.Token,
                    OldValue = "",
                    Token = maniphestTask.Token,
                    Type = "owner"
                },
                new Phabricator.Data.Transaction {
                    Author = userWhoAmI.Token,
                    DateModified = DateTimeOffset.Now,
                    ID = "150186",
                    IsStaged = false,
                    NewValue = maniphestTask.Priority,
                    OldValue = "",
                    Token = maniphestTask.Token,
                    Type = "priority"
                },
            };
            maniphestStorage.Add(Database, maniphestTask);
            foreach (var transaction in maniphestTask.Transactions)
            {
                transactionStorage.Add(Database, transaction);
            }

            Phabricator.Data.Transaction comment = new Phabricator.Data.Transaction();
            comment.Author = userWhoAmI.Token;
            comment.DateModified = DateTimeOffset.Now;
            comment.ID = "4521";
            comment.IsStaged = false;
            comment.NewValue = "C3 is preferred, but B3 is also allowed {F1234, size=full}";
            comment.OldValue = "";
            comment.Token = maniphestTask.Token;
            comment.Type = "comment";
            transactionStorage.Add(Database, comment);

            Phabricator.Data.File file = new Phabricator.Data.File();
            file.DateModified = DateTimeOffset.Now;
            file.FileName = "small.png";
            file.FileType = Phabricator.Data.File.FileStyle.Image;
            file.ID = 1234;
            file.MacroName = ":mymacro";
            file.Token = "PHID-FILE-SMALLPNG";
            file.Size = 391;
            file.Data = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x18, 0x08, 0x03, 0x00, 0x00, 0x00, 0xD7, 0xA9, 0xCD,
                0xCA, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
                0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
                0x00, 0x42, 0x50, 0x4C, 0x54, 0x45, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x0A, 0x0A, 0x0A, 0x7F,
                0x6A, 0x00, 0xFF, 0xD8, 0x00, 0x10, 0x10, 0x10, 0x09, 0x09, 0x09, 0x1D, 0x1D, 0x1D, 0x11, 0x11,
                0x11, 0x1B, 0x1B, 0x1B, 0x0F, 0x0F, 0x0F, 0x25, 0x25, 0x25, 0x3A, 0x3A, 0x3A, 0x12, 0x12, 0x12,
                0xFF, 0x00, 0x00, 0x14, 0x14, 0x14, 0x03, 0x03, 0x03, 0x02, 0x02, 0x02, 0x05, 0x05, 0x05, 0x06,
                0x06, 0x06, 0x0D, 0x0D, 0x0D, 0x00, 0x00, 0x00, 0x32, 0xB6, 0x87, 0x3F, 0x00, 0x00, 0x00, 0x16,
                0x74, 0x52, 0x4E, 0x53, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x01, 0xD2, 0xC0, 0xE4, 0x00, 0x00,
                0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC2, 0x00, 0x00, 0x0E, 0xC2, 0x01, 0x15,
                0x28, 0x4A, 0x80, 0x00, 0x00, 0x00, 0xAC, 0x49, 0x44, 0x41, 0x54, 0x28, 0x53, 0xA5, 0x92, 0xE9,
                0x12, 0x83, 0x20, 0x0C, 0x84, 0x83, 0xDB, 0xFB, 0x40, 0xDB, 0x0E, 0xEF, 0xFF, 0xAA, 0xDD, 0x5C,
                0xA8, 0xCC, 0xF4, 0x57, 0x57, 0x21, 0xB0, 0x1F, 0x31, 0x0A, 0x4A, 0xFB, 0xA1, 0x0E, 0xA4, 0x08,
                0x35, 0xAD, 0xF3, 0x08, 0x82, 0x83, 0x0A, 0x84, 0xE1, 0x78, 0xEF, 0xB6, 0x49, 0x8E, 0x6E, 0x8D,
                0x3E, 0xB3, 0x4E, 0x09, 0x24, 0x9C, 0x14, 0xCE, 0x0E, 0xF6, 0xEB, 0x55, 0x5A, 0x47, 0xEF, 0x98,
                0x6E, 0x34, 0x29, 0xF0, 0x04, 0x44, 0x9A, 0x47, 0xA6, 0xF0, 0xD2, 0xD1, 0x05, 0xB8, 0x6A, 0xBC,
                0xE1, 0xFE, 0x60, 0x40, 0x02, 0x5D, 0x67, 0x4D, 0xC7, 0xBA, 0xA0, 0x67, 0x8C, 0xFA, 0x03, 0xD8,
                0x83, 0x5D, 0x56, 0xC7, 0x80, 0x8E, 0x4A, 0x27, 0x78, 0xB2, 0xB3, 0xD7, 0xF5, 0xD7, 0x4A, 0x82,
                0x52, 0xD9, 0xC7, 0x97, 0x7B, 0xF2, 0x0C, 0x6A, 0x79, 0xD5, 0x5A, 0x73, 0x4B, 0xA2, 0x3C, 0xDE,
                0x22, 0x1F, 0xA1, 0x5F, 0xAB, 0x99, 0x6C, 0xEB, 0x36, 0x9A, 0xBD, 0x01, 0x03, 0xB1, 0x43, 0x74,
                0xC0, 0xA3, 0x75, 0xD6, 0xFD, 0x04, 0x1C, 0xB0, 0x04, 0x7F, 0x86, 0x25, 0x0F, 0xBD, 0x83, 0xBD,
                0x5A, 0xFB, 0x02, 0x35, 0x8B, 0x14, 0xF0, 0xA4, 0xFD, 0x0F, 0x59, 0x00, 0x00, 0x00, 0x00, 0x49,
                0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };
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
