﻿using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Prng;
using System.Threading;

namespace tinhat.EntropySources
{
    /// <summary>
    /// A simple wrapper around BouncyCastle ThreadedSeedGenerator. Starts a thread in a tight increment loop,
    /// while another thread samples the variable being incremented.  Entropy is generated by the OS thread
    /// scheduler, not knowing how many times the first thread will loop in the period of time the second thread loops once.
    /// It is recommended to use ThreadedSeedGeneratorRNG as one of the entropy sources, but not all by itself,
    /// because thread scheduling is deterministically controlled by your OS, and easily influenced by outsiders.
    /// </summary>
    public sealed class ThreadedSeedGeneratorRNG : RandomNumberGenerator
    {
        /// <summary>
        /// ThreadedSeedGeneratorRNG will always try to fill up to MaxPoolSize bytes available for read
        /// </summary>
        public static int MaxPoolSize { get; private set; }

        private static object fifoStreamLock = new object();
        private static SupportingClasses.FifoStream myFifoStream = new SupportingClasses.FifoStream(Zeroize: true);
        private static Thread mainThread;
        private static AutoResetEvent poolFullARE = new AutoResetEvent(false);
        private static ThreadedSeedGenerator myThreadedSeedGenerator = new ThreadedSeedGenerator();

        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        static ThreadedSeedGeneratorRNG()
        {
            MaxPoolSize = 4096;
            mainThread = new Thread(new ThreadStart(mainThreadLoop));
            mainThread.IsBackground = true;    // Don't prevent application from dying if it wants to.
            mainThread.Start();
        }
        public ThreadedSeedGeneratorRNG()
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
                        if (readCount > 0)
                        {
                            int bytesRead = myFifoStream.Read(buffer, pos, (int)readCount);
                            pos += bytesRead;
                        }
                        if (pos < count)
                        {
                            if (pos < count)
                            {
                                // TODO BUG there is a bug here, if you request large amounts of data, infinite sleep.
                                // TODO BUG also, bad entropy if using mainthread & another thread, perhaps?
                                //Thread.Sleep((count-pos)*8/2);
                                Thread.Sleep(1);
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
        public static byte[] GetAvailableBytes(int MaxLength)
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
            try
            {
                // ThreadedSeedGenerator performs better with large hunks of data, but if we just use MaxPoolSize, then the whole
                // thing gets drained before it starts refilling.  In effect, the pool will drain by one unit of byteCount, before it
                // starts refilling, and likewise, there will be zero bytes available until at least one unit of byteCount becomes
                // available.  So there's a balancing act happening here... Faster throughput versus faster response time...
                // Divide by 8 seems to be a reasonable compromise between the two.
                int byteCount = MaxPoolSize / 8;
                while (true)    // The only time we ever quit is on the terminate signal ... interrupt signal ... whatever.  OS kills my thread.
                {
                    if (myFifoStream.Length < MaxPoolSize)
                    {
                        var newBytes = new byte[byteCount];
                        // By my measurements, estimated entropy returned by ThreadedSeedGenerator is approx 0.6 or 0.7 bits per bit
                        // when fast=false, and 0.5 when fast=true.  Occasionally we see measurements between 0.4 and 0.5. So round this 
                        // down to 0.125, and just generate 8x as much data as you need. And mix it.
                        for (int i = 0; i < 8; i++)
                        {
                            byte[] maskBytes = myThreadedSeedGenerator.GenerateSeed(byteCount, fast: true);
                            for (int j = 0; j < newBytes.Length; j++)
                            {
                                newBytes[j] ^= maskBytes[j];
                            }
                            Array.Clear(maskBytes, 0, maskBytes.Length);
                        }
                        myFifoStream.Write(newBytes, 0, newBytes.Length);
                    }
                    else
                    {
                        poolFullARE.WaitOne();
                    }
                }
            }
            catch
            {
                // If we got disposed while in the middle of doing stuff, we could throw any type of exception, and 
                // I would want to suppress those.
            }
        }
        ~ThreadedSeedGeneratorRNG()
        {
            Dispose(false);
        }
    }
}
