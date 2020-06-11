﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if !NETFX_CORE || UNITY_EDITOR
    using System.Net.Sockets;
#endif

using UnityEngine;

namespace BestHTTP
{
    #if !BESTHTTP_DISABLE_CACHING
        using BestHTTP.Caching;
    #endif

    using BestHTTP.Extensions;

    #if !BESTHTTP_DISABLE_COOKIES
        using BestHTTP.Cookies;
    #endif

    public interface IProtocol
    {
        bool IsClosed { get; }
        void HandleEvents();
    }

    /// <summary>
    ///
    /// </summary>
    public class HTTPResponse : IDisposable
    {
        internal const byte CR = 13;
        internal const byte LF = 10;

        public const int MinBufferSize = 4 * 1024;

        #region Public Properties

        public int VersionMajor { get; protected set; }
        public int VersionMinor { get; protected set; }

        /// <summary>
        /// The status code that sent from the server.
        /// </summary>
        public int StatusCode { get; protected set; }

        /// <summary>
        /// Returns true if the status code is in the range of [200..300[ or 304 (Not Modified)
        /// </summary>
        public bool IsSuccess { get { return (this.StatusCode >= 200 && this.StatusCode < 300) || this.StatusCode == 304; } }

        /// <summary>
        /// The message that sent along with the StatusCode from the server. You can check it for errors from the server.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// True if it's a streamed response.
        /// </summary>
        public bool IsStreamed { get; protected set; }

        /// <summary>
        /// True if the streaming is finished, and no more fragments are coming.
        /// </summary>
        public bool IsStreamingFinished { get; internal set; }

#if !BESTHTTP_DISABLE_CACHING
        /// <summary>
        /// Indicates that the response body is read from the cache.
        /// </summary>
        public bool IsFromCache { get; internal set; }

        /// <summary>
        /// Provides information about the file used for caching the request.
        /// </summary>
        public HTTPCacheFileInfo CacheFileInfo { get; internal set; }

        /// <summary>
        /// Determines if this response is only stored to cache.
        /// If both IsCacheOnly and IsStreamed are true, GetStreamedFragments should not be called.
        /// </summary>
        public bool IsCacheOnly { get; private set; }
#endif

        /// <summary>
        /// The headers that sent from the server.
        /// </summary>
        public Dictionary<string, List<string>> Headers { get; protected set; }

        /// <summary>
        /// The data that downloaded from the server. All Transfer and Content encodings decoded if any(eg. chunked, gzip, deflate).
        /// </summary>
        public byte[] Data { get; internal set; }

        /// <summary>
        /// The normal HTTP protocol is upgraded to an other.
        /// </summary>
        public bool IsUpgraded { get; protected set; }

#if !BESTHTTP_DISABLE_COOKIES
        /// <summary>
        /// The cookies that the server sent to the client.
        /// </summary>
        public List<Cookie> Cookies { get; internal set; }
#endif

        /// <summary>
        /// Cached, converted data.
        /// </summary>
        protected string dataAsText;

        /// <summary>
        /// The data converted to an UTF8 string.
        /// </summary>
        public string DataAsText
        {
            get
            {
                if (Data == null)
                    return string.Empty;

                if (!string.IsNullOrEmpty(dataAsText))
                    return dataAsText;

                return dataAsText = Encoding.UTF8.GetString(Data, 0, Data.Length);
            }
        }

        /// <summary>
        /// Cached converted data.
        /// </summary>
        protected Texture2D texture;

        /// <summary>
        /// The data loaded to a Texture2D.
        /// </summary>
        public Texture2D DataAsTexture2D
        {
            get
            {
                if (Data == null)
                    return null;

                if (texture != null)
                    return texture;

                texture = new Texture2D(0, 0, TextureFormat.ARGB32, false);
                texture.LoadImage(Data);

                return texture;
            }
        }

        /// <summary>
        /// True if the connection's stream will be closed manually. Used in custom protocols (WebSocket, EventSource).
        /// </summary>
        public bool IsClosedManually { get; protected set; }

        #endregion

        #region Internal Fields

        internal HTTPRequest baseRequest;

        #endregion

        #region Protected Properties And Fields

        protected Stream Stream;

        protected List<byte[]> streamedFragments;
        protected object SyncRoot = new object();

        protected byte[] fragmentBuffer;
        protected int fragmentBufferDataLength;
#if !BESTHTTP_DISABLE_CACHING
        protected Stream cacheStream;
#endif
        protected int allFragmentSize;

        #endregion

        public HTTPResponse(HTTPRequest request, Stream stream, bool isStreamed, bool isFromCache)
        {
            this.baseRequest = request;
            this.Stream = /*new ReadOnlyBufferedStream*/(stream);
            this.IsStreamed = isStreamed;
#if !BESTHTTP_DISABLE_CACHING
            this.IsFromCache = isFromCache;
            this.IsCacheOnly = request.CacheOnly;
#endif
            this.IsClosedManually = false;
        }

