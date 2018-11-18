using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;

namespace DuplicateFileScaner
{
    public partial class FrmFileScaner : Form
    {
        private readonly BackgroundWorker _bgOutputWorker;
        
        private List<DataStuctInfo> _scanList;
        private List<Thread> _hashThreadList;
        private ThreadControl _threadExecState;        
        private Thread _thHashMonitor;

        private string _outputFile;
        private bool _scanStatusCanceled;
        private int _totalFilesToScan;
        private int _duplicateFilesFound;
        private DialogResult result = DialogResult.Cancel;
        //Decalare thread-safe collection to maintain duplicates.
        public static BlockingCollection<OutputDataStruct> OutputList;
        

        public FrmFileScaner()
        {
            InitializeComponent();

            //Use BackgroundWorker to update the UI.
            _bgOutputWorker = new BackgroundWorker();

            _bgOutputWorker.DoWork += bgOutputWorker_DoWork;
            _bgOutputWorker.RunWorkerCompleted += bgOutputWorker_RunWorkerCompleted;
            _bgOutputWorker.ProgressChanged += bgOutputWorker_ProgressChanged;
            _bgOutputWorker.WorkerReportsProgress = true;
            _bgOutputWorker.WorkerSupportsCancellation = true;

            InitScanProcess();
        }

        protected void bgOutputWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                _thHashMonitor.Start();

                if (OutputList != null)
                {
                    while (_thHashMonitor.IsAlive)
                    {
                        if (OutputList.Count > 0)
                        {
                            OutputDataStruct tempResultData = OutputList.Take();
                            if (tempResultData != null)
                            {
                                _bgOutputWorker.ReportProgress(OutputList.Count, tempResultData);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result = ShowThreadExceptionDialog("Error occured.", ex);
                // Exits the program when the user clicks Abort. 
                if (result == DialogResult.Abort)
                    Environment.Exit(0);
            }
        }

        protected void bgOutputWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            OutputDataStruct tempResultData = (OutputDataStruct)e.UserState;
            _duplicateFilesFound++;
            lstBoxResults.Items.Add(tempResultData.FileName1 + @"          <==>          " + tempResultData.FileName2);
            lblDuplicateCounter.Text = _duplicateFilesFound + @" Duplicate file(s) found out of " + _totalFilesToScan + @" files.";

            using (StreamWriter outputResultFile = new StreamWriter(_outputFile, true))
            {
                outputResultFile.WriteLine("#{0} [{1}] - Thread{4}: {2} = {3}", _duplicateFilesFound, DateTime.Now.ToString(CultureInfo.CurrentCulture), tempResultData.FileName1, tempResultData.FileName2, tempResultData.ThreadId.ToString());
            }
        }

        protected void bgOutputWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            lblStatus.Text = (_scanStatusCanceled) ? @"File scanning canceled" : @"File scanning completed";
            RestoreScanSession();
        }

        private void btnSelectDirectory_Click(object sender, EventArgs e)
        {
            if (txtScanDirectory.Text != String.Empty)
            {
                dlgScanFolder.SelectedPath = txtScanDirectory.Text;
            }
            DialogResult results = dlgScanFolder.ShowDialog();

            if (results == DialogResult.OK)
            {
                txtScanDirectory.Text = dlgScanFolder.SelectedPath;                
            }
            ToggleButtonStatus();
        }

        private void btnSelectOutputFile_Click(object sender, EventArgs e)
        {
            dlgSaveOutputFile.Filter = @"Text File|*.txt";
            DialogResult results = dlgSaveOutputFile.ShowDialog();
            if (results == DialogResult.OK)
            {
                txtOutputFile.Text = dlgSaveOutputFile.FileName;                
            }
            ToggleButtonStatus();
        }

        private void btnStartScan_Click(object sender, EventArgs e)
        {
            try
            {
                InitScanProcess();

                _outputFile = txtOutputFile.Text.Trim();
                string scanDir = txtScanDirectory.Text.Trim();
                int scanThreads = Convert.ToInt32(numUpDownScanThreads.Value);

                UpdateStatusMessage(@"Validating input data and initializing...");

                //Check if the selected scan directory is still available.
                if (!Directory.Exists(scanDir))
                {
                    showValidatationMessage(@"Unable to find scan directory", MessageBoxIcon.Warning);
                    return;
                }

                lstBoxResults.Items.Clear();

                _scanList = new List<DataStuctInfo>();
                UpdateStatusMessage(@"Scanning directory contents...");

                String[] fileList = Directory.GetFiles(scanDir, "*", SearchOption.AllDirectories);

                _totalFilesToScan = fileList.Length;

                //Check if the files to scan is greater than the number of allocated threads.
                if (_totalFilesToScan < scanThreads)
                {
                    showValidatationMessage(@"Total files found in the directory is less than the scan thrad count. Please reduce the thread count or select a different direcotory.", MessageBoxIcon.Warning);
                    return;
                }

                foreach (string filePath in fileList)
                {
                    _scanList.Add(new DataStuctInfo(filePath));
                }

                if (_scanList.Count < 2)
                {
                    showValidatationMessage(@"Selected directory may not contain enough files to perform matching", MessageBoxIcon.Warning);
                    return;
                }

                Invalidate();

                if (File.Exists(_outputFile))
                {
                    File.Delete(_outputFile);
                }

                UpdateStatusMessage(@"Start scanning files...");

                btnStartScan.Enabled = false;
                btnPause.Enabled = true;
                btnPause.Visible = true;
                btnCancel.Enabled = true;
                btnCancel.Visible = true;
                btnSelectDirectory.Enabled = false;
                btnSelectOutputFile.Enabled = false;

                _threadExecState = new ThreadControl
                {
                    ThreadPause = false
                };

                UpdatePauseCaption();
                Refresh();

                OutputList = new BlockingCollection<OutputDataStruct>();
                PerformFileHashUpdate(scanThreads);
            }
            catch(Exception ex)
            {
                result = ShowThreadExceptionDialog("Error occured.", ex);
                // Exits the program when the user clicks Abort. 
                if (result == DialogResult.Abort)
                    Environment.Exit(0);  
            }

        }

