#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Digests
{
    public class GOST3411_2012_512Digest:GOST3411_2012Digest
    {
		private readonly static byte[] IV = {
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
	};

		public override string AlgorithmName
		{
			get { return "GOST3411-2012-512"; }
		}

        public GOST3411_2012_512Digest():base(IV)
        {
        }

		public GOST3411_2012_512Digest(GOST3411_2012_512Digest other) : base(IV)
		{
            Reset(other);
        }

        public override int GetDigestSize()
        {
            return 64;
        }

		public override IMemoable Copy()
		{
			return new GOST3411_2012_512Digest(this);
		}
    }
}
#pragma warning restore
#endif
