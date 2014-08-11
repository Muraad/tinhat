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
    /// bits of DateTime.Now.Ticks upon thread waking.  But since the OS scheduler is surely deterministic, it is
    /// not recommended to use ThreadSchedulerRNG as your only entropy source.  It is recommended to use this
    /// class ONLY in addition to other entropy sources.
    /// </summary>
    public sealed class ThreadSchedulerRNG : RandomNumberGenerator
    {
        /// <summary>
        /// By putting the core into its own class, it makes it easy for us to create a single instance of it, referenced 
        /// by a static member of ThreadSchedulerRNG, without any difficulty of finalizing & disposing etc.
        /// </summary>
        private class ThreadSchedulerRNGCore
        {
            private const int MaxPoolSize = 4096;
            private object fifoStreamLock = new object();
            private SupportingClasses.FifoStream myFifoStream = new SupportingClasses.FifoStream(Zeroize: true);
            private Thread mainThread;
            private AutoResetEvent mainThreadLoopARE = new AutoResetEvent(false);
            private AutoResetEvent bytesAvailableARE = new AutoResetEvent(false);

            // Interlocked cannot handle bools.  So using int as if it were bool.
            private const int TrueInt = 1;
            private const int FalseInt = 0;
            private int disposed = FalseInt;

            private const int chunkSize = 16;
            private byte[] chunk;
            private int chunkByteIndex = 0;
            private int chunkBitIndex = 0;
            public ThreadSchedulerRNGCore()
            {
                chunk = new byte[chunkSize];
                mainThread = new Thread(new ThreadStart(mainThreadLoop));
                mainThread.IsBackground = true;    // Don't prevent application from dying if it wants to.
                mainThread.Start();
            }
            public int Read(byte[] buffer, int offset, int count)
            {
                int pos = offset;
                try
                {
                    lock (fifoStreamLock)
                    {
                        while (pos < offset + count)
                        {
                            long readCount = myFifoStream.Length;   // All the available bytes
                            if (pos + readCount >= offset + count)
                            {
                                readCount = offset + count - pos;    // Don't try to read more than we need
                            }
                            int bytesRead = -1;
                            while (readCount > 0 && bytesRead != 0)
                            {
                                bytesRead = myFifoStream.Read(buffer, pos, (int)readCount);
                                mainThreadLoopARE.Set();
                                readCount -= bytesRead;
                                pos += bytesRead;
                            }
                            if (pos < offset + count)
                            {
                                bytesAvailableARE.WaitOne();
                            }
                        }
                        return count;
                    }
                }
                catch
                {
                    if (disposed == TrueInt)
                    {
                        throw new System.IO.IOException("Read() interrupted by Dispose()");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            private void mainThreadLoop()
            {
                try
                {
                    while (this.disposed == FalseInt)
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
                             * 
                             * To be ultra-conservative, I'll extract only a single bit each time, and it will be a mixture of all 64 bits.  Which
                             * means, as long as *any* bit is unknown to an adversary, or the sum total of the adversary's uncertainty over all 64
                             * bits > 50%, then the adversary will have at best 50% chance of guessing the output bit, which means it is 1 bit of
                             * good solid entropy.  In other words, by mashing all ~8-14 bits of entrpoy into a single bit, the resultant bit
                             * should be a really good quality entropy bit.
                             */

                            long ticks = DateTime.Now.Ticks;
                            byte newBit = 0;
                            for (int i = 0; i < 64; i++)    // Mix all 64 bits together to produce a single output bit
                            {
                                newBit ^= (byte)(ticks % 2);
                                ticks >>= 1;
                            }
                            GotBit(newBit);
                            Thread.Sleep(1);
                        }
                        else
                        {
                            mainThreadLoopARE.WaitOne();
                        }
                    }
                }
                catch
                {
                    if (disposed == FalseInt)   // If we caught an exception after being disposed, just swallow it.
                    {
                        throw;
                    }
                }
            }
            private void GotBit(byte bitByte)
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
                        bytesAvailableARE.Set();
                        chunkByteIndex = 0;
                    }
                }
            }
            protected void Dispose(bool disposing)
            {
                if (Interlocked.Exchange(ref disposed,TrueInt) == TrueInt)
                {
                    return;
                }
                mainThreadLoopARE.Set();
                mainThreadLoopARE.Dispose();
                bytesAvailableARE.Set();
                bytesAvailableARE.Dispose();
                myFifoStream.Dispose();
            }
            ~ThreadSchedulerRNGCore()
            {
                Dispose(false);
            }
        }

        private static ThreadSchedulerRNGCore core = new ThreadSchedulerRNGCore();

        public override void GetBytes(byte[] data)
        {
            if (core.Read(data,0,data.Length) != data.Length)
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
                if (core.Read(newBytes,0,newBytes.Length) != newBytes.Length)
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
            base.Dispose(disposing);
        }
        ~ThreadSchedulerRNG()
        {
            Dispose(false);
        }
    }
}
