﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TidyStorage
{
    public partial class StorageImportMenu : Form
    {
        public bool AllowedToClose;

        /// <summary>
        /// Constructor
        /// </summary>
        public StorageImportMenu()
        {
            InitializeComponent();
            this.AllowedToClose = false;
        }

        /// <summary>
        /// Form close event handler. Close have to enabled by the app.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StorageImportMenu_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (AllowedToClose == false)
            {
                e.Cancel = true;
            }
        }
    }
}
