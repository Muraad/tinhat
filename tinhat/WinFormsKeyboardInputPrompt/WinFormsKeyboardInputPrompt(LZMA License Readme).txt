WinFormsKeyboardInputPrompt does not strictly require LZMA.  It's a nice feature - while the user is inputting text, LZMA recognizes obvious patterns such as asdfasdfasdf and jjjjjjjjjjjjj, and discounts them, so it makes the counter count down slower.  It is still easy to defeat this by entering nonrepeating patterns such as 1234qwerasdfzxcv...  But using LZMA will help reduce the most obvious bad lazy user behaviors.

Without LZMA, the counter simply counts the number of characters entered.  No attempt is made to recognize the user entering rubbish characters.

The version of LZMA linked in this project comes from CompressSharp.
    https://github.com/adamhathcock/sharpcompress
Which is distributed under Ms-PL license
    http://sharpcompress.codeplex.com/license
