namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Model class for words in a document or in a task.
    /// Each word that appears in a document or task is stored as a 'Keyword'
    /// </summary>
    public class Keyword : PhabricatorObject
    {
        /// <summary>
        /// The word itself
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The title of the document to where the word belongs to
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Number of times the word appears in the document
        /// </summary>
        public int NumberOccurrences { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Keyword()
        {
        }
    }
}
