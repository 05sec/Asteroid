#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Security
{
	[Obsolete("Never thrown")]
#if !(NETCF_1_0 || NETCF_2_0 || SILVERLIGHT || PORTABLE || NETFX_CORE)
    [Serializable]
#endif
    public class NoSuchAlgorithmException : GeneralSecurityException
	{
		public NoSuchAlgorithmException() : base() {}
		public NoSuchAlgorithmException(string message) : base(message) {}
		public NoSuchAlgorithmException(string message, Exception exception) : base(message, exception) {}
	}
}
#pragma warning restore
#endif
