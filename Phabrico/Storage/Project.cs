using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for ProjectInfo table
    /// </summary>
    public class Project : PhabricatorObject<Phabricator.Data.Project>
    {
        /// <summary>
        /// Adds or modifies a ProjectInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="project"></param>
        public override void Add(Database database, Phabricator.Data.Project project)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO projectInfo(token, name, slug, description, color, selected, dateSynchronized) 
                           VALUES (@projectToken, @projectName, @slug, @description, @color, @selected, @dateSynchronized);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "projectToken", project.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "projectName", project.Name);
                    database.AddParameter(dbCommand, "slug", project.InternalName);
                    database.AddParameter(dbCommand, "description", project.Description);
                    database.AddParameter(dbCommand, "color", project.Color);
                    database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, project.Selected.ToString()));
                    database.AddParameter(dbCommand, "dateSynchronized", project.DateSynchronized);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns the number of ProjectInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long Count(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(*) AS result
                       FROM projectInfo
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (long)reader["result"];
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// Returns all ProjectInfo records from the database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Project> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, name, slug, description, color, selected, dateSynchronized
                       FROM projectInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Project record = new Phabricator.Data.Project();
                        record.Token = (string)reader["token"];
                        record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["name"]);
                        record.InternalName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["slug"]);
                        record.Description = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["description"]);
                        record.Selected = (Phabricator.Data.Project.Selection)Enum.Parse(typeof(Phabricator.Data.Project.Selection), Encryption.Decrypt(database.EncryptionKey, (byte[])reader["selected"]));
                        record.DateSynchronized = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateSynchronized"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Color = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["color"]);
                        
                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific ProjectInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="language"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Project Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, name, slug, description, color, selected, dateSynchronized
                       FROM projectInfo
                       WHERE token = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "slug", key, Database.EncryptionMode.Default);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.Project record = new Phabricator.Data.Project();
                        record.Token = (string)reader["token"];
                        record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["name"]);
                        record.InternalName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["slug"]);
                        record.Description = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["description"]);
                        record.Selected = (Phabricator.Data.Project.Selection)Enum.Parse(typeof(Phabricator.Data.Project.Selection), Encryption.Decrypt(database.EncryptionKey, (byte[])reader["selected"]));
                        record.DateSynchronized = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateSynchronized"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Color = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["color"]);

                        return record;
                    }
                }
            }

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, name, slug, description, color, selected, dateSynchronized
                       FROM projectInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string slug = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["slug"]);
                        if (slug.Equals(key, StringComparison.OrdinalIgnoreCase) == false) continue;

                        Phabricator.Data.Project record = new Phabricator.Data.Project();
                        record.Token = (string)reader["token"];
                        record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["name"]);
                        record.InternalName = slug;
                        record.Description = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["description"]);
                        record.Selected = (Phabricator.Data.Project.Selection)Enum.Parse(typeof(Phabricator.Data.Project.Selection), Encryption.Decrypt(database.EncryptionKey, (byte[])reader["selected"]));
                        record.DateSynchronized = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateSynchronized"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Color = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["color"]);

                        return record;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes a project from the database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="project"></param>
        public void Remove(Database database, Phabricator.Data.Project project)
        {
            using (SQLiteCommand cmdDeleteProjectInfo = new SQLiteCommand(@"
                       DELETE FROM projectInfo
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(cmdDeleteProjectInfo, "token", project.Token, Database.EncryptionMode.None);
                cmdDeleteProjectInfo.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Changes the 'selected' state of a ProjectInfo record
        /// If 'selected' than the ProjectInfo record is taken into account for synchronizing with Phabricator
        /// </summary>
        /// <param name="database"></param>
        /// <param name="projectToken"></param>
        /// <param name="projectSelection"></param>
        public void SelectProject(Database database, Language language, string projectToken, Phabricator.Data.Project.Selection projectSelection)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE projectInfo 
                           SET selected = @selected
                       WHERE token LIKE @projectToken;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "projectToken", projectToken, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, projectSelection.ToString()));
                dbCommand.ExecuteNonQuery();
            }

            if (projectSelection == Phabricator.Data.Project.Selection.Selected)
            {
                Phabricator.Data.Project currentProject = Get(database, projectToken, language);
                if (currentProject != null)
                {
                    currentProject.DateSynchronized = DateTimeOffset.MinValue;  // download all when sync'ing

                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                               UPDATE projectInfo 
                                   SET dateSynchronized = @dateSynchronized
                               WHERE token LIKE @projectToken;
                           ", database.Connection))
                    {
                        database.AddParameter(dbCommand, "projectToken", projectToken, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "dateSynchronized", currentProject.DateSynchronized);
                        dbCommand.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
