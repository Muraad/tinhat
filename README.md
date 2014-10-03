## tinhat ##

tinhat : crypto random for the truly paranoid

## why tinhat? ##

tinhat guarantees not to reduce the strength of your crypto random.  See below for more details on that.  But if your OS crypto random has in any way been undermined (for example, by a nefarious government agency, or simple incompetence) then tinhat has the ability to improve the strength of your crypto random.

## What is tinhat? ##

Tinhat Random distrustfully takes a bunch of supposed random sources, and mixes them in such a way to produce output which is no less random than the cumulative randomness of *all* the combined inputs, and yet any input being non-random or even actively malicious cannot undermine the cryptographic strength of the Tinhat Random output.

Tinhat Random provides two main classes.  The first one, "TinHatRandom" draws entropy from available entropy sources, and mixes them together as described below, to eliminate or reduce the risk of any individual entropy source failing (being predictable or even controlled by an adversary.)  TinHatRandom will never return more bytes than what it collected from entropy sources, which means it can be slow (typically a few hundred bytes per second).  The second main class, "TinHatURandom" uses a PRNG, which is seeded and regularly reseeded by TinHatRandom.  As long as no weaknesses exist in the underlying PRNG, the output of TinHatURandom is pure random just as the output of TinHatRandom, yet TinHatURandom is very fast - typically generating several MB per second.

## Architecture in Detail ##

You want to know how tinhat can guarantee it will only improve the quality of crypto random.  Here is a detailed description of the architecture behind tinhat random.

