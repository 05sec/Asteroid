#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC.Multiplier
{
    public class ZSignedDigitR2LMultiplier 
        : AbstractECMultiplier
    {
        /**
         * 'Zeroless' Signed Digit Right-to-Left.
         */
        protected override ECPoint MultiplyPositive(ECPoint p, BigInteger k)
        {
            ECPoint R0 = p.Curve.Infinity, R1 = p;

            int n = k.BitLength;
            int s = k.GetLowestSetBit();

            R1 = R1.TimesPow2(s);

            int i = s;
            while (++i < n)
            {
                R0 = R0.Add(k.TestBit(i) ? R1 : R1.Negate());
                R1 = R1.Twice();
            }

            R0 = R0.Add(R1);

            return R0;
        }
    }
}
#pragma warning restore
#endif
