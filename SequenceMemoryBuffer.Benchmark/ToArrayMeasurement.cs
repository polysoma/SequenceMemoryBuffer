using BenchmarkDotNet.Attributes;

namespace SequenceMemoryBuffer.Benchmark;

[MemoryDiagnoser]
public class ToArrayMeasurement
{
    private static readonly int[] TestSizeList = [1001, 3003, 0x8000, 0x1FFFFF];

    [Benchmark]
    public byte[] MemoryStream()
    {
        using var ms = new MemoryStream();
        foreach (var size in TestSizeList)
        {
            var buffer = new byte[size];
            ms.Write(buffer, 0, buffer.Length);
        }
        ms.Flush();
        return ms.ToArray();
    }

    [Benchmark]
    public byte[] SequenceMemoryBuffer()
    {
        var seqMemBuff = new SequenceMemoryBuffer.SequenceMemoryBuffer<byte>();
        foreach (var size in TestSizeList)
        {
            var buffer = new byte[size];
            seqMemBuff.Write(buffer, 0, buffer.Length);
        }
        return seqMemBuff.ToArray();
    }
}
