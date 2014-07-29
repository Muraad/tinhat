using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tinhat;

namespace test
{
    class TestProgram
    {
        static double GetCompressionRatio(byte[] data)
        {
            var randBits = new byte[data.Length * 8];
            for (int i = 0; i < data.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    randBits[8 * i + j] = (byte)((data[i] >> j) % 2);
                }
            }
            double retVal = double.MaxValue;
            using (var outStream = new System.IO.MemoryStream())
            {
                using (var gzStream = new System.IO.Compression.GZipStream(outStream,System.IO.Compression.CompressionMode.Compress))
                {
                    using (var inStream = new System.IO.MemoryStream(randBits))
                    {
                        inStream.CopyTo(gzStream);
                    }
                }
                var outBytes = outStream.ToArray();
                double gzRatio = ((double)outBytes.Length / data.Length);
                if (gzRatio < retVal)
                {
                    retVal = gzRatio;
                }
            }
            using (var outStream = new System.IO.MemoryStream())
            {
                using (var bzipStream = new SharpCompress.Compressor.BZip2.BZip2Stream(outStream,SharpCompress.Compressor.CompressionMode.Compress,true))
                {
                    using (var inStream = new System.IO.MemoryStream(randBits))
                    {
                        inStream.CopyTo(bzipStream);
                    }
                }
                var outBytes = outStream.ToArray();
                double bzip2Ratio = ((double)outBytes.Length / data.Length);
                if (bzip2Ratio < retVal)
                {
                    retVal = bzip2Ratio;
                }
            }
            using (var outStream = new System.IO.MemoryStream())
            {
                var lzmaProps = new SharpCompress.Compressor.LZMA.LzmaEncoderProperties();
                using (var lzmaStream = new SharpCompress.Compressor.LZMA.LzmaStream(lzmaProps,false,outStream))
                {
                    using (var inStream = new System.IO.MemoryStream(randBits))
                    {
                        inStream.CopyTo(lzmaStream);
                    }
                }
                var outBytes = outStream.ToArray();
                double lzmaRatio = ((double)outBytes.Length / data.Length);
                if (lzmaRatio < retVal)
                {
                    retVal = lzmaRatio;
                }
            }
            return retVal;
        }
        private class RandResult
        {
            public string AlgorithmName;
            public double CompressionRatio;
            public TimeSpan TimeSpan;
        }
        static void Main(string[] args)
        {
            // It's always good to StartFillingEntropyPools as early as possible when the application is launched.
            // But if I'm benchmarking performance below, then it's not really fair to let them start early.
            // StartEarly.StartFillingEntropyPools();

            DateTime before;
            DateTime after;
            RandResult result;

            const int randBytesLength = 8 * 1024;

            var results = new List<RandResult>();
            result = new RandResult();
            result.AlgorithmName = "AllZeros";
            before = DateTime.Now;
            var randBytes = new byte[randBytesLength];
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);