        private void PerformFileHashUpdate(int threadCount)
        {
            int filesPerThread = _scanList.Count / threadCount;
            int arrayScanPos = 0;

            _hashThreadList = new List<Thread>();
            for (int threadPos = 0; threadPos < threadCount; threadPos++)
            {
                var scanLength = filesPerThread + ((threadPos == (threadCount - 1)) ? (_scanList.Count - (threadCount * filesPerThread)) : 0);
                HashProcessor threadData = new HashProcessor(_scanList, arrayScanPos, scanLength, threadPos, _threadExecState);

                Thread hashThread = new Thread(threadData.ThreadProc)
                {
                    Priority = ThreadPriority.Lowest
                };

                hashThread.Start();
                Thread.Sleep(10);
                _hashThreadList.Add(hashThread);
                arrayScanPos += filesPerThread;
            }

            _thHashMonitor = new Thread(HashStatusCheck);
            _bgOutputWorker.RunWorkerAsync();
        }

        public void HashStatusCheck()
        {
            if (_hashThreadList != null)
            {
                foreach (Thread thrObj in _hashThreadList)
                {
                    if (thrObj != null)
                    {
                        thrObj.Join();
                    }
                }
            }

            if (_hashThreadList != null)
            {
                _hashThreadList.Clear();
            }
        }


        private void btnPause_Click(object sender, EventArgs e)
        { 
            try
            {
                if (_hashThreadList != null)
                {
                    _threadExecState.ThreadPause = !_threadExecState.ThreadPause;
                    UpdatePauseCaption();
                }

            }
            catch (Exception ex)
            {
                result = ShowThreadExceptionDialog("Error occured.", ex);
                // Exits the program when the user clicks Abort. 
                if (result == DialogResult.Abort)
                    Environment.Exit(0);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {  
            try
            {
                RemoveOperatingThreads();
                _scanStatusCanceled = true;
                ToggleButtonStatus();
            }
            catch (Exception ex)
            {
                result = ShowThreadExceptionDialog("Error occured.", ex);
                // Exits the program when the user clicks Abort. 
                if (result == DialogResult.Abort)
                    Environment.Exit(0);
            }
        }

        private void showValidatationMessage(string msg, MessageBoxIcon icon, string title = "Warning !")
        {
            MessageBox.Show(msg, title, MessageBoxButtons.OK, icon);
        }

        private void UpdateStatusMessage(string statusMsg)
        {
            lblStatus.Text = statusMsg;
        }

        private void UpdatePauseCaption()
        {
            if (_threadExecState != null)
            {
                btnPause.Text = (_threadExecState.ThreadPause) ? @"Resume" : @"Pause";
            }
            else
            {
                btnPause.Text = @"Pause";
            }
        }

        private void ToggleButtonStatus()
        {
            btnStartScan.Enabled = ((txtOutputFile.Text.Trim() != String.Empty && txtScanDirectory.Text.Trim() != String.Empty));
            btnCancel.Enabled = !btnStartScan.Enabled;
            btnCancel.Visible = false;
            btnPause.Enabled = false;
            btnPause.Visible = false;
            btnSelectDirectory.Enabled = true;
            btnSelectOutputFile.Enabled = true;
        }

        private void RestoreScanSession()
        {
            ToggleButtonStatus();

            _threadExecState.ThreadPause = false;
            UpdatePauseCaption();
            lstBoxResults.Focus();
        }

        private void frmFileScaner_FormClosing(object sender, FormClosingEventArgs e)
        {
            RemoveOperatingThreads();
        }

        private void RemoveOperatingThreads()
        {
            if (_thHashMonitor != null)
            {
                if (_threadExecState != null)
                {
                    _threadExecState.ThreadPause = true;
                }

                if (_thHashMonitor != null)
                {
                    _thHashMonitor.Abort();
                }

                if (_hashThreadList != null)
                {
                    //Stop all available scan threads.
                    foreach (Thread tmpThread in _hashThreadList)
                    {
                        if (tmpThread != null)
                        {
                            tmpThread.Abort();
                        }
                    }

                    _hashThreadList.Clear();
                }
                if (_bgOutputWorker.IsBusy)
                {
                    _bgOutputWorker.CancelAsync();
                }
            }
        }

        private void InitScanProcess()
        {
            _totalFilesToScan = 0;
            _duplicateFilesFound = 0;
            _scanStatusCanceled = true;
            ToggleButtonStatus();
            lblDuplicateCounter.Text = "";
            lblStatus.Text = "";
        }

        public BackgroundWorker BgOutputWorker
        {
            get { return _bgOutputWorker; }
        } 

        /// <summary>
        ///  Creates the Exception error message and displays it. 
        /// </summary>
        /// <param name="title">Title for dialog</param>
        /// <param name="e">Exception</param>
        /// <returns>Dialog reults</returns>
        private static DialogResult ShowThreadExceptionDialog(string title, Exception e)
        {
            string errorMsg = "An application error occurred. Please contact the adminstrator " +
                "with the following information:\n\n";
            errorMsg = errorMsg + e.Message + "\n\nStack Trace:\n" + e.StackTrace;
            return MessageBox.Show(errorMsg, title, MessageBoxButtons.AbortRetryIgnore,
                MessageBoxIcon.Stop);
        }

    }
}