Optionally, in addition to the text below, you may refer to the [TinHat Random.pptx](https://github.com/rahvee/tinhat/blob/master/Documentation/TinHat%20Random.pptx?raw=true) presentation.  Just skip slides 4-11 describing bad random in the wild.  The text below tends to explain more detail, while the presentation tends to make it graphically simpler to understand quickly.

Let's study the one-time pad.  The one-time pad (OTP) involves two parties meeting and generating a random sequence together.  This is a shared secret key, equal to or longer than the length of message.  Later, the message sender mixes character-by-character, the plaintext message with the random key, to generate the ciphertext and send the ciphertext.  The recipient is able to remove the random key to discover the plaintext.  This task cannot be performed without knowledge of the shared secret key.  When used correctly, the OTP provides perfect secrecy, because the ciphertext is indistinguishable from true random.  The really important characteristic here is:  *Even if the plaintext is a very predictable pattern, such as all repeating characters, or a pseudo-random pattern generated by a hostile government to undermine your security, when you mix that plaintext with a true random key, the output is indistinguishable from true random.*

I am specifically vague about "mixing" the plaintext with the key, because traditionally, OTP users would use add-without-carry (XOR), but that is not a requirement.  OTP users must use some reversible method (such as XOR) to mix the plaintext with the key, because they are interested in preserving the plaintext when the key is removed.  But if you have no interest in preserving the plaintext (tinhat is only interested in guaranteeing strong crypto random output) you could, for example, take a one-way hash of the plaintext, and XOR it with a hash of the key.  The output would still be indistinguishable from true random, but it would be impossible to determine anything about the original plaintext or random key (unless there is a fundamental flaw in your hash algorithm.)

Let's hypothetically suppose a nefarious government agency (or simple incompetence) has undermined your OS crypto random provider.  To you, the output looks random.  You have been told this output is cryptographically strong random.  But the hypothetical nefarious agency (or just some kid on the internet) has some secret way of guessing the supposedly random values generated on your system.  Let's suppose you have some additional entropy sources - maybe user mouse movements, user keyboard entry, timings of hard drive events, process or thread scheduling contention events, or some other hardware entropy sources...  But you're not completely sure that *any* of them can be trusted as a source of cryptographic random.  What you can do to improve your security is to pull supposedly random data from all these sources, mix them all together, and as long as *any* of them is effectively random (as a key to OTP), then your output is effectively random (as a OTP ciphertext).

The only way to undermine this technique, is for one supposed entropy source to actively predict the output of another supposed entropy source, and try to (or accidentally) interfere with the other, or perform some sort of logging, recording the other for post-analysis.  Checking for the existence of trojans and/or entropy logging is beyond the scope of this project, although, there exist sources on the internet (reputable?) that state certain OSes record and archive all keys generated by the OS.  This is an exercise left for the reader.  A very likely use for tinhat is to feed random data into a non-OS key generator, such as BouncyCastle, which is open source and surely does not record either the random data or the keys generated.  The suspicion remains at the OS level - Is it possible for the OS to sample and record the random data it produces, and also the entropy sources that are used by tinhat to generate random data for an alternate key generator?  Of course it is, but the level of mal-intent and targeted focus are much greater to do this attack, than to come up with some plausible excuse to record all the keys generated by the OS.  So the prospect of OS recording all keys seems much more plausible, and much more deniable by the OS manufacturer, than the prospect of the OS recording all your random data and entropy sources.  For example, the OS manufacturer could say, "We need to record and archive all the keys in order to ensure they're never repeated."  It would sound plausible and reasonable to a lot of people who don't know any better, and very likely get the OS manufacturer out of legal trouble or other trouble, despite being obviously wrong and irresponsible in the eyes of any responsible cryptanalyst.

Getting back to the subject at hand, we are willfully neglecting the possibility of the OS actively recording your entropy sources for post-analysis.  The remaining way to undermine our technique of mixing random sources to produce a more secure random source, is for some supposedly random source to maliciously or accidentally be identical or related to another.  We analyze as follows:

Assume two supposedly crypto random sources actually are related to each other in some way.  Call them Source A and Source B.  For example, suppose Source B extracts entropy from mouse movements, and suppose Source A knows its output will be XOR'd with the output of Source B, and Source A also watches the mouse movements, intentionally trying to generate output that will undermine the cryptographic strength of Source B when the two outputs are mixed.  Source A has precisely three strategies it can choose from:  It can generate the same output as B, generate different but related output, or completely unrelated.  We don't have to consider the case of unrelated output - as it cannot undermine the integrity of Source B's output.  We can trivially detect and correct the case of identical output:  Just compare the outputs, and if they're equal, discard one of them.  So the only non-trivial case is the case of different but related, which we explore in more detail as follows:

The design requirements of a cryptographically secure hash function requires that its output be indistinguishable from random, and that any two different (but possibly related) inputs produce outputs that have no known relation to each other or the inputs.  Any violation of these requirements would result in a "distinguishing attack" against the hash function, and as such, the hash function would lose its strength of "collision resistance," and would henceforth be considered broken, or insecure.

Let's take a hash of A, and call the result A'.  Unless there is a fundamental flaw in the hash function, the relationship here is that A' is apparently random, revealing nothing about A.  Similarly, let B be possibly related, but different from A.  Take a hash, with possibly a different hash function, of B as B'.  This is yet again, apparently random, revealing nothing about B.  If we may assume the hash functions remain cryptographically sound, it is then impossible to manipulate A, different from B, such that A' will have any known relation to B'. 

We are necessarily placing a lot of trust into the hash function, in the sense that no distinguishing attacks are known, and the cryptography community still considers the hash function to be "unbroken."  The moderately paranoid might use a hash function that's publicly considered "secure," such as one of the SHA family.  The extremely paranoid might suspect somebody out there has secret knowledge of a flaw in the SHA family.  To address this concern, let us construct a hash function which is just a wrapper around other hash functions.  Even if a weakness is known for some of the internal hash functions, the attacker still cannot manipulate the input in any way to have a controllable effect on the output, as long as *any* of the internal hash functions remains unbroken.

Proceduralizing all of the above:

* Let there be an arbitrary number of supposedly crypto random sources, or entropy sources, named A, B, C, ...  Let us distrust their entropic integrity or usefulness as cryptographic entropy sources.
* Choose a secure crypto hash function, or a suite of hash functions, with output size m.
* When a user requests n bytes of random data, repeat this process for every m bytes requested:
    * Read m bytes from each of the sources A, B, C, ...
    * Compare each of them against each other for equality, and eliminate any duplicates.
    * Generate hashes A', B', C', ...  The hash functions used for each may be different from each other, and/or may be chained or mixed, provided they are all considered unbroken cryptographic hash functions, with the same output size m.
    * Combine the hashes A', B', C', ... via XOR, and return the result to the user, up to the number of bytes requested.

This is the architecture behind TinHatRandom.  By comparison, TinHatURandom is a simple wrapper around a PRNG, which uses TinHatRandom for seed material.

## Download (Tinhat Random Core) ##

It is generally recommended to use NuGet to add the library to your project/solution.

- Visual Studio: In your project, right-click References, and Manage NuGet Packages. Search for TinHat, and install.
- Xamarin Studio / MonoDevelop: If you don't already have a NuGet package manager, install it from <https://github.com/mrward/monodevelop-nuget-addin>.  And then right-click References, and Manage NuGet Packages.  Search for TinHat, and install.

Tinhat source code is available at <https://github.com/rahvee/tinhat>

## Download (Tinhat Random Extras) ##

* WindowsFormsMouse provides an easy to use entropy gathering interface, to collect randomness from user mouse movements in windows.
* WinFormsKeyboardInputPrompt (With LZMA) prompts for user keyboard random input.  LZMA is used to attempt removing repeated patterns such as asdfasdfasdf or jjjjjjj, but LZMA requires acceptance of [the CompressSharp MS-Pl license](http://sharpcompress.codeplex.com/license).
* WinFormsKeyboardInputPrompt (Without LZMA) is exactly the same as above, but blindly assumes 1 bit of entropy per character entered by the user.

If you use these projects, it is assumed you'll want to update icons or customize slightly, so they are not distributed in binary form.  Please checkout the Tinhat source code at <https://github.com/rahvee/tinhat>.

We will be adding more soon (WPF, Mac OSX, GTK (linux)).  Right now, it's only winforms.

## License ##

Tinhat Random is distributed under the MIT license.  Details here:  <https://raw.githubusercontent.com/rahvee/tinhat/master/LICENSE>

## Documentation and API ##

### Absolute Simplest Possible Usage ###

This is an example of the absolute simplest possible usage.  It gets random bytes from the OS RNGCryptoServiceProvider, ThreadSchedulerRNG, and ThreadedSeedGeneratorRNG.  So it *might* be better than the OS RNGCryptoServiceProvider all by itself.  For stronger usage, see below to include EntropyFileRNG.

    using tinhat;
    
    static void Main(string[] args)
    {
        /* Please note: On first call, there may be a delay to gather
         * enough entropy to satisfy the request.  It's recommended to
         * use StartEarly as early as possible, as seen in Simple Example #2.
         */
    
        /* Only use TinHatRandom for keys and other things that don't require
         * a large number of bytes quickly.  Use TinHatURandom for everything else.
         * Performance is highly variable.  On my system 
         * TinHatRandom generated 497Bytes(minimum)/567KB(avg)/1.7MB(max) per second
         * TinHatURandom generated 2.00MB(minimum)/3.04MB(avg)/3.91MB(max) per second
         * default constructors use:
         *     SystemRNGCryptoServiceProvider/SHA256, 
         *     ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest,
         *     ThreadSchedulerRNG/SHA256,
         *     (if available) EntropyFileRNG/SHA256
         * and TinHatURandom by default uses Sha256Digest as the basis 
         * for DigestRandomGenerator PRNG
         */
        var randomBytes = new byte[32];
        TinHatRandom.StaticInstance.GetBytes(randomBytes);
        // or use TinHatURandom:
        TinHatURandom.StaticInstance.GetBytes(randomBytes);
    }

### Simple Example #2 ###

It is recommended to use StartEarly as soon as possible in your application, as follows:

    using tinhat;
    
    static void Main(string[] args)
    {
        StartEarly.StartFillingEntropyPools();  // Start gathering entropy as early as possible

        /* Only use TinHatRandom for keys and other things that don't require
         * a large number of bytes quickly.  Use TinHatURandom for everything else.
         * Performance is highly variable.  On my system 
         * TinHatRandom generated 497Bytes(minimum)/567KB(avg)/1.7MB(max) per second
         * TinHatURandom generated 2.00MB(minimum)/3.04MB(avg)/3.91MB(max) per second
         * default constructors use:
         *     SystemRNGCryptoServiceProvider/SHA256, 
         *     ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest,
         *     ThreadSchedulerRNG/SHA256,
         *     (if available) EntropyFileRNG/SHA256
         * and TinHatURandom by default uses Sha256Digest as the basis 
         * for DigestRandomGenerator PRNG
         */
        var randomBytes = new byte[32];
        TinHatRandom.StaticInstance.GetBytes(randomBytes);
        // or use TinHatURandom:
        TinHatURandom.StaticInstance.GetBytes(randomBytes);
    }


### Stronger Usage (Recommended) ###

This is a stronger usage model.  Most likely, this is the easiest, best, strongest, last word you'll ever need in strong crypto RNG. At least until the world changes, and this stuff gets updated.  ;-)  This is the recommended usage:

* Using any combination of WindowsFormsMouse, WinFormsKeyboardInputPrompt, and other entropy sources such as [List of Random Number Servers](http://en.wikipedia.org/wiki/List_of_random_number_generators#Random_Number_Servers), add seed bytes to EntropyFileRNG via `tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randomBytes)`
* It is recommended to add at least 32 bytes from each available entropy source, using as many different entropy sources as available.
* It doesn't matter if these seed bytes are dense high quality entropy.  Some non-random bytes mixed in there won't hurt anything, as long as the total entropy is sufficient for your purposes.  So for example, suppose the user was uncooperative and just held down a single key, repeated the letter "j" 256 times, that obviously provides essentially zero entropy from the user keyboard prompt, but as long as they actually moved their mouse around and didn't use a robot to eliminate mouse entropy, or as long as you collected random bytes from the internet  that were truly random and not compromised in any way, then you're going to have a good result as long as you got enough entropy from those other sources.

Now that you've seeded the EntropyFileRNG once, in the future you can follow either of the "Simple Examples" above.  The mere existence of the EntropyFile will cause TinHatRandom and TinHatURandom to use it.  Your call to AddSeedMaterial() causes the new seed material to become available immediately in the TinHatRandom.StaticInstance and TinHatURandom.StaticInstance.

You may add seed material as often as you like.  The reseed event immediately propagates to all TinHatRandom and TinHatURandom instances, causing them to reseed themselves, so there is a slight CPU penalty, and each reseed takes perhaps 100ms of disk time, but aside from that, reseeding often is probably a good thing.  You can reseed using bytes obtained from TinHatRandom, or using entropy that you gather from any other source.

### Advanced Usage ###

If you want to manually specify the hash algorithms and entropy sources, please consult the documentation below.

For windows users, we recommend downloading the chm file (compressed html, displays natively in your windows help dialog by just double-clicking the chm file).  <https://github.com/rahvee/tinhat/raw/master/Documentation/tinhat.chm>

The html when viewed in a web browser, doesn't render quite as nicely, but here it is, for anyone who doesn't want or can't use the chm.  <https://www.tinhatrandom.org/API>

### RNG Comparisons ###

In the TinHat Random source code, there is a Test project, which benchmarks and statistically analyzes several RNG algorithms.  Below are some results from a sample run.

Highlights include:

* The OS crypto API is obviously the fastest, and very strong, except perhaps if the OS or hardware manufacturers have backdoor'd it.  So it should always be used, but not exclusively.
* The bouncy castle ThreadedSeedGenerator output is not very random.
* The TinHat ThreadedSeedGeneratorRNG is a wrapper around bouncy castle ThreadedSeedGenerator, which collects 8x more data than necessary, and mixes it all together, to create hopefully one good output random stream.
* TinHat ThreadSchedulerRNG seems to produce good entropy, but rather slowly.
* For nearly all purposes, it is recommended to seed EntropyFileRNG as described above, and then use TinHatURandom, because it strongly mixes all the other entropy sources together, produces good solid random output, and performs well.

Sample Performance (Windows 8.1, Intel Core i5)

    |                    AlgorithmName | bits per bit | elapsed sec | effective rate|
    |----------------------------------|--------------|-------------|---------------|
    |                    TinHatURandom |        0.999 |       0.007 |   1.15 MiB/sec|
    |                     TinHatRandom |        0.999 |      67.315 |   121.61 B/sec|
    |   SystemRNGCryptoServiceProvider |        0.999 |       0.000 |       infinity|
    |         RNGCryptoServiceProvider |        0.999 |       0.000 |       infinity|
    |         ThreadedSeedGeneratorRNG |        0.939 |       2.893 |   2.66 KiB/sec|
    |      ThreadedSeedGenerator(fast) |        0.421 |       0.021 | 161.17 KiB/sec|
    |      ThreadedSeedGenerator(slow) |        0.408 |       0.005 | 708.79 KiB/sec|
    |               ThreadSchedulerRNG |        0.999 |     127.735 |    64.09 B/sec|
    |                    ticks bit # 0 |        0.994 |     133.637 |    60.91 B/sec|
    |                    ticks bit # 1 |        0.989 |     133.637 |    60.60 B/sec|
    |                    ticks bit # 2 |        0.999 |     133.637 |    61.23 B/sec|
    |                    ticks bit # 3 |        0.999 |     133.637 |    61.24 B/sec|
    |                    ticks bit # 4 |        0.999 |     133.637 |    61.23 B/sec|
    |                    ticks bit # 5 |        0.999 |     133.637 |    61.22 B/sec|
    |                    ticks bit # 6 |        0.998 |     133.637 |    61.18 B/sec|
    |                    ticks bit # 7 |        0.997 |     133.637 |    61.09 B/sec|
    |                    ticks bit # 8 |        0.987 |     133.637 |    60.52 B/sec|
    |                    ticks bit # 9 |        0.952 |     133.637 |    58.36 B/sec|
    |                    ticks bit #10 |        0.932 |     133.637 |    57.13 B/sec|
    |                    ticks bit #11 |        0.866 |     133.637 |    53.07 B/sec|
    |                    ticks bit #12 |        0.670 |     133.637 |    41.06 B/sec|
    |                    ticks bit #13 |        0.714 |     133.637 |    43.74 B/sec|
    |                   EntropyFileRNG |        0.999 |       0.004 |   1.94 MiB/sec|
    |EntropyFileRNG (RIPEMD256_256bit) |        0.999 |       0.001 |   7.27 MiB/sec|
    |   EntropyFileRNG (SHA256_256bit) |        0.999 |       0.002 |   4.11 MiB/sec|
    |   EntropyFileRNG (SHA512_512bit) |        0.999 |       0.002 |   4.03 MiB/sec|
    |EntropyFileRNG (Whirlpool_512bit) |        1.000 |       0.021 | 388.80 KiB/sec|
    |                         AllZeros |        0.000 |       0.001 |     0.00 B/sec|

Sample Performance (Max OSX 10.9.4 Mavericks, Intel Core i5)

    |                    AlgorithmName : bits per bit : elapsed sec : effective rate|
    |----------------------------------|--------------|-------------|---------------|
    |                    TinHatURandom :        0.999 :       0.006 :   1.47 MiB/sec|
    |                     TinHatRandom :        1.000 :      36.799 :   222.61 B/sec|
    |   SystemRNGCryptoServiceProvider :        0.999 :       0.002 :   4.89 MiB/sec|
    |         RNGCryptoServiceProvider :        1.000 :       0.001 :   9.08 MiB/sec|
    |         ThreadedSeedGeneratorRNG :        0.984 :       2.047 :   3.94 KiB/sec|
    |      ThreadedSeedGenerator(fast) :        0.130 :       0.001 : 725.12 KiB/sec|
    |      ThreadedSeedGenerator(slow) :        0.979 :       0.004 :   2.22 MiB/sec|
    |               ThreadSchedulerRNG :        1.000 :      73.829 :   110.92 B/sec|
    |                    ticks bit # 0 :        0.000 :      73.364 :     0.00 B/sec|
    |                    ticks bit # 1 :        0.999 :      73.364 :   111.55 B/sec|
    |                    ticks bit # 2 :        0.999 :      73.364 :   111.54 B/sec|
    |                    ticks bit # 3 :        0.999 :      73.364 :   111.59 B/sec|
    |                    ticks bit # 4 :        1.000 :      73.364 :   111.65 B/sec|
    |                    ticks bit # 5 :        0.999 :      73.364 :   111.58 B/sec|
    |                    ticks bit # 6 :        1.000 :      73.364 :   111.62 B/sec|
    |                    ticks bit # 7 :        0.999 :      73.364 :   111.55 B/sec|
    |                    ticks bit # 8 :        0.994 :      73.364 :   110.96 B/sec|
    |                    ticks bit # 9 :        0.995 :      73.364 :   111.15 B/sec|
    |                    ticks bit #10 :        0.972 :      73.364 :   108.54 B/sec|
    |                    ticks bit #11 :        0.912 :      73.364 :   101.89 B/sec|
    |                    ticks bit #12 :        0.703 :      73.364 :    78.55 B/sec|
    |                    ticks bit #13 :        0.482 :      73.364 :    53.82 B/sec|
    |                   EntropyFileRNG :        0.999 :       0.003 :   2.64 MiB/sec|
    |EntropyFileRNG (RIPEMD256_256bit) :        0.999 :       0.001 :   9.49 MiB/sec|
    |   EntropyFileRNG (SHA256_256bit) :        0.999 :       0.002 :   5.25 MiB/sec|
    |   EntropyFileRNG (SHA512_512bit) :        0.999 :       0.003 :   3.12 MiB/sec|
    |EntropyFileRNG (Whirlpool_512bit) :        0.999 :       0.009 : 943.64 KiB/sec|
    |                         AllZeros :        0.000 :       0.024 :     0.00 B/sec|

## Support ##

Please send email to <tinhatrandom-discuss@tinhatrandom.org>
