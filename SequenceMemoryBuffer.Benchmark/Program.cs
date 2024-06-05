using BenchmarkDotNet.Running;
using SequenceMemoryBuffer.Benchmark;

var summary = BenchmarkRunner.Run<ToArrayMeasurement>();
