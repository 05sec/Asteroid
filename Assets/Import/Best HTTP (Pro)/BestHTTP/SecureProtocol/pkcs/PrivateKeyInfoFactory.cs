#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.CryptoPro;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.EdEC;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Oiw;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Sec;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X9;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Generators;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Pkcs
{
    public sealed class PrivateKeyInfoFactory
    {
        private PrivateKeyInfoFactory()
        {
        }

        public static PrivateKeyInfo CreatePrivateKeyInfo(
            AsymmetricKeyParameter privateKey)
        {
            return CreatePrivateKeyInfo(privateKey, null);
        }

        /**
         * Create a PrivateKeyInfo representation of a private key with attributes.
         *
         * @param privateKey the key to be encoded into the info object.
         * @param attributes the set of attributes to be included.
         * @return the appropriate PrivateKeyInfo
         * @throws java.io.IOException on an error encoding the key
         */
        public static PrivateKeyInfo CreatePrivateKeyInfo(AsymmetricKeyParameter privateKey, Asn1Set attributes)
        {
            if (privateKey == null)
                throw new ArgumentNullException("privateKey");
            if (!privateKey.IsPrivate)
                throw new ArgumentException("Public key passed - private key expected", "privateKey");

            if (privateKey is ElGamalPrivateKeyParameters)
            {
                ElGamalPrivateKeyParameters _key = (ElGamalPrivateKeyParameters)privateKey;
                ElGamalParameters egp = _key.Parameters;
                return new PrivateKeyInfo(
                    new AlgorithmIdentifier(OiwObjectIdentifiers.ElGamalAlgorithm, new ElGamalParameter(egp.P, egp.G).ToAsn1Object()),
                    new DerInteger(_key.X),
                    attributes);
            }

            if (privateKey is DsaPrivateKeyParameters)
            {
                DsaPrivateKeyParameters _key = (DsaPrivateKeyParameters)privateKey;
                DsaParameters dp = _key.Parameters;
                return new PrivateKeyInfo(
                    new AlgorithmIdentifier(X9ObjectIdentifiers.IdDsa, new DsaParameter(dp.P, dp.Q, dp.G).ToAsn1Object()),
                    new DerInteger(_key.X),
                    attributes);
            }

            if (privateKey is DHPrivateKeyParameters)
            {
                DHPrivateKeyParameters _key = (DHPrivateKeyParameters)privateKey;

                DHParameter p = new DHParameter(
                    _key.Parameters.P, _key.Parameters.G, _key.Parameters.L);

                return new PrivateKeyInfo(
                    new AlgorithmIdentifier(_key.AlgorithmOid, p.ToAsn1Object()),
                    new DerInteger(_key.X),
                    attributes);
            }

            if (privateKey is RsaKeyParameters)
            {
                AlgorithmIdentifier algID = new AlgorithmIdentifier(
                    PkcsObjectIdentifiers.RsaEncryption, DerNull.Instance);

                RsaPrivateKeyStructure keyStruct;
                if (privateKey is RsaPrivateCrtKeyParameters)
                {
                    RsaPrivateCrtKeyParameters _key = (RsaPrivateCrtKeyParameters)privateKey;

                    keyStruct = new RsaPrivateKeyStructure(
                        _key.Modulus,
                        _key.PublicExponent,
                        _key.Exponent,
                        _key.P,
                        _key.Q,
                        _key.DP,
                        _key.DQ,
                        _key.QInv);
                }
                else
                {
                    RsaKeyParameters _key = (RsaKeyParameters) privateKey;

                    keyStruct = new RsaPrivateKeyStructure(
                        _key.Modulus,
                        BigInteger.Zero,
                        _key.Exponent,
                        BigInteger.Zero,
                        BigInteger.Zero,
                        BigInteger.Zero,
                        BigInteger.Zero,
                        BigInteger.Zero);
                }

                return new PrivateKeyInfo(algID, keyStruct.ToAsn1Object(), attributes);
            }

            if (privateKey is ECPrivateKeyParameters)
            {
                ECPrivateKeyParameters priv = (ECPrivateKeyParameters)privateKey;
                DerBitString publicKey = new DerBitString(ECKeyPairGenerator.GetCorrespondingPublicKey(priv).Q.GetEncoded(false));

                ECDomainParameters dp = priv.Parameters;
                int orderBitLength = dp.N.BitLength;

                AlgorithmIdentifier algID;
                ECPrivateKeyStructure ec;

                if (priv.AlgorithmName == "ECGOST3410")
                {
                    if (priv.PublicKeyParamSet == null)
                        throw BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Platform.CreateNotImplementedException("Not a CryptoPro parameter set");

                    Gost3410PublicKeyAlgParameters gostParams = new Gost3410PublicKeyAlgParameters(
                        priv.PublicKeyParamSet, CryptoProObjectIdentifiers.GostR3411x94CryptoProParamSet);

                    algID = new AlgorithmIdentifier(CryptoProObjectIdentifiers.GostR3410x2001, gostParams);

                    // TODO Do we need to pass any parameters here?
                    ec = new ECPrivateKeyStructure(orderBitLength, priv.D, publicKey, null);
                }
                else
                {
                    X962Parameters x962;
                    if (priv.PublicKeyParamSet == null)
                    {
                        X9ECParameters ecP = new X9ECParameters(dp.Curve, dp.G, dp.N, dp.H, dp.GetSeed());
                        x962 = new X962Parameters(ecP);
                    }
                    else
                    {
                        x962 = new X962Parameters(priv.PublicKeyParamSet);
                    }

                    ec = new ECPrivateKeyStructure(orderBitLength, priv.D, publicKey, x962);

                    algID = new AlgorithmIdentifier(X9ObjectIdentifiers.IdECPublicKey, x962);
                }

                return new PrivateKeyInfo(algID, ec, attributes);
            }

            if (privateKey is Gost3410PrivateKeyParameters)
            {
                Gost3410PrivateKeyParameters _key = (Gost3410PrivateKeyParameters)privateKey;

                if (_key.PublicKeyParamSet == null)
                    throw BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Platform.CreateNotImplementedException("Not a CryptoPro parameter set");

                byte[] keyEnc = _key.X.ToByteArrayUnsigned();
                byte[] keyBytes = new byte[keyEnc.Length];

                for (int i = 0; i != keyBytes.Length; i++)
                {
                    keyBytes[i] = keyEnc[keyEnc.Length - 1 - i]; // must be little endian
                }

                Gost3410PublicKeyAlgParameters algParams = new Gost3410PublicKeyAlgParameters(
                    _key.PublicKeyParamSet, CryptoProObjectIdentifiers.GostR3411x94CryptoProParamSet, null);

                AlgorithmIdentifier algID = new AlgorithmIdentifier(
                    CryptoProObjectIdentifiers.GostR3410x94,
                    algParams.ToAsn1Object());

                return new PrivateKeyInfo(algID, new DerOctetString(keyBytes), attributes);
            }

            if (privateKey is X448PrivateKeyParameters)
            {
                X448PrivateKeyParameters key = (X448PrivateKeyParameters)privateKey;

                return new PrivateKeyInfo(new AlgorithmIdentifier(EdECObjectIdentifiers.id_X448),
                    new DerOctetString(key.GetEncoded()), attributes, key.GeneratePublicKey().GetEncoded());
            }

            if (privateKey is X25519PrivateKeyParameters)
            {
                X25519PrivateKeyParameters key = (X25519PrivateKeyParameters)privateKey;

                return new PrivateKeyInfo(new AlgorithmIdentifier(EdECObjectIdentifiers.id_X25519),
                    new DerOctetString(key.GetEncoded()), attributes, key.GeneratePublicKey().GetEncoded());
            }

            if (privateKey is Ed448PrivateKeyParameters)
            {
                Ed448PrivateKeyParameters key = (Ed448PrivateKeyParameters)privateKey;

                return new PrivateKeyInfo(new AlgorithmIdentifier(EdECObjectIdentifiers.id_Ed448),
                    new DerOctetString(key.GetEncoded()), attributes, key.GeneratePublicKey().GetEncoded());
            }

            if (privateKey is Ed25519PrivateKeyParameters)
            {
                Ed25519PrivateKeyParameters key = (Ed25519PrivateKeyParameters)privateKey;

                return new PrivateKeyInfo(new AlgorithmIdentifier(EdECObjectIdentifiers.id_Ed25519),
                    new DerOctetString(key.GetEncoded()), attributes, key.GeneratePublicKey().GetEncoded());
            }

            throw new ArgumentException("Class provided is not convertible: " + BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Platform.GetTypeName(privateKey));
        }

        public static PrivateKeyInfo CreatePrivateKeyInfo(
            char[]					passPhrase,
            EncryptedPrivateKeyInfo	encInfo)
        {
            return CreatePrivateKeyInfo(passPhrase, false, encInfo);
        }

        public static PrivateKeyInfo CreatePrivateKeyInfo(
            char[]					passPhrase,
            bool					wrongPkcs12Zero,
            EncryptedPrivateKeyInfo	encInfo)
        {
            AlgorithmIdentifier algID = encInfo.EncryptionAlgorithm;

            IBufferedCipher cipher = PbeUtilities.CreateEngine(algID) as IBufferedCipher;
            if (cipher == null)
                throw new Exception("Unknown encryption algorithm: " + algID.Algorithm);

            ICipherParameters cipherParameters = PbeUtilities.GenerateCipherParameters(
                algID, passPhrase, wrongPkcs12Zero);
            cipher.Init(false, cipherParameters);
            byte[] keyBytes = cipher.DoFinal(encInfo.GetEncryptedData());

            return PrivateKeyInfo.GetInstance(keyBytes);
        }
    }
}
#pragma warning restore
#endif
