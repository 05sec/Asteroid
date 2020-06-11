#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs
{
    public class AuthenticatedSafe
        : Asn1Encodable
    {
        private readonly ContentInfo[] info;

		public AuthenticatedSafe(
            Asn1Sequence seq)
        {
            info = new ContentInfo[seq.Count];

			for (int i = 0; i != info.Length; i++)
            {
                info[i] = ContentInfo.GetInstance(seq[i]);
            }
        }

		public AuthenticatedSafe(
            ContentInfo[] info)
        {
            this.info = (ContentInfo[]) info.Clone();
        }

		public ContentInfo[] GetContentInfo()
        {
            return (ContentInfo[]) info.Clone();
        }

		public override Asn1Object ToAsn1Object()
        {
			return new BerSequence(info);
        }
    }
}
#pragma warning restore
#endif
