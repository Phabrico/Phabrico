using Newtonsoft.Json;
using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents a DiagramInfo record from the SQLite Phabrico database
    /// </summary>
    public class Diagram : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify diagram objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-DGVN-";

        private Base64EIDOStream _dataStream;
        private string _templateFileName = "";

        /// <summary>
        /// Stream which represents the content of the diagram
        /// </summary>
        [JsonIgnore]
        public Base64EIDOStream DataStream
        {
            get
            {
                return _dataStream;
            }

            set
            {
                _dataStream = value;

                if (_dataStream != null)
                {
                    // seek back to begin
                    _dataStream.Seek(0, SeekOrigin.Begin);
                }
            }
        }

        /// <summary>
        /// Used by serialization/deserialization during loading/storing the object from/into the local database
        /// </summary>
        [JsonIgnore]
        public byte[] Data
        {
            get
            {
                if (_dataStream == null)
                {
                    return new byte[0];
                }

                byte[] decodedData = new byte[_dataStream.Length];
                _dataStream.Seek(0, SeekOrigin.Begin);
                _dataStream.Read(decodedData, 0, decodedData.Length);
                return decodedData;
            }

            set
            {
                DataStream = new Base64EIDOStream();
                DataStream.WriteDecodedData(value);
            }
        }

        /// <summary>
        /// Timestamp when diagram was modified/created
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// Name of the diagram
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// ID of diagram
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Size of the file
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Initializes a new diagram instance
        /// </summary>
        public Diagram()
        {
            TokenPrefix = Prefix;

            DateModified = DateTimeOffset.Now;
        }

        /// <summary>
        /// Clones a new Diagram instance
        /// </summary>
        /// <param name="originalFile"></param>
        public Diagram(Diagram originalFile)
            : base(originalFile)
        {
            TokenPrefix = Prefix;

            this.FileName = originalFile.FileName;
            this.TokenPrefix = Prefix;
            this.ID = originalFile.ID;
            this.DataStream = originalFile.DataStream;
            this.Size = originalFile.Size;
            this.DateModified = originalFile.DateModified;
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        public override void Dispose()
        {
            _dataStream = null;
        }

        /// <summary>
        /// Returns true if the image contains some specific EXIF metadata
        /// </summary>
        /// <param name="metadataQuery">Query to find metadata</param>
        /// <returns>True if metadata exists in image</returns>
        public bool HasMetadata(string metadataQuery)
        {
            if (_dataStream == null)
            {
                return false;
            }

            // search for metadata in image
            _dataStream.Seek(0, SeekOrigin.Begin);
            BitmapSource img = BitmapFrame.Create(_dataStream);
            BitmapMetadata md = (BitmapMetadata)img.Metadata;
            bool result = md.GetQuery(metadataQuery) != null;

            // set position in stream back to beginning
            _dataStream.Seek(0, SeekOrigin.Begin);

            return result;
        }
    }
}