#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC.Multiplier;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Generators
{
    /**
     * a DSA key pair generator.
     *
     * This Generates DSA keys in line with the method described
     * in <i>FIPS 186-3 B.1 FFC Key Pair Generation</i>.
     */
    public class DsaKeyPairGenerator
        : IAsymmetricCipherKeyPairGenerator
    {
        private static readonly BigInteger One = BigInteger.One;

        private DsaKeyGenerationParameters param;

        public void Init(
            KeyGenerationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            // Note: If we start accepting instances of KeyGenerationParameters,
            // must apply constraint checking on strength (see DsaParametersGenerator.Init)

            this.param = (DsaKeyGenerationParameters) parameters;
        }

        public AsymmetricCipherKeyPair GenerateKeyPair()
        {
            DsaParameters dsaParams = param.Parameters;

            BigInteger x = GeneratePrivateKey(dsaParams.Q, param.Random);
            BigInteger y = CalculatePublicKey(dsaParams.P, dsaParams.G, x);

            return new AsymmetricCipherKeyPair(
                new DsaPublicKeyParameters(y, dsaParams),
                new DsaPrivateKeyParameters(x, dsaParams));
        }

        private static BigInteger GeneratePrivateKey(BigInteger q, SecureRandom random)
        {
            // B.1.2 Key Pair Generation by Testing Candidates
            int minWeight = q.BitLength >> 2;
            for (;;)
            {
                // TODO Prefer this method? (change test cases that used fixed random)
                // B.1.1 Key Pair Generation Using Extra Random Bits
                //BigInteger x = new BigInteger(q.BitLength + 64, random).Mod(q.Subtract(One)).Add(One);

                BigInteger x = BigIntegers.CreateRandomInRange(One, q.Subtract(One), random);
                if (WNafUtilities.GetNafWeight(x) >= minWeight)
                {
                    return x;
                }
            }
        }

        private static BigInteger CalculatePublicKey(BigInteger p, BigInteger g, BigInteger x)
        {
            return g.ModPow(x, p);
        }
    }
}
#pragma warning restore
#endif
