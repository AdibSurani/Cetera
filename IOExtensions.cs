using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    static class IOExtensions
    {
        // BinaryReader
        public static string ReadString(this BinaryReader br, Encoding encoding, int count) => encoding.GetString(br.ReadBytes(count));
        public static string ReadCString(this BinaryReader br) => string.Concat(Enumerable.Range(0, 999).Select(_ => br.ReadChar()).TakeWhile(c => c != 0));
        public static unsafe T ReadStruct<T>(this BinaryReader br)
        {
            fixed (byte* pBuffer = br.ReadBytes(Marshal.SizeOf<T>()))
                return Marshal.PtrToStructure<T>((IntPtr)pBuffer);
        }
        public static List<T> ReadMultiple<T>(this BinaryReader br, int count, Func<int, T> func) => Enumerable.Range(0, count).Select(func).ToList();

        // BinaryWriter
        public static void WriteString(this BinaryWriter bw, Encoding encoding, string str)
        {
            var bytes = encoding.GetBytes(str);
            bw.Write((byte)bytes.Length);
            bw.Write(bytes);
        }
        public static unsafe void WriteStruct<T>(this BinaryWriter bw, T item)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            fixed (byte* pBuffer = buffer)
            {
                Marshal.StructureToPtr(item, (IntPtr)pBuffer, false);
            }
            bw.Write(buffer);
        }

        // GZip
        public static byte[] GZipCompress(byte[] bytes)
        {
            var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
            {
                new MemoryStream(bytes).CopyTo(gz);
            }
            return ms.ToArray();
        }

        public static byte[] GZipDecompress(byte[] bytes)
        {
            var ms = new MemoryStream();
            using (var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
            {
                gz.CopyTo(ms);
            }
            return ms.ToArray();
        }
    }
}
