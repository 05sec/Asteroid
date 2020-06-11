#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto
{
	/// <summary>
	/// Base interface describing an entropy source for a DRBG.
	/// </summary>
	public interface IEntropySource
	{
		/// <summary>
		/// Return whether or not this entropy source is regarded as prediction resistant.
		/// </summary>
		/// <value><c>true</c> if this instance is prediction resistant; otherwise, <c>false</c>.</value>
		bool IsPredictionResistant { get; }

		/// <summary>
		/// Return a byte array of entropy.
		/// </summary>
		/// <returns>The entropy bytes.</returns>
		byte[] GetEntropy();

		/// <summary>
		/// Return the number of bits of entropy this source can produce.
		/// </summary>
		/// <value>The size, in bits, of the return value of getEntropy.</value>
		int EntropySize { get; }
	}
}

#pragma warning restore
#endif
