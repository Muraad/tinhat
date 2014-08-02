using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinFormsKeyboardInputPrompt
{
    public partial class FormKeyboardInputPrompt : Form
    {
        private int requestedEntropyBitCount;
        /// <summary>
        /// requestedEntropyBitCount less than 20 or 30 is basically insane. Reasonable is 64 to 512 or so.
        /// </summary>
        public FormKeyboardInputPrompt(int numChars)
        {
            if (numChars < 2)   // Of course, anything less than 20 or 30 is basically insane
            {
                throw new ArgumentException("requestedEntropyBitCount");
            }
            this.requestedEntropyBitCount = numChars;
            InitializeComponent();
        }

        public string GetUserString()
        {
            return textBoxUserChars.Text;
        }
        private void UpdateLabelPleaseEnterPrompt()
        {
            string remainingCharsCountString;
#if WITHLZMA
            int estimatedBits = KeyboardEntropyEstimator.EstimateBits(textBoxUserChars.Text);
#else
            int estimatedBits = textBoxUserChars.Text.Length;
#endif
            if (estimatedBits >= requestedEntropyBitCount)
            {
                buttonOK.Enabled = true;
                remainingCharsCountString = "(All Done!)";
            }
            else
            {
                buttonOK.Enabled = false;
                remainingCharsCountString = (requestedEntropyBitCount - estimatedBits).ToString();
            }
            this.labelPleaseEnterPrompt.Text = "Please randomly enter at least this many characters: " + remainingCharsCountString;
        }
        private void FormKeyboardInputPrompt_Load(object sender, EventArgs e)
        {
            UpdateLabelPleaseEnterPrompt();
            textBoxUserChars.TextChanged += textBoxUserChars_TextChanged;
        }

        void textBoxUserChars_TextChanged(object sender, EventArgs e)
        {
            UpdateLabelPleaseEnterPrompt();
        }
    }
}
