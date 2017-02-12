using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera.IO
{
    public class BinaryReaderX : BinaryReader
    {
        int nibbles = -1;

        public BinaryReaderX(Stream input, bool leaveOpen = false) : base(input, Encoding.Unicode, leaveOpen)
        {
        }

        public string ReadString(Encoding encoding, int length) => encoding.GetString(ReadBytes(length));
        public string ReadCStringA() => string.Concat(Enumerable.Range(0, 999).Select(_ => (char)ReadByte()).TakeWhile(c => c != 0));
        public string ReadCStringW() => string.Concat(Enumerable.Range(0, 999).Select(_ => (char)ReadInt16()).TakeWhile(c => c != 0));
        public unsafe T ReadStruct<T>() => ReadBytes(Marshal.SizeOf<T>()).ToStruct<T>();
        public List<T> ReadMultiple<T>(int count, Func<int, T> func) => Enumerable.Range(0, count).Select(func).ToList();

        public int ReadNibble()
        {
            if (nibbles == -1)
            {
                nibbles = ReadByte();
                return nibbles % 16;
            }
            else
            {
                int val = nibbles / 16;
                nibbles = -1;
                return val;
            }
        }

        public List<NW4CSection> ReadSections(out string magic)
        {
            var header = ReadStruct<NW4CHeader>();
            magic = header.magic;
            return (from _ in Enumerable.Range(0, header.section_count)
                    let magic1 = ReadStruct<String4>()
                    let data = ReadBytes(ReadInt32() - 8)
                    select new NW4CSection(magic1, data)
                    ).ToList();
        }

        public List<NW4CSection> ReadSections()
        {
            return (from _ in Enumerable.Range(0, ReadStruct<NW4CHeader>().section_count)
                    let magic = ReadStruct<String4>()
                    let data = ReadBytes(ReadInt32() - 8)
                    select new NW4CSection(magic, data)
                    ).ToList();
        }
    }
}
