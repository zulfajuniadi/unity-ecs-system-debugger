using System;
using System.Security.Cryptography;
using UnityEngine;

namespace Demo.Utils
{
    /// <summary>
    /// Threadsafe Random number generator
    /// Nondeterministic
    /// </summary>
    public static class Random
    {

        static RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider ();

        public static Vector3 InUnitSphere ()
        {
            var result = new Vector3 (value, value, value);
            return result.normalized;
        }

        public static Quaternion Rotation ()
        {
            return Quaternion.Euler (InUnitSphere () * 360f);
        }

        public static bool boolean
        {
            get
            {
                return value < 0.5f;
            }
        }

        public static float value
        {
            get
            {
                byte[] b = new byte[4];
                provider.GetBytes (b);
                var num = (float) BitConverter.ToUInt32 (b, 0) / UInt32.MaxValue;
                num -= 0.5f;
                num *= 2f;
                return num;
            }
        }
    }
}