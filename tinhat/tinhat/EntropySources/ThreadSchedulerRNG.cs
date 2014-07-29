using System;
using System.Security.Cryptography;
using System.Threading;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Engines;

namespace tinhat.EntropySources
{
    /// <summary>
    /// In a multitasking OS, each individual thread never knows when it's going to be granted execution time,
    /// as many processes and threads compete for CPU cycles.  The granularity of time to wake up from sleep is
    /// something like +/- a few ms, while the granularity of DateTime.Now is Ticks, 10million per second.  Although
    /// the OS scheduler is surely deterministic, there should be a fair amount of entropy in the least significant
    /// bit of DateTime.Now.Ticks upon thread waking.  But since the OS scheduler is surely deterministic, it is
    /// not recommended to use ThreadSchedulerRNG as your only entropy source.  It is recommended to use this
    /// class ONLY in addition to other entropy sources.
    /// </summary>
    public sealed class ThreadSchedulerRNG : RandomNumberGenerator
    {
        /// <summary>
        /// ThreadSchedulerRNG will always try to fill up to MaxPoolSize bytes available for read
        /// </summary>
        public static int MaxPoolSize { get; private set; }

        /// <summary>
        /// Internal testing only. Do not change. Used only during unit tests, to ensure entropy measurements are measurable
        /// </summary>
        public static bool UseMixingFunction = true;
        /// <summary>
        /// Internal testing only. Do not change. Used only during unit tests, to ensure entropy measurements are measurable
        /// </summary>
        public static int UseBitPosition;

        private static object fifoStreamLock = new object();
        private static SupportingClasses.FifoStream myFifoStream = new SupportingClasses.FifoStream(Zeroize: true);
        private static Thread mainThread;
        private static AutoResetEvent poolFullARE = new AutoResetEvent(false);

        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        private const int chunkSize = 16;
        private static byte[] chunk;
        private static int chunkByteIndex = 0;
        private static int chunkBitIndex = 0;

