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
        // General conversion
        public static unsafe T ToStruct<T>(this byte[] buffer)
        {
            fixed (byte* pBuffer = buffer)
                return Marshal.PtrToStructure<T>((IntPtr)pBuffer);
        }
        
        // BinaryReader
        public static string ReadString(this BinaryReader br, Encoding encoding, int count) => encoding.GetString(br.ReadBytes(count));
        public static string ReadCString(this BinaryReader br) => string.Concat(Enumerable.Range(0, 999).Select(_ => br.ReadChar()).TakeWhile(c => c != 0));
        public static string ReadCStringA(this BinaryReader br) => string.Concat(Enumerable.Range(0, 999).Select(_ => (char)br.ReadByte()).TakeWhile(c => c != 0));
        public static string ReadCStringW(this BinaryReader br) => string.Concat(Enumerable.Range(0, 999).Select(_ => (char)br.ReadInt16()).TakeWhile(c => c != 0));
        public static unsafe T ReadStruct<T>(this BinaryReader br) => br.ReadBytes(Marshal.SizeOf<T>()).ToStruct<T>();
        public static List<T> ReadMultiple<T>(this BinaryReader br, int count, Func<int, T> func) => Enumerable.Range(0, count).Select(func).ToList();
        public static List<NW4CSection> ReadSections(this BinaryReader br)
        {
            return (from _ in Enumerable.Range(0, br.ReadStruct<NW4CHeader>().section_count)
                    let magic = (string)br.ReadStruct<String4>()
                    let data = br.ReadBytes(br.ReadInt32() - 8)
                    select new NW4CSection(magic, data)
                    ).ToList();
        }

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
