using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an AccountInfo record from the SQLite Phabrico database
    /// </summary>
    public class Account : PhabricatorObject
    {
        public enum AccountTypes
        {
            /// <summary>
            /// Role for primary user (=administrator, all access)
            /// </summary>
            PrimaryUser = 0,

            /// <summary>
            /// Role for secondary users (=standard user, limmited access)
            /// </summary>
            SecondaryUser = 1
        }

        /// <summary>
        /// How bright or light images in the Dark theme should be darkened
        /// </summary>
        public enum DarkenImageStyle
        {
            /// <summary>
            /// No darkening: images are not modified
            /// </summary>
            Disabled,

            /// <summary>
            /// Images are darkened by means a dark shading effect
            /// </summary>
            Moderate,

            /// <summary>
            /// Images are darkened by means of a filter (this might cause some color changes)
            /// </summary>
            Extreme
        }

        /// <summary>
        /// How new modifications should be tagged
        /// </summary>
        public enum DefaultStateModification
        {
            /// <summary>
            /// New modifications should be immediately available for upload to Phabricator
            /// </summary>
            Unfrozen,

            /// <summary>
            /// New modifications should not be immediately available for upload to Phabricator
            /// </summary>
            Frozen
        }

        /// <summary>
        /// Duration how long data should be kept in the Phabrico SQLite database
        /// </summary>
        public enum RemovalPeriod
        {
            /// <summary>
            /// Maximum 1 day
            /// </summary>
            RemovalPeriod1Day,

            /// <summary>
            /// Maximum 1 week
            /// </summary>
            RemovalPeriod1Week,

            /// <summary>
            /// Maximum 2 weeks
            /// </summary>
            RemovalPeriod2Weeks,

            /// <summary>
            /// Maximum 1 month
            /// </summary>
            RemovalPeriod1Month,

            /// <summary>
            /// Maximum 3 months
            /// </summary>
            RemovalPeriod3Months,

            /// <summary>
            /// Maximum 6 months
            /// </summary>
            RemovalPeriod6Months,

            /// <summary>
            /// Maximum 1 year
            /// </summary>
            RemovalPeriod1Year,

            /// <summary>
            /// Maximum 10 years
            /// </summary>
            RemovalPeriod10Years
        }

        /// <summary>
        /// Bitwise parameters which tell how the data to be downloaded from Phabricator should be filtered
        ///    NNNN NNNN UUUU UUUU PPPP PPPP
        ///    3210 9876 5432 1098 7654 3210
        ///    2222 1111 1111 1100 0000 0000
        ///    
        ///     P: Project filters
        ///     U: User filters
        ///     N: No filtering
        /// </summary>
        [Flags]
        public enum SynchronizationMethod
        {
            /// <summary>
            /// Initial state
            /// </summary>
            None = 0x000000,

            /// <summary>
            /// Is only used for unit testing
            /// </summary>
            All = 0xFFFFFF,

            /// <summary>
            /// Synchronization based on per-user-selection
            /// </summary>
            PerUsers = 0x008000,

            /// <summary>
            /// Synchronization based on per-project-selection
            /// </summary>
            PerProjects = 0x000080,

            /// <summary>
            /// Maniphest synchronization based on per-user-selection
            /// </summary>
            ManiphestSelectedUsersOnly = 0x008100,

            /// <summary>
            /// Maniphest synchronization based on per-project-selection
            /// </summary>
            ManiphestSelectedProjectsOnly = 0x000081,

            /// <summary>
            /// Phriction synchronization based on per-user-selection
            /// </summary>
            PhrictionSelectedUsersOnly = 0x008200,

            /// <summary>
            /// Phriction synchronization based on per-project-selection
            /// </summary>
            PhrictionSelectedProjectsOnly = 0x000082,

            /// <summary>
            /// Phriction synchronization based on per-project-selection and including all child-documents
            /// </summary>
            PhrictionSelectedProjectsOnlyIncludingDocumentTree = 0x000086,

            /// <summary>
            /// Phriction synchronization based on per-project-selection
            /// </summary>
            PhrictionAllSelectedProjectsOnly = 0x00008A,

            /// <summary>
            /// Phriction synchronization based on per-project-selection and including all child-documents
            /// </summary>
            PhrictionAllSelectedProjectsOnlyIncludingDocumentTree = 0x00008E,

            /// <summary>
            /// Full Phriction synchronization
            /// </summary>
            PhrictionAllProjects = 0x820000
        }

        /// <summary>
        /// The Conduit API token to be used for synchronizing with the phabricator server
        /// </summary>
        public string ConduitAPIToken { get; set; }

        /// <summary>
        /// Collection of several configuration parameters
        /// </summary>
        public Configuration Parameters { get; set; }

        /// <summary>
        /// URL to the phabricator server to be used for synchronizing
        /// </summary>
        public string PhabricatorUrl { get; set; }

        /// <summary>
        /// Color theme to be used by Phabrico
        /// </summary>
        public string Theme { get; set; } = "light";

        /// <summary>
        /// Username you can login to Phabrico with
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// XOR value for decrypting the public database encryption key
        /// </summary>
        public UInt64[] PublicXorCipher { get; set; }

        /// <summary>
        /// XOR value for decrypting the private database encryption key
        /// </summary>
        public UInt64[] PrivateXorCipher { get; set; }

        /// <summary>
        /// XOR value for decrypting the DPAPI (Windows Authentication) database encryption key
        /// </summary>
        public UInt64[] DpapiXorCipher1 { get; set; }

        /// <summary>
        /// XOR value for decrypting the DPAPI (Windows Authentication) database encryption key
        /// </summary>
        public UInt64[] DpapiXorCipher2 { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Account()
        {
            Parameters = new Configuration();

            PublicXorCipher = new UInt64[] { 0, 0, 0, 0 };
            PrivateXorCipher = new UInt64[] { 0, 0, 0, 0 };
            DpapiXorCipher1 = new UInt64[] { 0, 0, 0, 0 };
            DpapiXorCipher2 = new UInt64[] { 0, 0, 0, 0 };
        }

        /// <summary>
        /// Sub-class containing all possible configuration parameters
        /// This class will be serialized stored into the SQLite account table
        /// </summary>
        public class Configuration
        {
            /// <summary>
            /// Determines the access level of the account
            /// </summary>
            public AccountTypes AccountType { get; set; }
            
            /// <summary>
            /// If no mouse movement or keyboard activity is detected in the active Phabrico screen for a given number of minutes, the Log In dialog will be shown.
            /// The only exception is when an Edit screen is active (e.g. Edit Phriction Document, Edit Maniphest Task, ...)
            /// If set to 0 or lower, the auto log off functionality is disabled
            /// </summary>
            public int AutoLogOutAfterMinutesOfInactivity { get; set; } = 5;

            /// <summary>
            /// If set to true, all code blocks will get a 'Copy' button which will be visible when hovering over the code block with the mouse.
            /// This button will copy the content of the code block to the clipboard when clicked.
            /// </summary>
            public bool ClipboardCopyForCodeBlock { get; set; } = true;

            /// <summary>
            /// Determines which column cell-values should be hidden until hovered over with the mouse
            /// </summary>
            public string[] ColumnHeadersToHide { get; set; }

            /// <summary>
            /// If the Dark theme is selected and this property is set to Moderate or Extreme, bright/light images will be darkened by means of CSS filters.
            /// If the majority of the pixels in the 4 corners and the center contain a light color, the image is marked as 'bright/light' and can be darkened.
            /// If the majority of the pixels in the corners and the center does not contain a light color, no darkening will be executed.
            /// </summary>
            public DarkenImageStyle DarkenBrightImages { get; set; }

            /// <summary>
            /// Initial state for new Maniphest tasks
            /// </summary>
            public DefaultStateModification DefaultStateModifiedManiphest { get; set; }

            /// <summary>
            /// Initial state for new Phriction documents
            /// </summary>
            public DefaultStateModification DefaultStateModifiedPhriction { get; set; }

            /// <summary>
            /// Tag for secondary user accounts which identify the user role to which the user belongs to
            /// </summary>
            public string DefaultUserRoleTag { get; set; }

            /// <summary>
            /// Phabricator currently doesn't notify if a subscribers or a project tag has been added or removed via the Conduit API.
            /// If this parameter is set to true, the metadata of all phriction documents is downloaded during the synchronization process.
            /// </summary>
            public bool ForceDownloadAllPhrictionMetadata { get; set; } = false;

            /// <summary>
            /// If set to false metadata like 'Last modified by/at' and 'Tags' will not be shown in Phriction's action menu on the right
            /// </summary>
            public bool ShowPhrictionMetadata { get; set; } = true;

            /// <summary>
            /// Timestamp of the last time the Phabrico database was synchronized with the Phabricator server
            /// </summary>
            public DateTimeOffset LastSynchronizationTimestamp { get; set; } = DateTimeOffset.MinValue;

            /// <summary>
            /// Determines which Maniphest tasks should be downloaded from the Phabricator server or removed from the
            /// local database after the Phabricator synchronization process is finished
            /// </summary>
            public RemovalPeriod RemovalPeriodClosedManiphests { get; set; }

            /// <summary>
            /// Determines which phabricator items should be synchronized
            /// </summary>
            public SynchronizationMethod Synchronization { get; set; } = SynchronizationMethod.PhrictionAllProjects | SynchronizationMethod.ManiphestSelectedUsersOnly;

            /// <summary>
            /// If set to true, the title in the NOTE, WARNING and IMPORTANT notifications in Remarkup will be translated in the current UI language.
            /// If set to false, the titles will be shown in english
            /// </summary>
            public bool UITranslation { get; set; } = false;

            /// <summary>
            /// The Phabricator token of the user whose Conduit API token is used
            /// </summary>
            public string UserToken { get; set; }
        }
    }
}
