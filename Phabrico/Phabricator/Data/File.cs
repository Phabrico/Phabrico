using Newtonsoft.Json;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an FileInfo record from the SQLite Phabrico database
    /// </summary>
    public class File : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify file objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-FILE-";

        /// <summary>
        /// Subclass which represents a chunk of file which was being uploaded to Phabrico
        /// </summary>
        public class Chunk
        {
            /// <summary>
            /// ID to file to where this chunk belongs to
            /// </summary>
            public int FileID { get; set; }

            /// <summary>
            /// Unique ID for chunk
            /// </summary>
            public int ChunkID { get; set; }

            /// <summary>
            /// Number of expected chunks to be uploaded
            /// </summary>
            public int NbrChunks { get; set; }

            /// <summary>
            /// Timestamp when chunk was uploaded
            /// </summary>
            public DateTimeOffset DateModified { get; set; }

            /// <summary>
            /// Content of file chunk
            /// </summary>
            public byte[] Data { get; set; }
        }

        private string _contentType = "text/plain";
        private Base64EIDOStream _dataStream;
        private string _templateFileName = "";

        /// <summary>
        /// Represents the type of file
        /// </summary>
        public enum FileStyle
        {
            /// <summary>
            /// Audio-typed file
            /// </summary>
            Audio,

            /// <summary>
            /// Image-typed file
            /// </summary>
            Image,

            /// <summary>
            /// Generic file
            /// </summary>
            Other,

            /// <summary>
            /// Video-typed file
            /// </summary>
            Video
        }

        /// <summary>
        /// Represents the Content-Type that is sent to the browser to identify the content
        /// Is calculated by SetContentType() when downloaded from Phabricator
        /// or set directly when loaded from local database
        /// </summary>
        public string ContentType
        {
            get
            {
                return _contentType;
            }

            set
            {
                if (value.StartsWith("image/"))
                {
                    FileType = FileStyle.Image;
                }
                else
                if (value.StartsWith("video/"))
                {
                    FileType = FileStyle.Video;
                }
                else
                if (value.StartsWith("audio/"))
                {
                    FileType = FileStyle.Audio;
                }
                else
                {
                    FileType = FileStyle.Other;
                }

                _contentType = value;
            }
        }

        /// <summary>
        /// Stream which represents the content of the file
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
                    // read first bytes to detect the type of file
                    byte[] header = new byte[256];
                    _dataStream.Seek(0, SeekOrigin.Begin);
                    int headerLength = _dataStream.Read(header, 0, header.Length);
                    SetContentType(header.Take(headerLength).ToArray());

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

                // read first bytes to detect the type of file
                int headerLength = 256;
                SetContentType(value.Take(headerLength).ToArray());
            }
        }

        /// <summary>
        /// Timestamp when file was modified/created
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// Name of the file
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Represents the type of content to make Phabrico it easier to identify for representation
        /// E.g. an image is shown embedded, a PDF is shown as a download link
        /// Is calculated by SetContentType()
        /// </summary>
        public FileStyle FileType { get; set; }

        /// <summary>
        /// Represents the FontAwesome icon in the download link when ContentType is Other
        /// If ContentType is different than Other, FontAwesomeIcon will not be used and can be set to null
        /// Also: if FontAwesomeIcon is set to null, the file will be seen as a download attachment:
        /// when the user clicks on the download link, a dialog will appear in where the user can select
        /// the destination where to save the file to
        /// Is calculated by SetContentType()
        /// </summary>
        public string FontAwesomeIcon { get; set; }

        /// <summary>
        /// Hash value used by Phabricator API 'file.allocate' to differentiate/identify new file uploads
        /// </summary>
        public string Hash
        {
            get
            {
                if (_dataStream == null)
                {
                    return "";
                }

                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] sha256HashBytes = sha256Hash.ComputeHash(_dataStream);
                    return Convert.ToBase64String(sha256HashBytes);
                }
            }
        }

        /// <summary>
        /// ID of file
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Name of macro which points to this file
        /// </summary>
        public string MacroName { get; set; }

        /// <summary>
        /// This property is not part of the fileInfo table but contains the number of references of the current file token in the objectHierarchyInfo table.
        /// This value is not calculated with all the Storage methods.
        /// The Storage.File.GetReferenceInfo method for example will calculate this value
        /// </summary>
        public int NumberOfReferences { get; set; }

        /// <summary>
        /// In case an existing non-staged file object is staged, a new file ID will be generated.
        /// This property will contain the original ID.
        /// </summary>
        public int OriginalID { get; set; }

        /// <summary>
        /// Contains some file-type specific properties.
        /// E.g. for an image a Width property and a Height property exist
        /// </summary>
        private Dictionary<string, string> Property = new Dictionary<string, string>();

        /// <summary>
        /// Contains some file-type specific properties.
        /// E.g. for an image a Width property and a Height property exist
        /// This property is a wrapper for the Property dictionary, which contains the real content
        /// </summary>
        public string Properties
        {
            get
            {
                return string.Join(",", Property.Select(parameter => parameter.Key + "=" + parameter.Value));
            }

            set
            {
                Property = value.Split(',')
                                .Where(parameter => parameter.Contains('='))
                                .Select(parameter => parameter.Split('='))
                                .ToDictionary(propertyName => propertyName[0].Trim(), 
                                              propertyValue => propertyValue[1].Trim()
                                             );
            }
        }

        /// <summary>
        /// Size of the file
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Template string for building a filename
        /// The "{0}" string in this TemplateFileName string will be replaced with the file ID (e.g. F-1234)
        /// and forms the FileName to be used
        /// E.g. "PLAN-{0}.DWG" may generate a FileName property "PLAN-F1234.DWG" (if the ID is 1234)
        /// </summary>
        [JsonIgnore]
        public string TemplateFileName
        {
            get
            {
                return _templateFileName;
            }

            set
            {
                _templateFileName = value;

                FileName = _templateFileName.Replace("{0}", "F" + ID);
            }
        }

        /// <summary>
        /// Initializes a new File instance
        /// </summary>
        public File()
        {
            TokenPrefix = Prefix;

            DateModified = DateTimeOffset.Now;
            FontAwesomeIcon = "fa-file-text-o";
            FileType = FileStyle.Other;
            Properties = "";
        }

        /// <summary>
        /// Clones a new File instance
        /// </summary>
        /// <param name="originalFile"></param>
        public File(File originalFile)
            : base(originalFile)
        {
            TokenPrefix = Prefix;

            this.FileName = originalFile.FileName;
            this.TokenPrefix = Prefix;
            this.ID = originalFile.ID;
            this.DataStream = originalFile.DataStream;
            this.Size = originalFile.Size;
            this.DateModified = originalFile.DateModified;
            this.Properties = originalFile.Properties;
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

        /// <summary>
        /// Represents the image width in case the file represents an image
        /// </summary>
        public int ImagePropertyPixelWidth
        {
            get
            {
                string value;
                if (Property.TryGetValue("ImageWidth", out value) == false)
                {
                    return 0;
                }

                return Int32.Parse(value);
            }

            set
            {
                Property["ImageWidth"] = value.ToString();
            }
        }

        /// <summary>
        /// Represents the image height in case the file represents an image
        /// </summary>
        public int ImagePropertyPixelHeight
        {
            get
            {
                string value;
                if (Property.TryGetValue("ImageHeight", out value) == false)
                {
                    return 0;
                }

                return Int32.Parse(value);
            }

            set
            {
                Property["ImageHeight"] = value.ToString();
            }
        }

        /// <summary>
        /// Sets the MIME content type of the file based on the first bytes of its content
        /// </summary>
        /// <param name="fileData"></param>
        private void SetContentType(byte[] fileData)
        {
            if (fileData == null) return;

            // set default
            ContentType = "text/plain";

            try
            {
                if (fileData.Length >= 12)
                {
                    if ((fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF && fileData[3] == 0xE0 &&
                         fileData[4] == 0x00 && fileData[5] == 0x10 && fileData[6] == 0x4A && fileData[7] == 0x46 &&
                         fileData[8] == 0x49 && fileData[9] == 0x46 && fileData[10] == 0x00 && fileData[11] == 0x01
                        ) ||
                        (fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF && fileData[3] == 0xE1 &&
                         fileData[6] == 0x45 && fileData[7] == 0x78 && fileData[8] == 0x69 && fileData[9] == 0x66 &&
                         fileData[10] == 0x00 && fileData[11] == 0x00
                        ))
                    {
                        ContentType = "image/jpeg";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 'R' && fileData[1] == 'I' && fileData[2] == 'F' && fileData[3] == 'F' &&
                        fileData[8] == 'W' && fileData[9] == 'A' && fileData[10] == 'V' && fileData[11] == 'E'
                        )
                    {
                        ContentType = "audio/x-wav";
                        FontAwesomeIcon = null;
                    }


                    if (fileData[0] == 'R' && fileData[1] == 'I' && fileData[2] == 'F' && fileData[3] == 'F' &&
                        fileData[8] == 'A' && fileData[9] == 'V' && fileData[10] == 'I' && fileData[11] == ' '
                        )
                    {
                        ContentType = "video/vnd.avi";
                        FontAwesomeIcon = null;
                    }
                }

                if (fileData.Length >= 8)
                {
                    if (fileData[0] == 0x00 && fileData[4] == 'f' && fileData[5] == 't' && fileData[6] == 'y' && fileData[7] == 'p')
                    {
                        ContentType = "video/mp4";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 'u' && fileData[1] == 's' && fileData[2] == 't' && fileData[3] == 'a' && fileData[4] == 'r' &&
                        ((fileData[5] == 0x00 && fileData[6] == 0x30 && fileData[7] == 0x30) ||
                         (fileData[5] == 0x20 && fileData[6] == 0x20 && fileData[7] == 0x00)))
                    {
                        ContentType = "application/x-tar";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }

                    if (fileData[0] == 0xD0 && fileData[1] == 0xCF && fileData[2] == 0x11 && fileData[3] == 0xE0 && 
                        fileData[4] == 0xA1 && fileData[5] == 0xB1 && fileData[6] == 0x1A && fileData[7] == 0xE1)
                    {
                        // Compound File Binary Format, a container format used for document by older versions of Microsoft Office
                        string fileExtension = Path.GetExtension(FileName).ToUpper();
                        switch (fileExtension)
                        {
                            case ".MSG":
                                ContentType = "application/vnd.ms-outlook";
                                FontAwesomeIcon = "fa-envelope-o";
                                break;

                            case ".DOC":
                                ContentType = "application/msword";
                                FontAwesomeIcon = "fa-file-word-o";
                                break;

                            case ".XLS":
                                ContentType = "application/vnd.ms-excel";
                                FontAwesomeIcon = "fa-file-excel-o";
                                break;

                            case ".PPT":
                                ContentType = "application/vnd.ms-powerpoint";
                                FontAwesomeIcon = "fa-file-powerpoint-o";
                                break;

                            default:
                                break;
                        }
                    }
                }

                if (fileData.Length >= 7)
                {
                    if (fileData[0] == 0xFD && fileData[1] == 0x37 && fileData[2] == 0x7A && fileData[3] == 0x58 && fileData[4] == 0x5A && fileData[5] == 0x00 && fileData[6] == 0x00)
                    {
                        ContentType = "application/x-xz";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }
                }

                if (fileData.Length >= 6)
                {
                    if (fileData[0] == 'G' && fileData[1] == 'I' && fileData[2] == 'F' && fileData[3] == '8' && (fileData[4] == '7' || fileData[4] == '9') && fileData[5] == 'a')
                    {
                        ContentType = "image/gif";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 0x37 && fileData[1] == 0x7A && fileData[2] == 0xBC && fileData[3] == 0xAF && fileData[4] == 0x27 && fileData[5] == 0x1C)
                    {
                        ContentType = "application/x-7z-compressed";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }

                    if (fileData[0] == 'R' && fileData[1] == 'a' && fileData[2] == 'r' && fileData[3] == '!' && (fileData[4] == 0x1A || fileData[5] == 0x07))
                    {
                        ContentType = "application/vnd.rar";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }
                }

                if (fileData.Length >= 4)
                {
                    if (fileData[0] == 0x89 && fileData[1] == 'P' && fileData[2] == 'N' && fileData[3] == 'G')
                    {
                        ContentType = "image/png";
                        FontAwesomeIcon = null;

                        // check if Diagrams.net (Draw.io) metadata is stored in PNG image
                        if (HasMetadata("/Text/{str=mxfile}"))
                        {
                            // Diagrams.net metadata found -> change content-type
                            ContentType = "image/drawio";
                            FontAwesomeIcon = "fa-sitemap";
                        }
                    }

                    if (fileData[0] == 'f' && fileData[1] == 'L' && fileData[2] == 'a' && fileData[3] == 'C')
                    {
                        ContentType = "audio/flac";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 0x1A && fileData[1] == 0x45 && fileData[2] == 0xDF && fileData[3] == 0xA3)
                    {
                        ContentType = "video/x-matroska";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 'M' && fileData[1] == 'T' && fileData[2] == 'h' && fileData[3] == 'd')
                    {
                        ContentType = "audio/midi";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == '%' && fileData[1] == 'P' && fileData[2] == 'D' && fileData[3] == 'F')
                    {
                        ContentType = "application/pdf";
                        FontAwesomeIcon = "fa-file-pdf-o";
                    }

                    if ((fileData[0] == 'I' && fileData[1] == 'I' && fileData[2] == '*' && fileData[3] == 0x00) ||
                        (fileData[0] == 'M' && fileData[1] == 'M' && fileData[2] == 0x00 && fileData[3] == '*'))
                    {
                        ContentType = "image/tiff";
                        FontAwesomeIcon = null;
                    }

                    if ((fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF && fileData[3] == 0xDB) ||
                        (fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF && fileData[3] == 0xEE))
                    {
                        ContentType = "image/jpeg";
                        FontAwesomeIcon = null;
                    }

                    if ((fileData[0] == 0x00 && fileData[1] == 0x00 && fileData[2] == 0x01 && fileData[3] == 0xB3) ||
                        (fileData[0] == 0x00 && fileData[1] == 0x00 && fileData[2] == 0x01 && fileData[3] == 0xBA))
                    {
                        ContentType = "video/x-mpeg";
                        FontAwesomeIcon = null;
                    }
                }

                if (fileData.Length >= 3)
                {
                    if ((fileData[0] == 'I' && fileData[1] == 'D' && fileData[2] == '3') ||
                        (fileData[0] == 0xFF && fileData[1] == 0xFB)
                       )
                    {
                        ContentType = "audio/mpeg";
                    }

                    if (fileData[0] == 'B' && fileData[1] == 'Z' && fileData[2] == 'h')
                    {
                        ContentType = "application/x-bzip2";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }
                }

                if (fileData.Length >= 2)
                {
                    if (fileData[0] == 0x1F && fileData[1] == 0x8B)
                    {
                        ContentType = "application/gzip";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }

                    if (fileData[0] == 'B' && fileData[1] == 'M')
                    {
                        ContentType = "image/bmp";
                        FontAwesomeIcon = null;
                    }

                    if (fileData[0] == 'P' && fileData[1] == 'K')
                    {
                        ContentType = "application/zip";
                        FontAwesomeIcon = "fa-file-archive-o";

                        string fileExtension = Path.GetExtension(FileName).ToUpper();
                        switch (fileExtension)
                        {
                            case ".DOCX":
                                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                                FontAwesomeIcon = "fa-file-word-o";
                                break;

                            case ".XLSX":
                                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                                FontAwesomeIcon = "fa-file-excel-o";
                                break;

                            case ".PPTX":
                                ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                                FontAwesomeIcon = "fa-file-powerpoint-o";
                                break;

                            default:
                                break;
                        }
                    }

                    if (fileData[0] == 0x1F && (fileData[1] == 0x9D || fileData[1] == 0xA0))
                    {
                        ContentType = "application/gzip";
                        FontAwesomeIcon = "fa-file-archive-o";
                    }
                }
            }
            catch (System.Exception e)
            {
                // some error occurred: do nothing but logging and keep default settings
                Logging.WriteException(null, e);
            }
        }
    }
}