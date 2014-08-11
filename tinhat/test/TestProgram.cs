using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tinhat;
using System.IO;
using SharpCompress.Compressor.LZMA;

namespace test
{
    class TestProgram
    {
        static double GetCompressionRatioLowerBound(int Length)
        {
            /* 
             * If we feed strings of all zero's into lzma, for various input lengths, here are the output lengths, to be 
             * used as the lower bound for how much compression could possibly squish things.  This is actually the envelope
             * of the output size, up to 8192 input size.  So in reality, sometimes it could squish more, but by taking the
             * envelope, we reduce our estimate of the entropy in the user string.
             *     2-274     : 6.985 + 0.007326 * Length
             *     275-564   : 7.103 + 0.00689655 * Length
             *     565-1384  : 7.555 + 0.00609756 * Length
             *     1385-2202 : 9.227 + 0.00488998 * Length
             *     2203-3022 : 9.254 + 0.00487805 * Length
             *     3023-4096 : 12.741 + 0.00372439 * Length
             *     4097-5188 : 12.993 + 0.003663 * Length
             *     5189-6025 : 13.401 + 0.00358423 * Length
             *     >=6026    : 17.341 + 0.0029304 * Length
             *     
             * This is the envelope of the output size, for varying lengths of input obtained from urandom
             *     1-8       : 4.875 + 1.125 * Length
             *     9-37      : 5.068965512 + 1.103448276 * Length
             *     38-49     : 5.8333333 + 1.08333333 * Length
             *     50-63     : 6.428571429 + 1.071428571 * Length
             *     64-95     : 7 + 1.0625 * Length
             *     96-142    : 8.914893617 + 1.042553191 * Length
             *     143-168   : 9.5 + 1.038461538 * Length
             *     169-203   : 11.17142857 + 1.028571429 * Length
             *     204-401   : 11.84848485 + 1.025252525 * Length
             *     >=402     : 16.94700013 + 1.012569651 * Length
            */
            if (Length < 275)
                return 6.985 + 0.007326 * Length;
            else if (Length < 565)
                return 7.103 + 0.00689655 * Length;
            else if (Length < 1385)
                return 7.555 + 0.00609756 * Length;
            else if (Length < 2203)
                return 9.227 + 0.00488998 * Length;
            else if (Length < 3023)
                return 9.254 + 0.00487805 * Length;
            else if (Length < 4097)
                return 12.741 + 0.00372439 * Length;
            else if (Length < 5189)
                return 12.993 + 0.003663 * Length;
            else if (Length < 6026)
                return 13.401 + 0.00358423 * Length;
            else
                return 17.341 + 0.0029304 * Length;
        }
        static double GetCompressionRatioUpperBound(int Length)
        {
            if (Length < 9)
                return 4.875 + 1.125 * Length;
            else if (Length < 38)
                return 5.068965512 + 1.103448276 * Length;
            else if (Length < 50)
                return 5.8333333 + 1.08333333 * Length;
            else if (Length < 64)
                return 6.428571429 + 1.071428571 * Length;
            else if (Length < 96)
                return 7 + 1.0625 * Length;
            else if (Length < 143)
                return 8.914893617 + 1.042553191 * Length;
            else if (Length < 169)
                return 9.5 + 1.038461538 * Length;
            else if (Length < 204)
                return 11.17142857 + 1.028571429 * Length;
            else if (Length < 402)
                return 11.84848485 + 1.025252525 * Length;
            else
                return 16.94700013 + 1.012569651 * Length;
        }
        static double GetCompressionRatio(byte[] data)
        {
            int Length = data.Length;
            double lowerBound = GetCompressionRatioLowerBound(Length);
            double upperBound = GetCompressionRatioUpperBound(Length);

            double compressionRatio;
            using (var outStream = new MemoryStream())
            {
                using (var lzmaStream = new LzmaStream(new LzmaEncoderProperties(), false, outStream))
                {
                    using (var inStream = new MemoryStream(data))
                    {
                        inStream.CopyTo(lzmaStream);
                    }
                }
                byte[] outBytes = outStream.ToArray();
                compressionRatio = (outBytes.Length - lowerBound) / (upperBound - lowerBound);
                // Because we used an envelope for both the upper and lower bound, the compression ratio is very unlikely
                // to exceed 1.0, but it's possible.  It will quite often be negative if the input is pure rubbish.
                if (compressionRatio > 1.0)
                    compressionRatio = 1.0;
                if (compressionRatio < 0)
                    compressionRatio = 0;
            }
            return compressionRatio;
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
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            var mySystemRNGCryptoServiceProvider = new tinhat.EntropySources.SystemRNGCryptoServiceProvider();
            mySystemRNGCryptoServiceProvider.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "RNGCryptoServiceProvider";
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(randBytes);
            }
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGeneratorRNG";
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            var myThreadedSeedGeneratorRNG = new tinhat.EntropySources.ThreadedSeedGeneratorRNG();
            myThreadedSeedGeneratorRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            System.Console.Write("Sleeping to allow pool to fill...");
            System.Threading.Thread.Sleep(3000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            System.Console.WriteLine("  Done.");
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGenerator(fast)";
            System.Console.Write(result.AlgorithmName + " ");
            var myThreadedSeedGenerator= new Org.BouncyCastle.Crypto.Prng.ThreadedSeedGenerator();
            Array.Clear(randBytes, 0, randBytesLength);
            before = DateTime.Now;
            randBytes = myThreadedSeedGenerator.GenerateSeed(randBytesLength, fast: true);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadedSeedGenerator(slow)";
            System.Console.Write(result.AlgorithmName + " ");
            Array.Clear(randBytes, 0, randBytesLength);
            before = DateTime.Now;
            randBytes = myThreadedSeedGenerator.GenerateSeed(randBytesLength, fast: false);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "ThreadSchedulerRNG";
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            var myThreadSchedulerRNG = new tinhat.EntropySources.ThreadSchedulerRNG();
            myThreadSchedulerRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            const int numResults = 14;
            System.Console.Write("ticks bit positions ");
            before = DateTime.Now;
            var ticksResults = new RandResult[numResults];
            var ticksResultsBytes = new byte[numResults][];
            for (int i = 0; i < numResults; i++)
            {
                ticksResults[i] = new RandResult();
                ticksResults[i].AlgorithmName = "ticks bit #" + i.ToString().PadLeft(2);
                ticksResultsBytes[i] = new byte[randBytesLength];
            }
            for (int i = 0; i < randBytesLength; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    long ticks = DateTime.Now.Ticks;
                    for (int bitPos = 0; bitPos < numResults; bitPos++)
                    {
                        ticksResultsBytes[bitPos][i] <<= 1;
                        ticksResultsBytes[bitPos][i] += (byte)(ticks % 2);
                        ticks >>= 1;
                    }
                    System.Threading.Thread.Sleep(1);
                }
            }
            after = DateTime.Now;
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(tinhat.EntropySources.EntropyFileRNG.ConcatenateByteArrays(ticksResultsBytes));
            for (int i = 0; i < numResults; i++)
            {
                ticksResults[i].TimeSpan = after - before;
                ticksResults[i].CompressionRatio = GetCompressionRatio(ticksResultsBytes[i]);
                results.Add(ticksResults[i]);
            }
            System.Console.WriteLine((after - before).ToString());
            System.Console.Write("Sleeping to allow pool to fill...");
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            System.Console.WriteLine("  Done.");

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG";
            System.Console.Write(result.AlgorithmName + " ");
            var myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG();
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (RIPEMD256_256bit)";
            System.Console.Write(result.AlgorithmName + " ");
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.RIPEMD256_256bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (SHA256_256bit)";
            System.Console.Write(result.AlgorithmName + " ");
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.SHA256_256bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (SHA512_512bit)";
            System.Console.Write(result.AlgorithmName + " ");
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.SHA512_512bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());