            result = new RandResult();
            result.AlgorithmName = "SystemRNGCryptoServiceProvider";
            before = DateTime.Now;
            var mySystemRNGCryptoServiceProvider = new tinhat.EntropySources.SystemRNGCryptoServiceProvider();
            mySystemRNGCryptoServiceProvider.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("SystemRNGCryptoServiceProvider " + (after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "RNGCryptoServiceProvider";
            before = DateTime.Now;
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(randBytes);
            }
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("RNGCryptoServiceProvider " + (after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGeneratorRNG";
            before = DateTime.Now;
            var myThreadedSeedGeneratorRNG = new tinhat.EntropySources.ThreadedSeedGeneratorRNG();
            myThreadedSeedGeneratorRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("ThreadedSeedGeneratorRNG " + (after - before).ToString());
            System.Threading.Thread.Sleep(3000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGenerator(fast)";
            var myFastThreadedSeedGeneratorRNG = new Org.BouncyCastle.Crypto.Prng.ThreadedSeedGenerator();
            Array.Clear(randBytes, 0, randBytesLength);
            before = DateTime.Now;
            randBytes = myFastThreadedSeedGeneratorRNG.GenerateSeed(randBytesLength, fast: true);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("ThreadedSeedGenerator(fast) " + (after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGenerator(slow)";
            Array.Clear(randBytes, 0, randBytesLength);
            before = DateTime.Now;
            randBytes = myFastThreadedSeedGeneratorRNG.GenerateSeed(randBytesLength, fast: false);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("ThreadedSeedGenerator(slow) " + (after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            const int numResults = 6;
            var ThreadSchedulerResults = new RandResult[numResults+1];     // Will test once with default settings, and 4 times with individual bits
            var ThreadSchedulerResultsBytes = new byte[numResults + 1][];
            for (int i = 0; i < numResults+1; i++)
            {
                ThreadSchedulerResults[i] = new RandResult();
                ThreadSchedulerResultsBytes[i] = new byte[randBytesLength];
            }
            ThreadSchedulerResults[numResults].AlgorithmName = "ThreadSchedulerRNG (with mixing)";
            before = DateTime.Now;
            var myThreadSchedulerRng = new tinhat.EntropySources.ThreadSchedulerRNG();
            myThreadSchedulerRng.GetBytes(ThreadSchedulerResultsBytes[numResults]);
            after = DateTime.Now;
            System.Console.WriteLine("ThreadSchedulerRNG (with mixing) " + (after - before).ToString());
            ThreadSchedulerResults[numResults].TimeSpan = after - before;
            tinhat.EntropySources.ThreadSchedulerRNG.UseMixingFunction = false;
            for (int bitPosition = 0; bitPosition < numResults; bitPosition++)
            {
                ThreadSchedulerResults[bitPosition].AlgorithmName = "ThreadSchedulerRNG (bit " + bitPosition.ToString() + ")";
                tinhat.EntropySources.ThreadSchedulerRNG.UseBitPosition = bitPosition;
                before = DateTime.Now;
                myThreadSchedulerRng.GetBytes(ThreadSchedulerResultsBytes[bitPosition]);
                after = DateTime.Now;
                System.Console.WriteLine(ThreadSchedulerResults[bitPosition].AlgorithmName + " " + (after - before).ToString());
                ThreadSchedulerResults[bitPosition].TimeSpan = after - before;
            }
            tinhat.EntropySources.ThreadSchedulerRNG.UseMixingFunction = true;
            for (int i = 0; i < numResults + 1; i++)
            {
                ThreadSchedulerResults[i].CompressionRatio = GetCompressionRatio(ThreadSchedulerResultsBytes[i]);
                tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(ThreadSchedulerResultsBytes[i]);
                results.Add(ThreadSchedulerResults[i]);
            }
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG";
            var myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG();
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("EntropyFileRNG " + (after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (RIPEMD256_256bit)";
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.RIPEMD256_256bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("EntropyFileRNG  (RIPEMD256_256bit)" + (after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (SHA256_256bit)";
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.SHA256_256bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("EntropyFileRNG  (SHA256_256bit)" + (after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (SHA512_512bit)";
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.SHA512_512bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("EntropyFileRNG  (SHA512_512bit)" + (after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (Whirlpool_512bit)";
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.Whirlpool_512bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("EntropyFileRNG  (Whirlpool_512bit)" + (after - before).ToString());

            // I want to test each of the ThreadedSeedGeneratorRNG and ThreadSchedulerRNG prior to doing TinHatRandom
            // or TinHatURandom, because otherwise, TinHatRandom will create static instances of them, which race, etc.
            // thus throwing off my benchmark results.

            result = new RandResult();
            result.AlgorithmName = "TinHatRandom";
            before = DateTime.Now;
            TinHatRandom.StaticInstance.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("TinHatRandom " + (after - before).ToString());
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "TinHatURandom";
            before = DateTime.Now;
            TinHatURandom.StaticInstance.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine("TinHatURandom " + (after - before).ToString());
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            System.Console.WriteLine("");

            double maxCompressionRatio = double.MinValue;
            double minCompressionRatio = double.MaxValue;
            int longestName = 0;
            foreach (var theResult in results)
            {
                if (theResult.AlgorithmName.Length > longestName) longestName = theResult.AlgorithmName.Length;
                if (theResult.CompressionRatio < minCompressionRatio) minCompressionRatio = theResult.CompressionRatio;
                if (theResult.CompressionRatio > maxCompressionRatio) maxCompressionRatio = theResult.CompressionRatio;
            }
            System.Console.WriteLine("AlgorithmName".PadLeft(longestName) + " : bits per bit : elapsed sec : effective rate");
            foreach (var theResult in results)
            {
                double bitsPerBit = (theResult.CompressionRatio - minCompressionRatio) / (maxCompressionRatio - minCompressionRatio);
                double byteRate;
                string byteRateString;
                if (theResult.TimeSpan.TotalSeconds == 0)
                {
                    if (theResult.CompressionRatio == minCompressionRatio)
                    {
                        byteRateString = "0";
                    }
                    else
                    {
                        byteRateString = "infinity";
                    }
                }
                else
                {
                    byteRate = bitsPerBit * randBytesLength / theResult.TimeSpan.TotalSeconds;
                    if (byteRate > 1000000)
                        byteRateString = (byteRate / 1000000).ToString("F2") + " MiB/sec";
                    else if (byteRate > 1000)
                        byteRateString = (byteRate / 1000).ToString("F2") + " KiB/sec";
                    else
                        byteRateString = byteRate.ToString("F2") + " B/sec";
                }
                System.Console.WriteLine(theResult.AlgorithmName.PadLeft(longestName) + " : " + bitsPerBit.ToString("0.000").PadLeft(12) + " : " + theResult.TimeSpan.TotalSeconds.ToString("0.000").PadLeft(11) + " : " + byteRateString.PadLeft(14));
            }

            System.Console.WriteLine("");
            System.Console.Error.WriteLine("Finished");
            System.Console.Out.Flush();
            System.Threading.Thread.Sleep(int.MaxValue);    // Just so the window doesn't close instantly
        }
    }
}
