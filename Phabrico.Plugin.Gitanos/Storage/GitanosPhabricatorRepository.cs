using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Phabrico.Plugin.Phabricator.Data;
using Phabrico.Storage;

namespace Phabrico.Plugin.Storage
{
    class GitanosPhabricatorRepository : Phabrico.Storage.PhabricatorObject<Phabricator.Data.Diffusion>
    {
        public override void Add(Database database, Diffusion diffusion)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       INSERT OR REPLACE INTO gitanosPhabricatorRepositories(name, uri, callsign, shortname, description, datemodified)
                       VALUES (@name, @uri, @callsign, @shortname, @description, @datemodified);
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "name", diffusion.Name, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "uri", diffusion.URI, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "callsign", diffusion.CallSign, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "shortname", diffusion.ShortName, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "description", diffusion.Description, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "datemodified", diffusion.DateModified, Database.EncryptionMode.None);
                dbCommand.ExecuteNonQuery();
            }
        }

        public override IEnumerable<Diffusion> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, uri, callsign, shortname, description, datemodified
                       FROM gitanosPhabricatorRepositories;
                   ",
                   database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Diffusion record = new Phabricator.Data.Diffusion();
                        record.Name = (string)reader["name"];
                        record.URI = (string)reader["uri"];
                        record.CallSign = (string)reader["callsign"];
                        record.ShortName = (string)reader["shortname"];
                        record.Description = (string)reader["description"];
                        record.DateModified = DateTimeOffset.Parse((string)reader["datemodified"]);

                        yield return record;
                    }
                }
            }
        }

        public override Diffusion Get(Database database, string key, bool ignoreStageData)
        {
            throw new NotImplementedException();
        }
    }
}
