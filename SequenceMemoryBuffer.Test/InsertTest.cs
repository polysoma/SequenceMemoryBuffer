namespace SequenceMemoryBuffer.Test;

public class InsertTest
{
    private static readonly int[] TestSizeList = [1001, 3003, 0x8000, 0x1FFFFF];

    [Fact]
    public void TestInsertBuffer()
    {
        foreach (var size in TestSizeList)
        {
            var buffer = new byte[size];
            var rand = new Random();
            rand.NextBytes(buffer);

            var seqMemoryBuffer = new SequenceMemoryBuffer.SequenceMemoryBuffer<byte>();
            seqMemoryBuffer.Write(buffer, 0, buffer.Length);

            var seqBuffer = seqMemoryBuffer.ToArray();

            Assert.Equal(buffer, seqBuffer);
        }
    }

    [Fact]
    public void TestInsertSpan()
    {
        foreach (var size in TestSizeList)
        {
            Span<byte> span = new Span<byte>(new byte[size]);
            var rand = new Random();
            rand.NextBytes(span);

            var seqMemoryBuffer = new SequenceMemoryBuffer.SequenceMemoryBuffer<byte>();
            seqMemoryBuffer.Write(span);

            var seqBuffer = seqMemoryBuffer.ToArray();

            Assert.Equal(span.ToArray(), seqBuffer);
        }
    }

    [Fact]
    public void TestInsertMultiSpan()
    {
        foreach (var size in TestSizeList)
        {
            using var ms = new MemoryStream();
            var seqMemoryBuffer = new SequenceMemoryBuffer.SequenceMemoryBuffer<byte>();

            foreach (var size2 in TestSizeList)
            {
                Span<byte> span = new Span<byte>(new byte[size]);
                var rand = new Random();
                rand.NextBytes(span);

                ms.Write(span);
                seqMemoryBuffer.Write(span);
            }
            ms.Flush();

            // ReadOnlySequenceの出力を確認
            using var ms2 = new MemoryStream();
            foreach (var memory in seqMemoryBuffer.Buffer)
            {
                ms2.Write(memory.Span);
            }
            ms2.Flush();

            Assert.Equal(ms.ToArray(), ms2.ToArray());
        }
    }
}
