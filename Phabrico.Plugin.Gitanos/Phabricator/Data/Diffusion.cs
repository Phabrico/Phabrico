using System;

namespace Phabrico.Plugin.Phabricator.Data
{
    class Diffusion
    {
        public string CallSign { get; set; }
        public DateTimeOffset DateModified { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Status { get; set; }
        public string URI { get; set; }

        public string DefaultCloneDestination { get; set; }
    }
}
