using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsKeyboardInputPrompt
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var myForm = new FormKeyboardInputPrompt(128);  // Request 128 characters, ~128 bits of entropy
            DialogResult result = myForm.ShowDialog();
            if (result == DialogResult.OK)
            {
                string userString = myForm.GetUserString();
                MessageBox.Show("Got string: '" + userString + "'");
                tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(System.Text.Encoding.UTF8.GetBytes(userString));
            }
            else
            {
                MessageBox.Show("No result received");
            }
        }
    }
}