        public virtual bool Receive(int forceReadRawContentLength = -1, bool readPayloadData = true)
        {
            string statusLine = string.Empty;

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("Receive. forceReadRawContentLength: '{0:N0}', readPayloadData: '{1:N0}'", forceReadRawContentLength, readPayloadData));

            // On WP platform we aren't able to determined sure enough whether the tcp connection is closed or not.
            //  So if we get an exception here, we need to recreate the connection.
            try
            {
                // Read out 'HTTP/1.1' from the "HTTP/1.1 {StatusCode} {Message}"
                statusLine = ReadTo(Stream, (byte)' ');
            }
            catch
            {
                if (!baseRequest.DisableRetry)
                {
                    HTTPManager.Logger.Warning("HTTPResponse", string.Format("{0} - Failed to read Status Line! Retry is enabled, returning with false.", this.baseRequest.CurrentUri.ToString()));
                    return false;
                }

                HTTPManager.Logger.Warning("HTTPResponse", string.Format("{0} - Failed to read Status Line! Retry is disabled, re-throwing exception.", this.baseRequest.CurrentUri.ToString()));
                throw;
            }

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("Status Line: '{0}'", statusLine));

            if (string.IsNullOrEmpty(statusLine))
            {
                if (!baseRequest.DisableRetry)
                    return false;

                throw new Exception("Remote server closed the connection before sending response header!");
            }

