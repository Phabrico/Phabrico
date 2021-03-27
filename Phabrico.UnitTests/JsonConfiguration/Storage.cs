using System.Collections.Generic;

namespace Phabrico.UnitTests.JsonConfiguration
{
    public class Storage
    {
        public List<Account> account { get; set; }
        public List<File> file { get; set; }
        public List<Maniphest> maniphest { get; set; }
        public List<ManiphestPriority> maniphestpriority { get; set; }
        public List<ManiphestStatus> manipheststatus { get; set; }
        public List<Phriction> phriction { get; set; }
        public List<Project> project { get; set; }
        public List<User> user { get; set; }
    }
}
