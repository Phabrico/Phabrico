using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Phabrico.Parsers.Base64
{
    /// <summary>
    /// Encrypted-data-IN-Decrypted-data-OUT stream for Base64 data
    /// </summary>
    public class Base64EIDOStream : Stream
    {
        /// <summary>
        /// Base64 translation table for decoding
        /// </summary>
        private Dictionary<char, byte> translateFromBase64 = new Dictionary<char, byte>() {
                { 'A', 0  }, { 'B', 1  }, { 'C', 2  }, { 'D', 3  },
                { 'E', 4  }, { 'F', 5  }, { 'G', 6  }, { 'H', 7  },
                { 'I', 8  }, { 'J', 9  }, { 'K', 10 }, { 'L', 11 },
                { 'M', 12 }, { 'N', 13 }, { 'O', 14 }, { 'P', 15 },
                { 'Q', 16 }, { 'R', 17 }, { 'S', 18 }, { 'T', 19 },
                { 'U', 20 }, { 'V', 21 }, { 'W', 22 }, { 'X', 23 },
                { 'Y', 24 }, { 'Z', 25 }, { 'a', 26 }, { 'b', 27 },
                { 'c', 28 }, { 'd', 29 }, { 'e', 30 }, { 'f', 31 },
                { 'g', 32 }, { 'h', 33 }, { 'i', 34 }, { 'j', 35 },
                { 'k', 36 }, { 'l', 37 }, { 'm', 38 }, { 'n', 39 },
                { 'o', 40 }, { 'p', 41 }, { 'q', 42 }, { 'r', 43 },
                { 's', 44 }, { 't', 45 }, { 'u', 46 }, { 'v', 47 },
                { 'w', 48 }, { 'x', 49 }, { 'y', 50 }, { 'z', 51 },
                { '0', 52 }, { '1', 53 }, { '2', 54 }, { '3', 55 },
                { '4', 56 }, { '5', 57 }, { '6', 58 }, { '7', 59 },
                { '8', 60 }, { '9', 61 }, { '+', 62 }, { '/', 63 }
            };

        /// <summary>
        /// Base64 translation table for encoding
        /// </summary>
        private Dictionary<byte, byte> translateToBase64 = new Dictionary<byte, byte>() {
                {  0, (byte)'A' }, {  1, (byte)'B' }, {  2, (byte)'C' }, {  3, (byte)'D'  },
                {  4, (byte)'E' }, {  5, (byte)'F' }, {  6, (byte)'G' }, {  7, (byte)'H'  },
                {  8, (byte)'I' }, {  9, (byte)'J' }, { 10, (byte)'K' }, { 11, (byte)'L' },
                { 12, (byte)'M' }, { 13, (byte)'N' }, { 14, (byte)'O' }, { 15, (byte)'P' },
                { 16, (byte)'Q' }, { 17, (byte)'R' }, { 18, (byte)'S' }, { 19, (byte)'T' },
                { 20, (byte)'U' }, { 21, (byte)'V' }, { 22, (byte)'W' }, { 23, (byte)'X' },
                { 24, (byte)'Y' }, { 25, (byte)'Z' }, { 26, (byte)'a' }, { 27, (byte)'b' },
                { 28, (byte)'c' }, { 29, (byte)'d' }, { 30, (byte)'e' }, { 31, (byte)'f' },
                { 32, (byte)'g' }, { 33, (byte)'h' }, { 34, (byte)'i' }, { 35, (byte)'j' },
                { 36, (byte)'k' }, { 37, (byte)'l' }, { 38, (byte)'m' }, { 39, (byte)'n' },
                { 40, (byte)'o' }, { 41, (byte)'p' }, { 42, (byte)'q' }, { 43, (byte)'r' },
                { 44, (byte)'s' }, { 45, (byte)'t' }, { 46, (byte)'u' }, { 47, (byte)'v' },
                { 48, (byte)'w' }, { 49, (byte)'x' }, { 50, (byte)'y' }, { 51, (byte)'z' },
                { 52, (byte)'0' }, { 53, (byte)'1' }, { 54, (byte)'2' }, { 55, (byte)'3' },
                { 56, (byte)'4' }, { 57, (byte)'5' }, { 58, (byte)'6' }, { 59, (byte)'7' },
                { 60, (byte)'8' }, { 61, (byte)'9' }, { 62, (byte)'+' }, { 63, (byte)'/' }
            };

        /// <summary>
        /// Internal memory stream which contains the decoded data
        /// </summary>
        private MemoryStream memoryStreamDecodedData = new MemoryStream();

        /// <summary>
        /// Internal memory stream which contains the encoded data
        /// </summary>
        private MemoryStream memoryStreamEncodedData = new MemoryStream();

        /// <summary>
        /// Current Seek position for Read actions
        /// </summary>
        private long positionReadMemoryStreamDecodedData = 0;

        /// <summary>
        /// Current Seek position for Write actions
        /// </summary>
        private long positionWriteMemoryStreamDecodedData = 0;

        /// <summary>
        /// Lock object for read/write actions
        /// The internal MemoryStream has only 1 seek position for both read and write actions.
        /// This lock object will make sure that the seek position will not be overwritten when the read/write action changes
        /// </summary>
        private object synchronizationReadWriteLock = new object();

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Returns a stream of Base64 encoded data
        /// </summary>
        public Stream EncodedData
        {
            get
            {
                if (memoryStreamEncodedData.Length == 0)
                {
                    byte[] buffer = new byte[0x600000];
                    int readOffset = 0;
                    int nbrEncodedBytes = 0;

                    memoryStreamDecodedData.Seek(0, SeekOrigin.Begin);
                    memoryStreamEncodedData.Seek(0, SeekOrigin.Begin);
                    while (true)
                    {
                        int nbrBytesRead = memoryStreamDecodedData.Read(buffer, readOffset, buffer.Length);
                        int bufferIndexer = 0;
                        while (nbrBytesRead > bufferIndexer)
                        {
                            byte encodedBCD1, encodedBCD2, encodedBCD3, encodedBCD4;

                            if (bufferIndexer + 2 < nbrBytesRead)
                            {
                                byte decodedByte1 = buffer[bufferIndexer + 0];
                                byte decodedByte2 = buffer[bufferIndexer + 1];
                                byte decodedByte3 = buffer[bufferIndexer + 2];

                                encodedBCD1 = (byte)((decodedByte1 & 0xFC) >> 2);
                                encodedBCD2 = (byte)(((decodedByte1 & 0x03) << 4) + ((decodedByte2 & 0xF0) >> 4));
                                encodedBCD3 = (byte)(((decodedByte2 & 0x0F) << 2) + ((decodedByte3 & 0xC0) >> 6));
                                encodedBCD4 = (byte)(decodedByte3 & 0x3F);

                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD1]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD2]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD3]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD4]);

                                bufferIndexer += 3;
                            }
                            else
                            if (bufferIndexer + 1 < nbrBytesRead)
                            {
                                byte decodedByte1 = buffer[bufferIndexer + 0];
                                byte decodedByte2 = buffer[bufferIndexer + 1];

                                encodedBCD1 = (byte)((decodedByte1 & 0xFC) >> 2);
                                encodedBCD2 = (byte)(((decodedByte1 & 0x03) << 4) + ((decodedByte2 & 0xF0) >> 4));
                                encodedBCD3 = (byte)((decodedByte2 & 0x0F) << 2);

                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD1]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD2]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD3]);
                                memoryStreamEncodedData.WriteByte((byte)'=');

                                bufferIndexer += 2;
                            }
                            else
                            if (bufferIndexer < nbrBytesRead)
                            {
                                byte decodedByte1 = buffer[bufferIndexer + 0];

                                encodedBCD1 = (byte)((decodedByte1 & 0xFC) >> 2);
                                encodedBCD2 = (byte)((decodedByte1 & 0x03) << 4);

                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD1]);
                                memoryStreamEncodedData.WriteByte(translateToBase64[encodedBCD2]);
                                memoryStreamEncodedData.WriteByte((byte)'=');
                                memoryStreamEncodedData.WriteByte((byte)'=');

                                bufferIndexer += 1;
                            }

                            nbrEncodedBytes += 4;
                        }

                        if (nbrBytesRead != buffer.Length) break;
                    }

                    memoryStreamEncodedData.SetLength(nbrEncodedBytes);
                    memoryStreamDecodedData.Seek(0, SeekOrigin.Begin);
                    memoryStreamEncodedData.Seek(0, SeekOrigin.Begin);
                }

                return memoryStreamEncodedData;
            }
        }

        /// <summary>
        /// Returns the length of the decoded data
        /// </summary>
        public override long Length
        {
            get
            {
                return memoryStreamDecodedData.Length;
            }
        }

        /// <summary>
        /// Returns the length of the Base64 encoded data
        /// </summary>
        public long LengthEncodedData
        {
            get
            {
                return memoryStreamEncodedData.Length;
            }
        }

        /// <summary>
        /// Returns the current position in the DecodedData stream
        /// </summary>
        public override long Position
        {
            get
            {
                return memoryStreamDecodedData.Position;
            }

            set
            {
                positionReadMemoryStreamDecodedData = value;
                memoryStreamDecodedData.Position = value;
            }
        }

        /// <summary>
        /// Basic constructor
        /// </summary>
        public Base64EIDOStream()
        {
        }

        /// <summary>
        /// Constructor which fills up content
        /// </summary>
        /// <param name="base64EncodedData">byte array containing Base64 data</param>
        public Base64EIDOStream(byte[] base64EncodedData)
        {
            Write(base64EncodedData, 0, base64EncodedData.Length);
        }

        /// <summary>
        /// Constructor which fills up content
        /// </summary>
        /// <param name="base64EncodedData">string containing Base64 data</param>
        public Base64EIDOStream(string base64EncodedData)
        {
            Write(ASCIIEncoding.ASCII.GetBytes(base64EncodedData), 0, base64EncodedData.Length);
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Flushes the data in the DecodedData stream
        /// </summary>
        public override void Flush()
        {
            memoryStreamDecodedData.Flush();
        }

        /// <summary>
        /// Reads a sequence of bytes from the DecodedData stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (synchronizationReadWriteLock)
            {
                memoryStreamDecodedData.Seek(positionReadMemoryStreamDecodedData, SeekOrigin.Begin);

                if (offset + count > Length)
                {
                    count = (int)Length - offset;
                }

                positionReadMemoryStreamDecodedData += count;
                return memoryStreamDecodedData.Read(buffer, offset, count);
            }
        }

        /// <summary>
        /// Reads a sequence of characters from the EncodedData stream
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public string ReadEncodedBlock(int offset, int count)
        {
            if (offset == 0 && count == 0)
            {
                count = (int)EncodedData.Length;
            }

            byte[] buffer = new byte[count];

            EncodedData.Seek(offset, SeekOrigin.Begin);
            EncodedData.Read(buffer, 0, count);

            return ASCIIEncoding.ASCII.GetString(buffer);
        }

        /// <summary>
        /// sets the position within the DecodedData stream.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            // always start from beginning
            positionReadMemoryStreamDecodedData = offset;
            memoryStreamDecodedData.Seek(0, SeekOrigin.Begin);
            return offset;
        }

        /// <summary>
        /// Sets the length of data in the DecodedData stream
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value)
        {
            memoryStreamDecodedData.SetLength(value);
        }

        /// <summary>
        /// Writes encoded data into the current stream
        /// </summary>
        /// <param name="base64EncodedData"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] base64EncodedData, int offset, int count)
        {
            lock (synchronizationReadWriteLock)
            {
                int nbrDecodedBytes = 0;
                UInt32 lastThreeDecodedBytes = 0;
                int base64Indexer = 0;
                int nbrPaddingCharacters = 0;

                memoryStreamDecodedData.Seek(positionWriteMemoryStreamDecodedData, SeekOrigin.Begin);

                foreach (char encodedChar in base64EncodedData.Skip(offset).Take(count))
                {
                    base64Indexer++;

                    memoryStreamEncodedData.WriteByte((byte)encodedChar);

                    if (encodedChar == '=')
                    {
                        // base64-padding detected
                        nbrPaddingCharacters++;

                        lastThreeDecodedBytes = (UInt32)(lastThreeDecodedBytes << 6);

                        if ((base64Indexer % 4) == 0)
                        {
                            byte[] decodedBytes = new byte[] {
                                    (byte)((lastThreeDecodedBytes & 0x00FF0000) / 0x10000),
                                    (byte)((lastThreeDecodedBytes & 0x0000FF00) / 0x100)
                                };

                            base64Indexer = 4;

                            if (nbrPaddingCharacters > 3) nbrPaddingCharacters = 3;

                            foreach (byte decodedByte in decodedBytes.Take(3 - nbrPaddingCharacters))
                            {
                                nbrDecodedBytes++;
                                positionWriteMemoryStreamDecodedData++;

                                memoryStreamDecodedData.WriteByte(decodedByte);
                            }
                        }
                    }
                    else
                    {
                        byte partialDecodedByte = translateFromBase64[encodedChar];
                        lastThreeDecodedBytes = (UInt32)(lastThreeDecodedBytes << 6) + (UInt32)(partialDecodedByte & 0x3F);

                        nbrPaddingCharacters = 0;

                        if ((base64Indexer % 4) == 0)
                        {
                            byte[] decodedBytes = new byte[] {
                                    (byte)((lastThreeDecodedBytes & 0x00FF0000) / 0x10000),
                                    (byte)((lastThreeDecodedBytes & 0x0000FF00) / 0x100),
                                    (byte)((lastThreeDecodedBytes & 0x000000FF))
                                };

                            base64Indexer = 4;

                            foreach (byte decodedByte in decodedBytes)
                            {
                                nbrDecodedBytes++;
                                positionWriteMemoryStreamDecodedData++;

                                memoryStreamDecodedData.WriteByte(decodedByte);
                            }
                        }
                    }
                }

                while ((base64Indexer % 4) != 0)
                {
                    base64Indexer++;
                    lastThreeDecodedBytes = (UInt32)(lastThreeDecodedBytes << 6);

                    if ((base64Indexer % 4) == 0)
                    {
                        byte[] decodedBytes = new byte[] {
                                    (byte)((lastThreeDecodedBytes & 0x00FF0000) / 0x10000),
                                    (byte)((lastThreeDecodedBytes & 0x0000FF00) / 0x100)
                            };

                        base64Indexer = 4;

                        foreach (byte decodedByte in decodedBytes)
                        {
                            nbrDecodedBytes++;
                            positionWriteMemoryStreamDecodedData++;

                            memoryStreamDecodedData.WriteByte(decodedByte);
                        }
                    }
                }

                count = nbrDecodedBytes;
            }
        }

        /// <summary>
        /// Writes decoded data into the current stream
        /// </summary>
        /// <param name="decodedBytes"></param>
        public void WriteDecodedData(byte[] decodedBytes)
        {
            lock (synchronizationReadWriteLock)
            {
                memoryStreamDecodedData.Seek(positionWriteMemoryStreamDecodedData, SeekOrigin.Begin);
                memoryStreamDecodedData.Write(decodedBytes, 0, decodedBytes.Length);

                positionWriteMemoryStreamDecodedData += decodedBytes.Length;
            }
        }
    }
}