            string[] versions = statusLine.Split(new char[] { '/', '.' });
            this.VersionMajor = int.Parse(versions[1]);
            this.VersionMinor = int.Parse(versions[2]);

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("HTTP Version: '{0}.{1}'", this.VersionMajor.ToString(), this.VersionMinor.ToString()));

            int statusCode;
            string statusCodeStr = NoTrimReadTo(Stream, (byte)' ', LF);

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("Status Code: '{0}'", statusCodeStr));

            if (baseRequest.DisableRetry)
                statusCode = int.Parse(statusCodeStr);
            else if (!int.TryParse(statusCodeStr, out statusCode))
                return false;

            this.StatusCode = statusCode;

            if (statusCodeStr.Length > 0 && (byte)statusCodeStr[statusCodeStr.Length - 1] != LF && (byte)statusCodeStr[statusCodeStr.Length - 1] != CR)
            {
                this.Message = ReadTo(Stream, LF);
                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("Status Message: '{0}'", this.Message));
            }
            else
            {
                HTTPManager.Logger.Warning("HTTPResponse", string.Format("{0} - Skipping Status Message reading!", this.baseRequest.CurrentUri.ToString()));

                this.Message = string.Empty;
            }

            //Read Headers
            ReadHeaders(Stream);

            IsUpgraded = StatusCode == 101 && (HasHeaderWithValue("connection", "upgrade") || HasHeader("upgrade"));

            if (IsUpgraded && HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging("Request Upgraded!");

            if (!readPayloadData)
                return true;

            return ReadPayload(forceReadRawContentLength);
        }

        protected bool ReadPayload(int forceReadRawContentLength)
        {
            // Reading from an already unpacked stream (eq. From a file cache)
            if (forceReadRawContentLength != -1)
            {
#if !BESTHTTP_DISABLE_CACHING
                this.IsFromCache = true;
#endif
                ReadRaw(Stream, forceReadRawContentLength);

                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging("ReadPayload Finished!");
                return true;
            }

            //  http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.4
            //  1.Any response message which "MUST NOT" include a message-body (such as the 1xx, 204, and 304 responses and any response to a HEAD request)
            //      is always terminated by the first empty line after the header fields, regardless of the entity-header fields present in the message.
            if ((StatusCode >= 100 && StatusCode < 200) || StatusCode == 204 || StatusCode == 304 || baseRequest.MethodType == HTTPMethods.Head)
                return true;

#if (!UNITY_WEBGL || UNITY_EDITOR)
            if (HasHeaderWithValue("transfer-encoding", "chunked"))
                ReadChunked(Stream);
            else
#endif
            {
                //  http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.4
                //      Case 3 in the above link.
                List<string> contentLengthHeaders = GetHeaderValues("content-length");
                var contentRangeHeaders = GetHeaderValues("content-range");
                if (contentLengthHeaders != null && contentRangeHeaders == null)
                    ReadRaw(Stream, long.Parse(contentLengthHeaders[0]));
                else if (contentRangeHeaders != null)
                {
                    if (contentLengthHeaders != null)
                        ReadRaw(Stream, long.Parse(contentLengthHeaders[0]));
                    else
                    {
                        HTTPRange range = GetRange();
                        ReadRaw(Stream, (range.LastBytePos - range.FirstBytePos) + 1);
                    }
                }
                else
                    ReadUnknownSize(Stream);
            }

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging("ReadPayload Finished!");

            return true;
        }

        #region Header Management

        protected void ReadHeaders(Stream stream)
        {
            string headerName = ReadTo(stream, (byte)':', LF)/*.Trim()*/;
            while (headerName != string.Empty)
            {
                string value = ReadTo(stream, LF);

                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("Header - '{0}': '{1}'", headerName, value));

                AddHeader(headerName, value);

                headerName = ReadTo(stream, (byte)':', LF);
            }
        }

        protected void AddHeader(string name, string value)
        {
            name = name.ToLower();

            if (Headers == null)
                Headers = new Dictionary<string, List<string>>();

            List<string> values;
            if (!Headers.TryGetValue(name, out values))
                Headers.Add(name, values = new List<string>(1));

            values.Add(value);
        }

        /// <summary>
        /// Returns the list of values that received from the server for the given header name.
        /// <remarks>Remarks: All headers converted to lowercase while reading the response.</remarks>
        /// </summary>
        /// <param name="name">Name of the header</param>
        /// <returns>If no header found with the given name or there are no values in the list (eg. Count == 0) returns null.</returns>
        public List<string> GetHeaderValues(string name)
        {
            if (Headers == null)
                return null;

            name = name.ToLower();

            List<string> values;
            if (!Headers.TryGetValue(name, out values) || values.Count == 0)
                return null;

            return values;
        }

        /// <summary>
        /// Returns the first value in the header list or null if there are no header or value.
        /// </summary>
        /// <param name="name">Name of the header</param>
        /// <returns>If no header found with the given name or there are no values in the list (eg. Count == 0) returns null.</returns>
        public string GetFirstHeaderValue(string name)
        {
            if (Headers == null)
                return null;

            name = name.ToLower();

            List<string> values;
            if (!Headers.TryGetValue(name, out values) || values.Count == 0)
                return null;

            return values[0];
        }

        /// <summary>
        /// Checks if there is a header with the given name and value.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <param name="value"></param>
        /// <returns>Returns true if there is a header with the given name and value.</returns>
        public bool HasHeaderWithValue(string headerName, string value)
        {
            var values = GetHeaderValues(headerName);
            if (values == null)
                return false;

            for (int i = 0; i < values.Count; ++i)
                if (string.Compare(values[i], value, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        /// <summary>
        /// Checks if there is a header with the given name.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <returns>Returns true if there is a header with the given name.</returns>
        public bool HasHeader(string headerName)
        {
            var values = GetHeaderValues(headerName);
            if (values == null)
                return false;

            return true;
        }

        /// <summary>
        /// Parses the 'Content-Range' header's value and returns a HTTPRange object.
        /// </summary>
        /// <remarks>If the server ignores a byte-range-spec because it is syntactically invalid, the server SHOULD treat the request as if the invalid Range header field did not exist.
        /// (Normally, this means return a 200 response containing the full entity). In this case because of there are no 'Content-Range' header, this function will return null!</remarks>
        /// <returns>Returns null if no 'Content-Range' header found.</returns>
        public HTTPRange GetRange()
        {
            var rangeHeaders = GetHeaderValues("content-range");
            if (rangeHeaders == null)
                return null;

            // A byte-content-range-spec with a byte-range-resp-spec whose last- byte-pos value is less than its first-byte-pos value,
            //  or whose instance-length value is less than or equal to its last-byte-pos value, is invalid.
            // The recipient of an invalid byte-content-range- spec MUST ignore it and any content transferred along with it.

            // A valid content-range sample: "bytes 500-1233/1234"
            var ranges = rangeHeaders[0].Split(new char[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);

            // A server sending a response with status code 416 (Requested range not satisfiable) SHOULD include a Content-Range field with a byte-range-resp-spec of "*".
            // The instance-length specifies the current length of the selected resource.
            // "bytes */1234"
            if (ranges[1] == "*")
                return new HTTPRange(int.Parse(ranges[2]));

            return new HTTPRange(int.Parse(ranges[1]), int.Parse(ranges[2]), ranges[3] != "*" ? int.Parse(ranges[3]) : -1);
        }

        #endregion

        #region Static Stream Management Helper Functions

        public static string ReadTo(Stream stream, byte blocker)
        {
            byte[] readBuf = VariableSizedBufferPool.Get(1024, true);
            try
            {
                int bufpos = 0;

                int ch = stream.ReadByte();
                while (ch != blocker && ch != -1)
                {
                    if (ch > 0x7f) //replaces asciitostring
                        ch = '?';

                    //make buffer larger if too short
                    if (readBuf.Length <= bufpos)
                        VariableSizedBufferPool.Resize(ref readBuf, readBuf.Length * 2, true);

                    if (bufpos > 0 || !char.IsWhiteSpace((char)ch)) //trimstart
                        readBuf[bufpos++] = (byte)ch;
                    ch = stream.ReadByte();
                }

                while (bufpos > 0 && char.IsWhiteSpace((char)readBuf[bufpos - 1]))
                    bufpos--;

                return System.Text.Encoding.UTF8.GetString(readBuf, 0, bufpos);
            }
            finally
            {
                VariableSizedBufferPool.Release(readBuf);
            }
        }

        public static string ReadTo(Stream stream, byte blocker1, byte blocker2)
        {
            byte[] readBuf = VariableSizedBufferPool.Get(1024, true);
            try {
                int bufpos = 0;

                int ch = stream.ReadByte();
                while (ch != blocker1 && ch != blocker2 && ch != -1)
                {
                    if (ch > 0x7f) //replaces asciitostring
                        ch = '?';

                    //make buffer larger if too short
                    if (readBuf.Length <= bufpos)
                        VariableSizedBufferPool.Resize(ref readBuf, readBuf.Length * 2, true);

                    if (bufpos > 0 || !char.IsWhiteSpace((char)ch)) //trimstart
                        readBuf[bufpos++] = (byte)ch;
                    ch = stream.ReadByte();
                }

                while (bufpos > 0 && char.IsWhiteSpace((char)readBuf[bufpos - 1]))
                    bufpos--;

                return System.Text.Encoding.UTF8.GetString(readBuf, 0, bufpos);
            }
            finally
            {
                VariableSizedBufferPool.Release(readBuf);
            }
        }

        public static string NoTrimReadTo(Stream stream, byte blocker1, byte blocker2)
        {
            byte[] readBuf = VariableSizedBufferPool.Get(1024, true);
            try {
                int bufpos = 0;

                int ch = stream.ReadByte();
                while (ch != blocker1 && ch != blocker2 && ch != -1)
                {
                    if (ch > 0x7f) //replaces asciitostring
                        ch = '?';

                    //make buffer larger if too short
                    if (readBuf.Length <= bufpos)
                        VariableSizedBufferPool.Resize(ref readBuf, readBuf.Length * 2, true);

                    if (bufpos > 0 || !char.IsWhiteSpace((char)ch)) //trimstart
                        readBuf[bufpos++] = (byte)ch;
                    ch = stream.ReadByte();
                }

                return System.Text.Encoding.UTF8.GetString(readBuf, 0, bufpos);
            }
            finally
            {
                VariableSizedBufferPool.Release(readBuf);
            }
        }

        #endregion

        #region Read Chunked Body

        protected int ReadChunkLength(Stream stream)
        {
            // Read until the end of line, then split the string so we will discard any optional chunk extensions
            string line = ReadTo(stream, LF);
            string[] splits = line.Split(';');
            string num = splits[0];

            int result;
            if (int.TryParse(num, System.Globalization.NumberStyles.AllowHexSpecifier, null, out result))
                return result;

            throw new Exception(string.Format("Can't parse '{0}' as a hex number!", num));
        }

        // http://www.w3.org/Protocols/rfc2616/rfc2616-sec3.html#sec3.6.1
        protected void ReadChunked(Stream stream)
        {
            BeginReceiveStreamFragments();

            string contentLengthHeader = GetFirstHeaderValue("Content-Length");
            bool hasContentLengthHeader = !string.IsNullOrEmpty(contentLengthHeader);
            int realLength = 0;
            if (hasContentLengthHeader)
                hasContentLengthHeader = int.TryParse(contentLengthHeader, out realLength);

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("ReadChunked - hasContentLengthHeader: {0}, contentLengthHeader: {1} realLength: {2:N0}", hasContentLengthHeader.ToString(), contentLengthHeader, realLength));

            using (var output = new BufferPoolMemoryStream())
            {
                int chunkLength = ReadChunkLength(stream);

                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("chunkLength: {0:N0}", chunkLength));

                byte[] buffer = VariableSizedBufferPool.Get(Mathf.NextPowerOfTwo(chunkLength), true);

                int contentLength = 0;

                // Progress report:
                baseRequest.DownloadLength = hasContentLengthHeader ? realLength : chunkLength;
                baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                    || this.IsFromCache
#endif
                    ;

                string encoding =
#if !BESTHTTP_DISABLE_CACHING
                IsFromCache ? null :
#endif
                GetFirstHeaderValue("content-encoding");
                bool gzipped = !string.IsNullOrEmpty(encoding) && encoding == "gzip";

                while (chunkLength != 0)
                {
                    // To avoid more GC garbage we use only one buffer, and resize only if the next chunk doesn't fit.
                    if (buffer.Length < chunkLength)
                        VariableSizedBufferPool.Resize(ref buffer, chunkLength, true);

                    int readBytes = 0;

                    // Fill up the buffer
                    do
                    {
                        int bytes = stream.Read(buffer, readBytes, chunkLength - readBytes);
                        if (bytes <= 0)
                            throw ExceptionHelper.ServerClosedTCPStream();

                        readBytes += bytes;

                        // Progress report:
                        // Placing reporting inside this cycle will report progress much more frequent
                        baseRequest.Downloaded += bytes;
                        baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                        || this.IsFromCache
#endif
                        ;

                    } while (readBytes < chunkLength);

                    if (baseRequest.UseStreaming)
                    {
                        if (gzipped)
                        {
                            var decompressed = Decompress(buffer, 0, readBytes);
                            if (decompressed != null)
                                FeedStreamFragment(decompressed, 0, decompressed.Length);
                        }
                        else
                            FeedStreamFragment(buffer, 0, readBytes);
                    }
                    else
                        output.Write(buffer, 0, readBytes);

                    // Every chunk data has a trailing CRLF
                    ReadTo(stream, LF);

                    contentLength += readBytes;

                    // read the next chunk's length
                    chunkLength = ReadChunkLength(stream);

                    if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                        VerboseLogging(string.Format("chunkLength: {0:N0}", chunkLength));

                    if (!hasContentLengthHeader)
                        baseRequest.DownloadLength += chunkLength;
                    baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                        || this.IsFromCache
#endif
                        ;
                }

                VariableSizedBufferPool.Release(buffer);

                if (baseRequest.UseStreaming)
                {
                    if (gzipped)
                    {
                        var decompressed = Decompress(null, 0, 0, true);
                        if (decompressed != null)
                            FeedStreamFragment(decompressed, 0, decompressed.Length);
                    }

                    FlushRemainingFragmentBuffer();
                }

                // Read the trailing headers or the CRLF
                ReadHeaders(stream);

                // HTTP servers sometimes use compression (gzip) or deflate methods to optimize transmission.
                // How both chunked and gzip encoding interact is dictated by the two-staged encoding of HTTP:
                //  first the content stream is encoded as (Content-Encoding: gzip), after which the resulting byte stream is encoded for transfer using another encoder (Transfer-Encoding: chunked).
                //  This means that in case both compression and chunked encoding are enabled, the chunk encoding itself is not compressed, and the data in each chunk should not be compressed individually.
                //  The remote endpoint can decode the incoming stream by first decoding it with the Transfer-Encoding, followed by the specified Content-Encoding.
                // It would be a better implementation when the chunk would be decododed on-the-fly. Becouse now the whole stream must be downloaded, and then decoded. It needs more memory.
                if (!baseRequest.UseStreaming)
                    this.Data = DecodeStream(output);
            }

            CloseDecompressors();
        }

        #endregion

        #region Read Raw Body

        // No transfer-encoding just raw bytes.
        internal void ReadRaw(Stream stream, long contentLength)
        {
            BeginReceiveStreamFragments();

            // Progress report:
            baseRequest.DownloadLength = contentLength;
            baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                || this.IsFromCache
#endif
                ;

            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging(string.Format("ReadRaw - contentLength: {0:N0}", contentLength));

            string encoding =
#if !BESTHTTP_DISABLE_CACHING
                IsFromCache ? null :
#endif
                GetFirstHeaderValue("content-encoding");
            bool gzipped = !string.IsNullOrEmpty(encoding) && encoding == "gzip";
            if(!baseRequest.UseStreaming && contentLength > 2147483646)
            {
                throw new OverflowException("You have to use STREAMING to download files bigger than 2GB!");
            }

            using (var output = new BufferPoolMemoryStream(baseRequest.UseStreaming ? 0 : (int)contentLength))
            {
                byte[] buffer = VariableSizedBufferPool.Get(Math.Max(baseRequest.StreamFragmentSize, MinBufferSize), true);
                int readBytes = 0;

                while (contentLength > 0)
                {
                    readBytes = 0;

                    do
                    {
                        int readbuffer = (int)Math.Min(2147483646, (uint)contentLength);
                        int bytes = stream.Read(buffer, readBytes, Math.Min(readbuffer, buffer.Length - readBytes));

                        if (bytes <= 0)
                            throw ExceptionHelper.ServerClosedTCPStream();

                        readBytes += bytes;
                        contentLength -= bytes;

                        // Progress report:
                        baseRequest.Downloaded += bytes;
                        baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                            || this.IsFromCache
#endif
                            ;

                    } while (readBytes < buffer.Length && contentLength > 0);

                    if (baseRequest.UseStreaming)
                    {
                        if (gzipped)
                        {
                            var decompressed = Decompress(buffer, 0, readBytes);
                            if (decompressed != null)
                                FeedStreamFragment(decompressed, 0, decompressed.Length);
                        }
                        else
                            FeedStreamFragment(buffer, 0, readBytes);
                    }
                    else
                        output.Write(buffer, 0, readBytes);
                };

                VariableSizedBufferPool.Release(buffer);

                if (baseRequest.UseStreaming)
                {
                    if (gzipped)
                    {
                        var decompressed = Decompress(null, 0, 0, true);
                        if (decompressed != null)
                            FeedStreamFragment(decompressed, 0, decompressed.Length);
                    }

                    FlushRemainingFragmentBuffer();
                }

                if (!baseRequest.UseStreaming)
                    this.Data = DecodeStream(output);
            }

            CloseDecompressors();
        }

        #endregion

        #region Read Unknown Size

        protected void ReadUnknownSize(Stream stream)
        {
            string encoding =
#if !BESTHTTP_DISABLE_CACHING
                IsFromCache ? null :
#endif
                GetFirstHeaderValue("content-encoding");
            bool gzipped = !string.IsNullOrEmpty(encoding) && encoding == "gzip";

            using (var output = new BufferPoolMemoryStream())
            {
                byte[] buffer = VariableSizedBufferPool.Get(Math.Max(baseRequest.StreamFragmentSize, MinBufferSize), false);

                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("ReadUnknownSize - buffer size: {0:N0}", buffer.Length));

                int readBytes = 0;
                int bytes = 0;
                do
                {
                    readBytes = 0;

                    do
                    {
                        bytes = 0;

#if !NETFX_CORE || UNITY_EDITOR
                        NetworkStream networkStream = stream as NetworkStream;
                        // If we have the good-old NetworkStream, than we can use the DataAvailable property. On WP8 platforms, these are omitted... :/
                        if (networkStream != null && baseRequest.EnableSafeReadOnUnknownContentLength)
                        {
                            for (int i = readBytes; i < buffer.Length && networkStream.DataAvailable; ++i)
                            {
                                int read = stream.ReadByte();
                                if (read >= 0)
                                {
                                    buffer[i] = (byte)read;
                                    bytes++;
                                }
                                else
                                    break;
                            }
                        }
                        else // This will be good anyway, but a little slower.
#endif
                        {
                            bytes = stream.Read(buffer, readBytes, buffer.Length - readBytes);
                        }

                        readBytes += bytes;

                        // Progress report:
                        baseRequest.Downloaded += bytes;
                        baseRequest.DownloadLength = baseRequest.Downloaded;
                        baseRequest.DownloadProgressChanged = this.IsSuccess
#if !BESTHTTP_DISABLE_CACHING
                            || this.IsFromCache
#endif
                            ;

                    } while (readBytes < buffer.Length && bytes > 0);

                    if (baseRequest.UseStreaming)
                    {
                        if (gzipped)
                        {
                            var decompressed = Decompress(buffer, 0, readBytes);
                            if (decompressed != null)
                                FeedStreamFragment(decompressed, 0, decompressed.Length);
                        }
                        else
                            FeedStreamFragment(buffer, 0, readBytes);
                    }
                    else
                        output.Write(buffer, 0, readBytes);

                } while (bytes > 0);

                VariableSizedBufferPool.Release(buffer);

                if (baseRequest.UseStreaming)
                {
                    if (gzipped)
                    {
                        var decompressed = Decompress(null, 0, 0, true);
                        if (decompressed != null)
                            FeedStreamFragment(decompressed, 0, decompressed.Length);
                    }

                    FlushRemainingFragmentBuffer();
                }

                if (!baseRequest.UseStreaming)
                    this.Data = DecodeStream(output);
            }

            CloseDecompressors();
        }

        #endregion

        #region Stream Decoding

        protected byte[] DecodeStream(BufferPoolMemoryStream streamToDecode)
        {
            streamToDecode.Seek(0, SeekOrigin.Begin);

            // The cache stores the decoded data
            var encoding =
#if !BESTHTTP_DISABLE_CACHING
                IsFromCache ? null :
#endif
                GetHeaderValues("content-encoding");

#if !UNITY_WEBGL || UNITY_EDITOR
            Stream decoderStream = null;
#endif

            // Return early if there are no encoding used.
            if (encoding == null)
                return streamToDecode.ToArray();
            else
            {
                switch (encoding[0])
                {
#if !UNITY_WEBGL || UNITY_EDITOR
                    case "gzip": decoderStream = new Decompression.Zlib.GZipStream(streamToDecode, Decompression.Zlib.CompressionMode.Decompress); break;
                    case "deflate": decoderStream = new Decompression.Zlib.DeflateStream(streamToDecode, Decompression.Zlib.CompressionMode.Decompress); break;
#endif
                    //identity, utf-8, etc.
                    default:
                        // Do not copy from one stream to an other, just return with the raw bytes
                        return streamToDecode.ToArray();
                }
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            using (var ms = new BufferPoolMemoryStream((int)streamToDecode.Length))
            {
                var buf = VariableSizedBufferPool.Get(1024, true);
                int byteCount = 0;

                while ((byteCount = decoderStream.Read(buf, 0, buf.Length)) > 0)
                    ms.Write(buf, 0, byteCount);

                VariableSizedBufferPool.Release(buf);

                decoderStream.Dispose();
                return ms.ToArray();
            }
#endif
        }

        #endregion

        #region Streaming Fragments Support

        private BufferPoolMemoryStream decompressorInputStream;
        private BufferPoolMemoryStream decompressorOutputStream;
        private Decompression.Zlib.GZipStream decompressorGZipStream;

        const int MinLengthToDecompress = 256;

        private void CloseDecompressors()
        {
            if (decompressorGZipStream != null)
                decompressorGZipStream.Dispose();
            decompressorGZipStream = null;

            if (decompressorInputStream != null)
                decompressorInputStream.Dispose();
            decompressorInputStream = null;

            if (decompressorOutputStream != null)
                decompressorOutputStream.Dispose();
            decompressorOutputStream = null;
        }

        private byte[] Decompress(byte[] data, int offset, int count, bool forceDecompress = false)
        {
            if (decompressorInputStream == null)
                decompressorInputStream = new BufferPoolMemoryStream(count);

            if (data != null)
                decompressorInputStream.Write(data, offset, count);

            if (!forceDecompress && decompressorInputStream.Length < MinLengthToDecompress)
                return null;
            
            decompressorInputStream.Position = 0;

            if (decompressorGZipStream == null)
            {
                decompressorGZipStream = new Decompression.Zlib.GZipStream(decompressorInputStream,
                                                                              Decompression.Zlib.CompressionMode.Decompress,
                                                                              Decompression.Zlib.CompressionLevel.Default,
                                                                              true);
                decompressorGZipStream.FlushMode = Decompression.Zlib.FlushType.Sync;
            }

            if (decompressorOutputStream == null)
                decompressorOutputStream = new BufferPoolMemoryStream();
            decompressorOutputStream.SetLength(0);

            byte[] copyBuffer = VariableSizedBufferPool.Get(1024, true);

            int readCount;
            while ((readCount = decompressorGZipStream.Read(copyBuffer, 0, copyBuffer.Length)) != 0)
                decompressorOutputStream.Write(copyBuffer, 0, readCount);

            VariableSizedBufferPool.Release(copyBuffer);

            decompressorGZipStream.SetLength(0);

            // TODO: be able to use a larger array
            byte[] result = decompressorOutputStream.ToArray();
            
            return result;
        }

        protected void BeginReceiveStreamFragments()
        {
#if !BESTHTTP_DISABLE_CACHING
            if (!baseRequest.DisableCache && baseRequest.UseStreaming)
            {
                // If caching is enabled and the response not from cache and it's cacheble we will cache the downloaded data.
                if (!IsFromCache && HTTPCacheService.IsCacheble(baseRequest.CurrentUri, baseRequest.MethodType, this))
                    cacheStream = HTTPCacheService.PrepareStreamed(baseRequest.CurrentUri, this);
            }
#endif
            allFragmentSize = 0;
        }

        /// <summary>
        /// Add data to the fragments list.
        /// </summary>
        /// <param name="buffer">The buffer to be added.</param>
        /// <param name="pos">The position where we start copy the data.</param>
        /// <param name="length">How many data we want to copy.</param>
        protected void FeedStreamFragment(byte[] buffer, int pos, int length)
        {
            if (buffer == null || length == 0)
                return;

            // If reading from cache, we don't want to read too much data to memory. So we will wait until the loaded fragment processed.
            WaitWhileFragmentQueueIsFull();

            if (fragmentBuffer == null)
            {
                fragmentBuffer = VariableSizedBufferPool.Get(baseRequest.StreamFragmentSize, false);
                fragmentBufferDataLength = 0;
            }

            if (fragmentBufferDataLength + length <= baseRequest.StreamFragmentSize)
            {
                Array.Copy(buffer, pos, fragmentBuffer, fragmentBufferDataLength, length);
                fragmentBufferDataLength += length;

                if (fragmentBufferDataLength == baseRequest.StreamFragmentSize)
                {
                    AddStreamedFragment(fragmentBuffer);
                    fragmentBuffer = null;
                    fragmentBufferDataLength = 0;
                }
            }
            else
            {
                int remaining = baseRequest.StreamFragmentSize - fragmentBufferDataLength;

                FeedStreamFragment(buffer, pos, remaining);
                FeedStreamFragment(buffer, pos + remaining, length - remaining);
            }
        }

        protected void FlushRemainingFragmentBuffer()
        {
            if (fragmentBuffer != null)
            {
                VariableSizedBufferPool.Resize(ref fragmentBuffer, fragmentBufferDataLength, false);

                AddStreamedFragment(fragmentBuffer);
                fragmentBuffer = null;
                fragmentBufferDataLength = 0;
            }

#if !BESTHTTP_DISABLE_CACHING
            if (cacheStream != null)
            {
                cacheStream.Dispose();
                cacheStream = null;

                HTTPCacheService.SetBodyLength(baseRequest.CurrentUri, allFragmentSize);
            }
#endif
            var tmp = fragmentWaitEvent;
            fragmentWaitEvent = null;
            if (tmp != null)
                (tmp as IDisposable).Dispose();
        }

        protected void AddStreamedFragment(byte[] buffer)
        {
            lock (SyncRoot)
            {
#if !BESTHTTP_DISABLE_CACHING
                if (!IsCacheOnly)
#endif
                {
                    if (streamedFragments == null)
                        streamedFragments = new List<byte[]>();

                    streamedFragments.Add(buffer);
                }

                if (HTTPManager.Logger.Level == Logger.Loglevels.All && buffer != null && streamedFragments != null)
                    VerboseLogging(string.Format("AddStreamedFragment buffer length: {0:N0} streamedFragments: {1:N0}", buffer.Length, streamedFragments.Count));

#if !BESTHTTP_DISABLE_CACHING
                if (cacheStream != null)
                {
                    cacheStream.Write(buffer, 0, buffer.Length);
                    allFragmentSize += buffer.Length;
                }
#endif
            }
        }

        private volatile System.Threading.AutoResetEvent fragmentWaitEvent;
#pragma warning disable 1998
        protected
#if NETFX_CORE
            async
#endif
            void WaitWhileFragmentQueueIsFull()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            while (baseRequest.UseStreaming && FragmentQueueIsFull())
            {
                VerboseLogging("WaitWhileFragmentQueueIsFull");
                
                if (fragmentWaitEvent == null)
                    fragmentWaitEvent = new System.Threading.AutoResetEvent(false);
                fragmentWaitEvent.WaitOne(16);
            }
#endif
        }

#pragma warning restore 1998

        bool FragmentQueueIsFull()
        {
            lock (SyncRoot)
            {
                bool result = streamedFragments != null && streamedFragments.Count >= baseRequest.MaxFragmentQueueLength;
                if (result && HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("HasFragmentsInQueue - {0} / {1}", streamedFragments.Count, baseRequest.MaxFragmentQueueLength));

                return result;
            }
        }

        /// <summary>
        /// If streaming is used, then every time this callback function called we can use this function to
        ///  retrieve the downloaded and buffered data. The returned list can be null, if there is no data yet.
        /// </summary>
        /// <returns></returns>
        public List<byte[]> GetStreamedFragments()
        {
            lock (SyncRoot)
            {
                if (streamedFragments == null || streamedFragments.Count == 0)
                {
                    if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                        VerboseLogging("GetStreamedFragments - no fragments, returning with null");
                    return null;
                }

                var result = new List<byte[]>(streamedFragments);
                streamedFragments.Clear();

                if (fragmentWaitEvent != null)
                    fragmentWaitEvent.Set();

                if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                    VerboseLogging(string.Format("GetStreamedFragments - returning with {0:N0} fragments", result.Count.ToString()));

                return result;
            }
        }

        internal bool HasStreamedFragments()
        {
            lock (SyncRoot)
                return streamedFragments != null && streamedFragments.Count >= baseRequest.MaxFragmentQueueLength;
        }

        internal void FinishStreaming()
        {
            if (HTTPManager.Logger.Level == Logger.Loglevels.All)
                VerboseLogging("FinishStreaming");

            IsStreamingFinished = true;
            Dispose();
        }

        #endregion

        void VerboseLogging(string str)
        {
          if (HTTPManager.Logger.Level == Logger.Loglevels.All)
            HTTPManager.Logger.Verbose("HTTPResponse", "'" + this.baseRequest.CurrentUri.ToString() + "' - " + str);
        }

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public void Dispose()
        {
            // Release resources in case we are using ReadOnlyBufferedStream, it will not close its inner stream.
            // Otherwise, closing the (inner) Stream is the connection's responsibility
            if (Stream != null && Stream is ReadOnlyBufferedStream)
                (Stream as IDisposable).Dispose();
            Stream = null;

#if !BESTHTTP_DISABLE_CACHING
            if (cacheStream != null)
            {
                cacheStream.Dispose();
                cacheStream = null;
            }
#endif
            GC.SuppressFinalize(this);
        }
    }
}