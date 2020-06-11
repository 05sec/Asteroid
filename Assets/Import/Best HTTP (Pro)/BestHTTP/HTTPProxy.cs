﻿#if !BESTHTTP_DISABLE_PROXY

using System;
using System.IO;
using System.Text;
using BestHTTP.Authentication;
using BestHTTP.Extensions;

namespace BestHTTP
{
    public abstract class Proxy
    {
        /// <summary>
        /// Address of the proxy server. It has to be in the http://proxyaddress:port form.
        /// </summary>
        public Uri Address { get; set; }

        /// <summary>
        /// Credentials of the proxy
        /// </summary>
        public Credentials Credentials { get; set; }

        internal Proxy(Uri address, Credentials credentials)
        {
            this.Address = address;
            this.Credentials = credentials;
        }

        internal abstract void Connect(Stream stream, HTTPRequest request);
        internal abstract string GetRequestPath(Uri uri);
    }

    public sealed class HTTPProxy : Proxy
    {
        /// <summary>
        /// True if the proxy can act as a transparent proxy
        /// </summary>
        public bool IsTransparent { get; set; }

        /// <summary>
        /// Some non-transparent proxies are except only the path and query of the request uri. Default value is true
        /// </summary>
        public bool SendWholeUri { get; set; }

        /// <summary>
        /// Regardless of the value of IsTransparent, for secure protocols(HTTPS://, WSS://) the plugin will use the proxy as an explicit proxy(will issue a CONNECT request to the proxy)
        /// </summary>
        public bool NonTransparentForHTTPS { get; set; }

        public HTTPProxy(Uri address)
            :this(address, null, false)
        {}

        public HTTPProxy(Uri address, Credentials credentials)
            :this(address, credentials, false)
        {}

        public HTTPProxy(Uri address, Credentials credentials, bool isTransparent)
            :this(address, credentials, isTransparent, true)
        { }

        public HTTPProxy(Uri address, Credentials credentials, bool isTransparent, bool sendWholeUri)
            : this(address, credentials, isTransparent, sendWholeUri, true)
        { }

        public HTTPProxy(Uri address, Credentials credentials, bool isTransparent, bool sendWholeUri, bool nonTransparentForHTTPS)
            :base(address, credentials)
        {
            this.IsTransparent = isTransparent;
            this.SendWholeUri = sendWholeUri;
            this.NonTransparentForHTTPS = nonTransparentForHTTPS;
        }

        internal override string GetRequestPath(Uri uri)
        {
            return this.SendWholeUri ? uri.OriginalString : uri.GetRequestPathAndQueryURL();
        }

        internal override void Connect(Stream stream, HTTPRequest request)
        {
            bool isSecure = HTTPProtocolFactory.IsSecureProtocol(request.CurrentUri);

            if ((!this.IsTransparent || (isSecure && this.NonTransparentForHTTPS)))
            {
                using (var bufferedStream = new WriteOnlyBufferedStream(stream, HTTPRequest.UploadChunkSize))
                using (var outStream = new BinaryWriter(bufferedStream, Encoding.UTF8))
                {
                    bool retry;
                    do
                    {
                        // If we have to because of a authentication request, we will switch it to true
                        retry = false;

                        string connectStr = string.Format("CONNECT {0}:{1} HTTP/1.1", request.CurrentUri.Host, request.CurrentUri.Port.ToString());

                        HTTPManager.Logger.Information("HTTPConnection", "Sending " + connectStr);

                        outStream.SendAsASCII(connectStr);
                        outStream.Write(HTTPRequest.EOL);

                        outStream.SendAsASCII("Proxy-Connection: Keep-Alive");
                        outStream.Write(HTTPRequest.EOL);

                        outStream.SendAsASCII("Connection: Keep-Alive");
                        outStream.Write(HTTPRequest.EOL);

                        outStream.SendAsASCII(string.Format("Host: {0}:{1}", request.CurrentUri.Host, request.CurrentUri.Port.ToString()));
                        outStream.Write(HTTPRequest.EOL);

                        // Proxy Authentication
                        if (this.Credentials != null)
                        {
                            switch (this.Credentials.Type)
                            {
                                case AuthenticationTypes.Basic:
                                    // With Basic authentication we don't want to wait for a challenge, we will send the hash with the first request
                                    outStream.Write(string.Format("Proxy-Authorization: {0}", string.Concat("Basic ", Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Credentials.UserName + ":" + this.Credentials.Password)))).GetASCIIBytes());
                                    outStream.Write(HTTPRequest.EOL);
                                    break;

                                case AuthenticationTypes.Unknown:
                                case AuthenticationTypes.Digest:
                                    var digest = DigestStore.Get(this.Address);
                                    if (digest != null)
                                    {
                                        string authentication = digest.GenerateResponseHeader(request, this.Credentials, true);
                                        if (!string.IsNullOrEmpty(authentication))
                                        {
                                            string auth = string.Format("Proxy-Authorization: {0}", authentication);
                                            if (HTTPManager.Logger.Level <= Logger.Loglevels.Information)
                                                HTTPManager.Logger.Information("HTTPConnection", "Sending proxy authorization header: " + auth);

                                            var bytes = auth.GetASCIIBytes();
                                            outStream.Write(bytes);
                                            outStream.Write(HTTPRequest.EOL);
                                            VariableSizedBufferPool.Release(bytes);
                                        }
                                    }

                                    break;
                            }
                        }

                        outStream.Write(HTTPRequest.EOL);

                        // Make sure to send all the wrote data to the wire
                        outStream.Flush();

                        request.ProxyResponse = new HTTPResponse(request, stream, false, false);

                        // Read back the response of the proxy
                        if (!request.ProxyResponse.Receive(-1, true))
                            throw new Exception("Connection to the Proxy Server failed!");

                        if (HTTPManager.Logger.Level <= Logger.Loglevels.Information)
                            HTTPManager.Logger.Information("HTTPConnection", "Proxy returned - status code: " + request.ProxyResponse.StatusCode + " message: " + request.ProxyResponse.Message + " Body: " + request.ProxyResponse.DataAsText);

                        switch (request.ProxyResponse.StatusCode)
                        {
                            // Proxy authentication required
                            // http://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html#sec10.4.8
                            case 407:
                                {
                                    string authHeader = DigestStore.FindBest(request.ProxyResponse.GetHeaderValues("proxy-authenticate"));
                                    if (!string.IsNullOrEmpty(authHeader))
                                    {
                                        var digest = DigestStore.GetOrCreate(this.Address);
                                        digest.ParseChallange(authHeader);

                                        if (this.Credentials != null && digest.IsUriProtected(this.Address) && (!request.HasHeader("Proxy-Authorization") || digest.Stale))
                                            retry = true;
                                    }

                                    if (!retry)
                                        throw new Exception(string.Format("Can't authenticate Proxy! Status Code: \"{0}\", Message: \"{1}\" and Response: {2}", request.ProxyResponse.StatusCode, request.ProxyResponse.Message, request.ProxyResponse.DataAsText));
                                    break;
                                }

                            default:
                                if (!request.ProxyResponse.IsSuccess)
                                    throw new Exception(string.Format("Proxy returned Status Code: \"{0}\", Message: \"{1}\" and Response: {2}", request.ProxyResponse.StatusCode, request.ProxyResponse.Message, request.ProxyResponse.DataAsText));
                                break;
                        }

                    } while (retry);
                } // using outstream
            }
        }
    }
}

#endif