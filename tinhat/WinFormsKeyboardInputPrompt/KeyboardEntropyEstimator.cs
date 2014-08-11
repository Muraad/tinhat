using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SharpCompress.Compressor.LZMA;

namespace WinFormsKeyboardInputPrompt
{
    public static class KeyboardEntropyEstimator
    {
        public static int EstimateBits(string UserString)
        {
            if (UserString == null)
            {
                throw new ArgumentNullException("UserString");
            }
            if (UserString.Length == 0)
            {
                return 0;
            }
            if (UserString.Length == 1)
            {
                return 1;
            }
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
            int Length = UserString.Length;
            double lowerBound;
            double upperBound;

            // Set lowerBound
            if (Length < 275)
                lowerBound = 6.985 + 0.007326 * Length;
            else if (Length<565)
                lowerBound = 7.103 + 0.00689655 * Length;
            else if (Length<1385)
                lowerBound = 7.555 + 0.00609756 * Length;
            else if (Length<2203)
                lowerBound = 9.227 + 0.00488998 * Length;
            else if (Length<3023)
                lowerBound = 9.254 + 0.00487805 * Length;
            else if (Length<4097)
                lowerBound = 12.741 + 0.00372439 * Length;
            else if (Length<5189)
                lowerBound = 12.993 + 0.003663 * Length;
            else if (Length<6026)
                lowerBound = 13.401 + 0.00358423 * Length;
            else
                lowerBound = 17.341 + 0.0029304 * Length;

            // Set upperBound
            if (Length<9)
                upperBound = 4.875 + 1.125 * Length;
            else if (Length<38)
                upperBound = 5.068965512 + 1.103448276 * Length;
            else if (Length<50)
                upperBound = 5.8333333 + 1.08333333 * Length;
            else if (Length<64)
                upperBound = 6.428571429 + 1.071428571 * Length;
            else if (Length<96)
                upperBound = 7 + 1.0625 * Length;
            else if (Length<143)
                upperBound = 8.914893617 + 1.042553191 * Length;
            else if (Length<169)
                upperBound = 9.5 + 1.038461538 * Length;
            else if (Length<204)
                upperBound = 11.17142857 + 1.028571429 * Length;
            else if (Length<402)
                upperBound = 11.84848485 + 1.025252525 * Length;
            else
                upperBound = 16.94700013 + 1.012569651 * Length;

            double compressionRatio;
            using (var outStream = new MemoryStream())
            {
                using (var lzmaStream = new LzmaStream(new LzmaEncoderProperties(), false, outStream))
                {
                    byte[] userStringBytes = Encoding.UTF8.GetBytes(UserString);
                    using (var inStream = new MemoryStream(userStringBytes))
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

            double estimatedGoodCharsCount = UserString.Length * compressionRatio;

            var alphabet = new HashSet<char>();
            foreach (char c in UserString.ToCharArray())
            {
                // Don't count upper and lower as distinct, because assume they're not randomly toggling shift on each keystroke
                // In other words, the case of the next character is assumed to invariably match the case of the previous character
                alphabet.Add(char.ToLower(c));
            }

            // If your alphabet is 8 characters, then the maximum number of bits per character is 3
            double bitsPerChar = Math.Log(alphabet.Count, 2);

            // We're assuming the user does a bad job of random selection, so discount bitsPerChar by a factor of 4.
            // This is an arbitrary decision - If they managed to hit a dictionary of 20 characters, then they get 1.08 bits
            // per estimatedGoodChar
            bitsPerChar /= 4;

            return (int)(estimatedGoodCharsCount * bitsPerChar);
        }
    }
}
