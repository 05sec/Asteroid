#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;

using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1
{
	public class BerOctetStringParser
		: Asn1OctetStringParser
	{
		private readonly Asn1StreamParser _parser;

		internal BerOctetStringParser(
			Asn1StreamParser parser)
		{
			_parser = parser;
		}

		public Stream GetOctetStream()
		{
			return new ConstructedOctetStream(_parser);
		}

		public Asn1Object ToAsn1Object()
		{
			try
			{
				return new BerOctetString(Streams.ReadAll(GetOctetStream()));
			}
			catch (IOException e)
			{
				throw new Asn1ParsingException("IOException converting stream to byte array: " + e.Message, e);
			}
		}
	}
}
#pragma warning restore
#endif