            result = new RandResult();
            result.AlgorithmName = "EntropyFileRNG (Whirlpool_512bit)";
            System.Console.Write(result.AlgorithmName + " ");
            myEntropyFileRNG = new tinhat.EntropySources.EntropyFileRNG(prngAlgorithm: tinhat.EntropySources.EntropyFileRNG.PrngAlgorithm.Whirlpool_512bit);
            before = DateTime.Now;
            myEntropyFileRNG.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());

            // I want to test each of the ThreadedSeedGeneratorRNG and ThreadSchedulerRNG prior to doing TinHatRandom
            // or TinHatURandom, because otherwise, TinHatRandom will create static instances of them, which race, etc.
            // thus throwing off my benchmark results.

            result = new RandResult();
            result.AlgorithmName = "TinHatRandom";
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            TinHatRandom.StaticInstance.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            System.Console.Write("Sleeping to allow pool to fill...");
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            System.Console.WriteLine("  Done.");
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randBytes);

            result = new RandResult();
            result.AlgorithmName = "TinHatURandom";
            System.Console.Write(result.AlgorithmName + " ");
            before = DateTime.Now;
            TinHatURandom.StaticInstance.GetBytes(randBytes);
            after = DateTime.Now;
            result.TimeSpan = after - before;
            result.CompressionRatio = GetCompressionRatio(randBytes);
            results.Add(result);
            System.Console.WriteLine((after - before).ToString());
            System.Console.Write("Sleeping to allow pool to fill...");
            System.Threading.Thread.Sleep(15000);    // Should be enough time for its pool to fill up, so it won't slow down next:
            System.Console.WriteLine("  Done.");
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
