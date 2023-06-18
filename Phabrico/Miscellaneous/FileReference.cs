namespace Phabrico.Miscellaneous
{
    internal class FileReference
    {
        /// <summary>
        /// Numeric file id that is referenced in a wiki or task
        /// </summary>
        internal int FileID { get; set; }

        /// <summary>
        /// Token of referenced wiki or task
        /// </summary>
        internal string LinkedToken { get; set; }
        
        /// <summary>
        /// Title of referenced wiki or task
        /// (is not stored in database)
        /// </summary>
        internal string LinkedDescription { get; set; }

        /// <summary>
        /// URL of referenced wiki or task
        /// (is not stored in database)
        /// </summary>
        internal string LinkedURL { get; set; }
    }
}
