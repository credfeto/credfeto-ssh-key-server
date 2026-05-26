using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Credfeto.Keys.Server.Crypto;

internal static class SshWireReader
{
    public static uint ReadUInt32(ReadOnlySpan<byte> data, ref int position)
    {
        if (position + 4 > data.Length)
        {
            throw new InvalidDataException("Unexpected end of SSH data reading uint32");
        }

        uint value = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(start: position, length: 4));
        position += 4;

        return value;
    }

    public static byte ReadByte(ReadOnlySpan<byte> data, ref int position)
    {
        if (position >= data.Length)
        {
            throw new InvalidDataException("Unexpected end of SSH data reading byte");
        }

        return data[position++];
    }

    public static byte[] ReadStringBytes(ReadOnlySpan<byte> data, ref int position)
    {
        uint len = ReadUInt32(data, ref position);

        if (len > int.MaxValue)
        {
            throw new InvalidDataException("SSH string length too large");
        }

        int ilen = (int)len;

        if (position + ilen > data.Length)
        {
            throw new InvalidDataException("Unexpected end of SSH data reading string content");
        }

        byte[] result = data.Slice(start: position, length: ilen).ToArray();
        position += ilen;

        return result;
    }

    public static string ReadUtf8String(ReadOnlySpan<byte> data, ref int position)
    {
        uint len = ReadUInt32(data, ref position);

        if (len > int.MaxValue)
        {
            throw new InvalidDataException("SSH string length too large");
        }

        int ilen = (int)len;

        if (position + ilen > data.Length)
        {
            throw new InvalidDataException("Unexpected end of SSH data reading string content");
        }

        string result = Encoding.UTF8.GetString(data.Slice(start: position, length: ilen));
        position += ilen;

        return result;
    }
}
