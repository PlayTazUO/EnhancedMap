using System;
using System.Security.Cryptography;
using System.Text;

namespace EnhancedMapServerNetCore.Cryptography
{
    public static class SHA1
    {
        private static readonly System.Security.Cryptography.SHA1 _provider = System.Security.Cryptography.SHA1.Create();
        private static readonly byte[] _buffer = new byte[256];

        public static string Protect(string input)
        {
            int length = Encoding.ASCII.GetBytes(input, 0, input.Length > 256 ? 256 : input.Length, _buffer, 0);
            byte[] hashed = _provider.ComputeHash(_buffer, 0, length);

            return BitConverter.ToString(hashed);
        }
    }
}