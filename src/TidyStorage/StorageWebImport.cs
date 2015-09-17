﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using TidyStorage.Suppliers;
using TidyStorage.Suppliers.Data;

namespace TidyStorage
{
    public partial class StorageWebImport : Form
    {
        Supplier supplier;
        LoadingForm loadingForm;
        Task downloadTask;
        public SupplierPart supplierPart;

        public int FoundPartType;
        Storage storage;

        List<PartRow> unusedPartRows = new List<PartRow>();
        List<PartRow> downloadedPartRows = new List<PartRow>();
        PartRow nullItem;

        public string PrimaryValueUnit = "";
        public string PrimaryValueTolernceUnit = "%";
        public string SecondaryValueUnit = "";
        public string ThirdValueUnit = "";

        public StorageWebImport(Storage storage, Supplier supplier)
        {
            this.storage = storage;
            this.supplier = supplier;

            nullItem = new PartRow("Do no update");

            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOk_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StoragePartImporter_Load(object sender, EventArgs e)
        {
            if (supplier == null)
            {
                this.Close();
                return;
            }

            loadingForm = new LoadingForm();
            downloadTask = new Task(new Action(DownloadWorker));
            downloadTask.Start();
            loadingForm.ShowDialog();
        }


        /// <summary>
        /// 
        /// </summary>
        private void DownloadWorker()
        {
            int timeout = 0;
            bool DownloadDone = false;

            Thread.Sleep(250);

            loadingForm.UpdateProgress(0);

            loadingForm.UpdateLabel("Downloading part " + supplier.PartNumber + " from " + this.supplier.Name);

            supplierPart = supplier.DownloadPart();

            loadingForm.UpdateProgress(25);
            loadingForm.UpdateLabel("Saving and processing");

            downloadedPartRows.AddRange(supplierPart.rows);

            loadingForm.UpdateProgress(50);
            loadingForm.UpdateLabel("Loading into form");

            this.Invoke(new Action(UpdateComboBoxes));

            Thread.Sleep(500);

            loadingForm.UpdateProgress(100);
            loadingForm.UpdateLabel("Done");

            loadingForm.AllowedToClose = true;
            loadingForm.Invoke(new Action(loadingForm.Close));
        }

   
        


        /// <summary>
        /// 
        /// </summary>
        private void UpdateComboBoxes()
        {
            unusedPartRows.Clear();
            unusedPartRows.AddRange(downloadedPartRows);

            foreach (Control x in this.tableLayoutPanel1.Controls)
            {
                if (x.GetType() == typeof(ComboBox))
                {
                    ComboBox cb = (ComboBox)x;
                    cb.Items.Clear();
                    cb.Items.Add(nullItem);
                    cb.Items.AddRange(unusedPartRows.ToArray());
                    cb.SelectedIndex = 0;
                }
            }

            if (supplier.GetType() == typeof(FarnellSupplier))
            {
                comboBox1.SelectedItem = unusedPartRows[2] ?? nullItem;
                comboBox2.SelectedItem = unusedPartRows[0] ?? nullItem;
                comboBox3.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Name.Contains("Pouzdr"))) ?? nullItem;
                comboBox4.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Name == "Datasheet")) ?? nullItem;
            }



            comboBox12.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.Contains("°C") && (x.Value.Contains("ppm") == false) && x.Value[0] == '-')) ?? nullItem;
            comboBox13.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.Contains("°C") && (x.Value.Contains("ppm") == false) && x.Value[0] != '-')) ?? nullItem;



            FoundPartType = -1;

            string[] s = new string[3];
            
            if ((PrimaryValueUnit == "") &&
                (SecondaryValueUnit == "") &&
                (ThirdValueUnit == ""))
            {
                DataTable dt = storage.GetTable(StorageConst.Str_PartType, StorageConst.Str_PartType_id + ",primary_valuename,secondary_valuename,tertiary_valuename");

                if (dt != null)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        var pvu = (dr.ItemArray[1] is string) ? (string)dr.ItemArray[1] : "";
                        var svu = (dr.ItemArray[2] is string) ? (string)dr.ItemArray[2] : "";
                        var tvu = (dr.ItemArray[3] is string) ? (string)dr.ItemArray[3] : "";

                        pvu = StringHelpers.Between(pvu, "[", "]");
                        svu = StringHelpers.Between(svu, "[", "]");
                        tvu = StringHelpers.Between(tvu, "[", "]");

                        if ((pvu != "") && (svu != ""))
                        {
                            var pvuo = unusedPartRows.FirstOrDefault(x => (x.Value.EndsWith(pvu)));
                            var svuo = unusedPartRows.FirstOrDefault(x => (x.Value.EndsWith(svu)));

                            if ((pvuo != null) && (svuo != null))
                            {
                                PrimaryValueUnit = pvu;
                                SecondaryValueUnit = svu;

                                var tvuo = unusedPartRows.FirstOrDefault(x => (x.Value.Contains(tvu)));

                                if (tvuo != null) ThirdValueUnit = tvu;

                                FoundPartType = (int)(Int64)dr.ItemArray[0];
                                break;
                            }
                        }
                    }
                }
            }



            if (PrimaryValueUnit != "") comboBox6.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.EndsWith(PrimaryValueUnit))) ?? nullItem;
            if (PrimaryValueTolernceUnit != "") comboBox7.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.EndsWith(PrimaryValueTolernceUnit))) ?? nullItem;
            if (SecondaryValueUnit != "") comboBox8.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.EndsWith(SecondaryValueUnit))) ?? nullItem;
            if (ThirdValueUnit != "") comboBox10.SelectedItem = unusedPartRows.FirstOrDefault(x => (x.Value.Contains(ThirdValueUnit))) ?? nullItem;



        }


    }
}