        static ThreadSchedulerRNG()
        {
            chunk = new byte[chunkSize];
            MaxPoolSize = 4096;
            mainThread = new Thread(new ThreadStart(mainThreadLoop));
            mainThread.IsBackground = true;    // Don't prevent application from dying if it wants to.
            mainThread.Start();
        }
        public ThreadSchedulerRNG()
        {
        }
        private static int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                int pos = offset;
                lock (fifoStreamLock)
                {
                    while (pos < count)
                    {
                        long readCount = myFifoStream.Length;   // All the available bytes
                        if (pos + readCount >= count)
                        {
                            readCount = count - pos;    // Don't try to read more than we need
                        }
                        int bytesRead = -1;
                        while (readCount > 0 && bytesRead != 0)
                        {
                            bytesRead = myFifoStream.Read(buffer, pos, (int)readCount);
                            readCount -= bytesRead;
                            pos += bytesRead;
                        }
                        if (pos < count)
                        {
                            if (pos < count)
                            {
                                // Expect 16 bytes every 32ms because 4 bits per sample, one sample every ms, and chunk in blocks of 16
                                Thread.Sleep(32);
                            }
                        }
                    }
                    return count;
                }
            }
            finally
            {
                poolFullARE.Set();
            }
        }
        public byte[] GetAvailableBytes(int MaxLength)
        {
            lock (fifoStreamLock)
            {
                long availBytesCount = myFifoStream.Length;
                byte[] allBytes;
                if (availBytesCount > MaxLength)
                {
                    allBytes = new byte[MaxLength];
                }
                else // availBytesCount could be 0, or greater
                {
                    allBytes = new byte[availBytesCount];
                }
                if (availBytesCount > 0)
                {
                    Read(allBytes, 0, allBytes.Length);
                }
                return allBytes;
            }
        }
        public override void GetBytes(byte[] data)
        {
            if (Read(data,0,data.Length) != data.Length)
            {
                throw new CryptographicException("Failed to return requested number of bytes");
            }
        }
        public override void GetNonZeroBytes(byte[] data)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                var newBytes = new byte[data.Length - offset];
                if (Read(newBytes,0,newBytes.Length) != newBytes.Length)
                {
                    throw new CryptographicException("Failed to return requested number of bytes");
                }
                for (int i=0; i<newBytes.Length; i++)
                {
                    if(newBytes[i] != 0)
                    {
                        data[offset] = newBytes[i];
                        offset++;
                    }
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposed,TrueInt) == TrueInt)
            {
                return;
            }
            poolFullARE.Set();
            poolFullARE.Dispose();
            myFifoStream.Dispose();
            base.Dispose(disposing);
        }
        private static void mainThreadLoop()
        {
            /* We are only using AES for mixing - meaning - We have an estimated 14 bits of entropy collectively scattered
             * over the least significant 31 bits of Ticks.  We don't trust it very much, so we're willing to extract only
             * 4 bits.  So how do you mix those 31 bits together in such a way as to extract 4 bits out of it? We're using
             * Aes in ECB mode.  Input 64 bits of 0's with 64 bits from Ticks. Output should be essentially random, and we
             * extract only 4 bits from it.
             */
            var aes = new AesFastEngine();
            var keyParam = new KeyParameter(new byte[16]);  // Because I'm only using AES for mixing, I literally don't care about the key.
            aes.Init(forEncryption: true, parameters: keyParam);
            int aesBlockSize = aes.GetBlockSize();     // 16

            byte[] inbuf = new byte[aesBlockSize];
            byte[] outbuf = new byte[aesBlockSize];

            while (true)
            {
                if (myFifoStream.Length < MaxPoolSize)
                {
                    // While running in this tight loop, consumes approx 0% cpu time.  Cannot even measure with Task Manager

                    /* With 10m ticks per second, and Thread.Sleep() precision of 1ms, it means Ticks is 10,000 times more precise than
                     * the sleep wakeup timer.  This means there could exist as much as 14 bits of entropy in every thread wakeup cycle,
                     * but realistically that's completely unrealistic.  I ran this 64*1024 times, and benchmarked each bit individually.
                     * The estimated entropy bits per bit of Ticks sample is very near 1 bit for each of the first 8 bits, and quickly
                     * deteriorates after that.
                     * 
                     * Surprisingly, the LSB #0 and LSB #1 demonstrated the *least* entropy within the first 8 bits, but it was still
                     * 0.987 bits per bit, which could be within sampling noise.  Bits 9, 10, and beyond very clearly demonstrated a
                     * degradation in terms of entropy quality.
                     * 
                     * The estimated sum total of all entropy in all 64 bits is about 14 bits of entropy per sample, which is just 
                     * coincidentally the same as the difference in precision described above.
                     * 
                     * Based on superstition, I would not attempt to extract anywhere near 14 bits per sample, not even 8 bits. But since 
                     * the first 8 bits all measured to be very close to 1 bit per bit, I am comfortable extracting at least 2 or 4 bits.
                     */

                    long ticks = DateTime.Now.Ticks;

                    if (UseMixingFunction)
                    {
                        const int numGoodBits = 4;  // See comment above. We could get up to 8 or 14 bits, but I'm comfortable with 4.
                        byte[] ticksBytes = BitConverter.GetBytes(ticks);  // Don't care about endianness
                        Array.Copy(ticksBytes, 0, inbuf, 0, ticksBytes.Length);
                        Array.Clear(ticksBytes, 0, ticksBytes.Length);
                        aes.ProcessBlock(inbuf, 0, outbuf, 0);
                        for (int i = 0; i < numGoodBits; i++)
                        {
                            byte newBit = (byte)(outbuf[0] % 2);
                            outbuf[0] >>= 1;
                            GotBit(newBit);
                        }
                    }
                    else
                    {
                        ticks >>= UseBitPosition;
                        byte newBit = (byte)(ticks % 2);
                        GotBit(newBit);
                    }
                    Thread.Sleep(1);
                }
                else
                {
                    poolFullARE.WaitOne();
                }
            }
        }

        private static void GotBit(byte bitByte)
        {
            if (bitByte > 1)
            {
                throw new ArgumentException("bitByte must be equal to 0 or 1");
            }
            chunk[chunkByteIndex] <<= 1;    // << operator discards msb's and zero-fills lsb's, never causes overflow
            if (bitByte == 1)
            {
                chunk[chunkByteIndex]++;    // By incrementing, we are setting the lsb to 1.
            }
            chunkBitIndex++;
            if (chunkBitIndex > 7)
            {
                chunkBitIndex = 0;
                chunkByteIndex++;
                if (chunkByteIndex >= chunkSize)
                {
                    myFifoStream.Write(chunk, 0, chunkSize);
                    chunkByteIndex = 0;
                }
            }
        }
        ~ThreadSchedulerRNG()
        {
            Dispose(false);
        }
    }
}
