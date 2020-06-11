#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC
{
    public class SimpleLookupTable
        : ECLookupTable
    {
        private static ECPoint[] Copy(ECPoint[] points, int off, int len)
        {
            ECPoint[] result = new ECPoint[len];
            for (int i = 0; i < len; ++i)
            {
                result[i] = points[off + i];
            }
            return result;
        }

        private readonly ECPoint[] points;

        public SimpleLookupTable(ECPoint[] points, int off, int len)
        {
            this.points = Copy(points, off, len);
        }

        public virtual int Size
        {
            get { return points.Length; }
        }

        public virtual ECPoint Lookup(int index)
        {
            return points[index];
        }
    }
}
#pragma warning restore
#endif
