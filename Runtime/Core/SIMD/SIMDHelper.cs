using UnityEngine;
using System;

namespace MySys22.DialogueEngine.Core.SIMD
{
    public static class SimdHelper
    {

        private static bool IsTypeAvailable(string typeName)
        {
            try { var type = Type.GetType(typeName); return type != null; }
            catch { return false; }
        }

        private static bool IsPropertyAvailable(string typeName, string propertyName)
        {
            try
            {
                var type = Type.GetType(typeName);
                if (type == null) return false;
                var prop = type.GetProperty(propertyName);
                if (prop == null) return false;
                return (bool)prop.GetValue(null);
            }
            catch { return false; }
        }

        public static bool IsAvx512Supported
        {
            get
            {
#if UNITY_WEBGL
                return false;
#elif UNITY_BURST
                try
                {
                    var type = Type.GetType("Unity.Burst.Intrinsics.X86+Avx512F, Unity.Burst");
                    if (type != null)
                    {
                        var prop = type.GetProperty("IsAvx512FSupported");
                        if (prop != null) return (bool)prop.GetValue(null);
                    }
                    return IsPropertyAvailable("Unity.Burst.Intrinsics.X86+Avx512F", "IsAvx512FSupported");
                }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        public static bool IsAvx2Supported
        {
            get
            {
#if UNITY_WEBGL
                return false;
#elif UNITY_BURST
                try { return IsPropertyAvailable("Unity.Burst.Intrinsics.X86+Avx2", "IsAvx2Supported"); }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        public static bool IsSse42Supported
        {
            get
            {
#if UNITY_WEBGL
                return false;
#elif UNITY_BURST
                try { return IsPropertyAvailable("Unity.Burst.Intrinsics.X86+Sse4_2", "IsSse4_2Supported"); }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        public static bool IsNeonSupported
        {
            get
            {
#if UNITY_WEBGL
                return false;
#elif UNITY_BURST
                try { return IsPropertyAvailable("Unity.Burst.Intrinsics.Arm+Neon", "IsNeonSupported"); }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        public static bool EnableSimd { get; set; } = true;

        public static bool UseSimd => EnableSimd &&
#if UNITY_WEBGL
            false
#else
            (IsAvx512Supported || IsAvx2Supported || IsSse42Supported || IsNeonSupported)
#endif
        ;

        public static string GetBestArchitecture()
        {
            if (IsAvx512Supported) return "AVX-512 (512-bit)";
            if (IsAvx2Supported) return "AVX2 (256-bit)";
            if (IsSse42Supported) return "SSE4.2 (128-bit)";
            if (IsNeonSupported) return "NEON (128-bit)";
            return "Scalar (No SIMD)";
        }

        public static void LogArchitecture()
        {
            DialogueLogger.Log($"Using {GetBestArchitecture()}");
        }
    }
}
