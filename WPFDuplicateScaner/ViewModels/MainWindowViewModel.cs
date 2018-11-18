using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using WPFFolderBrowser;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Collections.Concurrent;
using System.Threading;
using WPFDuplicateScaner.Model;
using System.Diagnostics;

namespace WPFDuplicateScaner.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public MainWindowCommands mainWindowCommand;
        private List<String> numberOfThread;
        private bool isSearchReady;

        private string searchDirectory;
        private string outputFile;
        private bool isOperationStarted;
        private bool isVisiblility;
        private readonly BackgroundWorker _bgOutputWorker;

        private List<DataStuctInfo> _scanList;
        private List<Thread> _hashThreadList;
        private ThreadControl _threadExecState;
        private Thread _thHashMonitor;

        private int _totalFilesToScan = 0;
        private int _duplicateFilesFound = 0;
        private bool _scanStatusCanceled = true;
        private string _outputFile;

        private Stopwatch oWatch;
        private string lblStatus;

        public static int fCount = 0;

        public string LblStatus
        {
            get { return lblStatus; }
            set { lblStatus = value; }
        }

        ObservableCollection<OutputDataStruct> items;
        public static BlockingCollection<OutputDataStruct> outputList;

        public static BlockingCollection<OutputDataStruct> OutputList
        {
            get { return outputList; }
            set { outputList = value; }
        }

        private bool autoReset = false;

        public bool AutoReset
        {
            get { return autoReset ; }
            set { autoReset = value; }
        }
        

        public MainWindowViewModel()
        {   

            oWatch = new Stopwatch();
            mainWindowCommand = new MainWindowCommands(HandleCommands, IsValid);
            numberOfThread = new List<string>();
            for(int i=1;i<100;i++)
            {
                numberOfThread.Add(i.ToString());
            }

            items = new ObservableCollection<OutputDataStruct>();
            items.CollectionChanged += myItemCollectionChanged;

            _bgOutputWorker = new BackgroundWorker();

            _bgOutputWorker.DoWork += bgOutputWorker_DoWork;
            _bgOutputWorker.RunWorkerCompleted += bgOutputWorker_RunWorkerCompleted;
            _bgOutputWorker.ProgressChanged += bgOutputWorker_ProgressChanged;
            _bgOutputWorker.WorkerReportsProgress = true;
            _bgOutputWorker.WorkerSupportsCancellation = true;
        }

        private int numThreads;

        public int NumThreads
        {
            get { return numThreads; }
            set { numThreads = value; }
        }
        

        public ObservableCollection<OutputDataStruct> DuplicateItemList
        {
            get { return items; }
            set { items = value; }
        }

        public string SearchDirectory
        {
            get { return searchDirectory; }
            set { searchDirectory = value; }
        }

        public string OutputFile
        {
            get { return outputFile; }
            set { outputFile = value; }
        }

        public bool IsValid(string btnCommand)
        {
            bool returnVal = false;
            switch(btnCommand)
            {
                case "StartSearch":
                    if (!string.IsNullOrWhiteSpace(SearchDirectory) && !string.IsNullOrWhiteSpace(OutputFile) && !isOperationStarted)
                    {
                        EnableStart = "ShowStart";
                        Refresh("EnableStart");
                        returnVal = true;
                    }
                    break;
                case "CancelSearch":
                case "PauseSearch":
                    returnVal = (isOperationStarted) ? true : false;
                    break;
                case "SearchDirector":
                        returnVal = (isOperationStarted)?false:true;
                    break;
                case "OutputFile":
                case "animTest":
                        returnVal = (isOperationStarted)?false:true;
                    break;
            }
            return returnVal;
        }

        private string enableStart;

        public string EnableStart
        {
            get { return enableStart; }
            set { enableStart = value; }
        }
        

        public string IsVisiblility
        {
            get
            {
                return (isOperationStarted) ? "Visible" : "Hidden";
            }
        }

        public string IsVisiblilityForSearch
        {
            get
            {
                return (!isOperationStarted) ? "Visible" : "Hidden";
            }
        }


        /// <summary>
        /// Pass the command Object to the View to Bind
        /// </summary>
        public ICommand MainWindowCommand
        {
            get
            {
                return mainWindowCommand;
            }
        }

        public List<String> NumberOfThread
        {
            get{
                return numberOfThread;
            }
        }

        public bool IsSearchReady
        {
            get
            {
                return isSearchReady; 
            }
            set
            {
                isSearchReady = value;
            }
        }

        /// <summary>
        /// Use this to refresh the required property
        /// </summary>
        /// <param name="propertyName">The property that need refreshing</param>
        public void Refresh(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool HandleCommands(string commandParameter)
        {
            Console.WriteLine("HandleCommands");
            switch(commandParameter)
            {
                case "SearchDirector":
                    OpenSearchDirectory();
                break;
                case "OutputFile":
                    OpenOutputFile();
                break;
                case "StartSearch":
                    
                    EnableStart = "NotReady";
                    Refresh("EnableStart");
                    StartSearchOperation();
                break;
                case "CancelSearch":
                    CancelSearch();
                break;
                //    case "animTest":
                //    BindAnim = "Cat";
                //    Console.WriteLine("----- " + BindAnim);
                //    Refresh("BindAnim");
                //break;
            }
            return false;
        }

        private void CancelSearch()
        {
            RemoveOperatingThreads();
        }

        protected void bgOutputWorker_DoWork(object sender, DoWorkEventArgs e)
        {

                _thHashMonitor.Start();
                
                oWatch.Start();
                if (OutputList != null)
                {
                    while (_thHashMonitor.IsAlive)
                    {
                        if (OutputList.Count > 0)
                        {
                            OutputDataStruct tempResultData = OutputList.Take();
                            if (tempResultData != null)
                            {
                                 System.Windows.Application.Current.Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() =>
                                 {
                                     _duplicateFilesFound++;
                                     DuplicateItemList.Add(tempResultData);
                                         //tempResultData.FileName1 + @"          <==>          " + tempResultData.FileName2);
                                     //Console.WriteLine(_duplicateFilesFound + @" Duplicate file(s) found out of " + _totalFilesToScan + @" files.");
                                     LblStatus = @" " + _duplicateFilesFound.ToString() + " files found.";
                                     Refresh("LblStatus");
                                 }));
                                //_bgOutputWorker.ReportProgress(OutputList.Count, tempResultData);
                            }
                        }
                    }
                }
            
            Console.WriteLine("----" + outputList.Count.ToString());
        }

        public void DismissMessage()
        {
            Console.WriteLine("DismissMessage");
        }

        public void myItemCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Console.WriteLine("----" );
            Refresh("ItemUsers");
        }

        private void InitScanProcess()
        {
            fCount = 0;
            _totalFilesToScan = 0;
            _duplicateFilesFound = 0;
            _scanStatusCanceled = true;

            LblStatus = "";
        }

        private void StartSearchOperation()
        {
            try
            {
                InitScanProcess();

                _outputFile = OutputFile;
                string scanDir = SearchDirectory;
                int scanThreads = NumThreads;// Convert.ToInt32(numUpDownScanThreads.Value);

                LblStatus = @"Validating input data and initializing...";
                Refresh("LblStatus");

                //Check if the selected scan directory is still available.
                if (!Directory.Exists(scanDir))
                {
                    //showValidatationMessage(@"Unable to find scan directory", MessageBoxIcon.Warning);
                    return;
                }
                DuplicateItemList.Clear();
                Refresh("ItemUsers");
                //lstBoxResults.Items.Clear();

                _scanList = new List<DataStuctInfo>();
                LblStatus = @"Scanning directory contents...";
                Refresh("LblStatus");

                String[] fileList = Directory.GetFiles(scanDir, "*", SearchOption.AllDirectories);

                _totalFilesToScan = fileList.Length;

                //Check if the files to scan is greater than the number of allocated threads.
                if (_totalFilesToScan < scanThreads)
                {
                    //showValidatationMessage(@"Total files found in the directory is less than the scan thrad count. Please reduce the thread count or select a different direcotory.", MessageBoxIcon.Warning);
                    return;
                }

                foreach (string filePath in fileList)
                {
                    _scanList.Add(new DataStuctInfo(filePath));
                }

                if (_scanList.Count < 2)
                {
                    //showValidatationMessage(@"Selected directory may not contain enough files to perform matching", MessageBoxIcon.Warning);
                    return;
                }

                //Invalidate();

                if (File.Exists(_outputFile))
                {
                    File.Delete(_outputFile);
                }

                LblStatus = @"Start scanning files...";
                Refresh("LblStatus");

                _threadExecState = new ThreadControl
                {
                    ThreadPause = false
                };

                //UpdatePauseCaption();
                Refresh("IsVisiblilityForSearch");
                Refresh("IsVisiblility");
                
                OutputList = new BlockingCollection<OutputDataStruct>();
                isOperationStarted = true;
                mainWindowCommand.RefreshCanExecute();
                Refresh("IsVisiblility");
                Refresh("IsVisiblilityForSearch");
                PerformFileHashUpdate(scanThreads);
            }
            catch (Exception ex)
            {
                //result = ShowThreadExceptionDialog("Error occured.", ex);
                //// Exits the program when the user clicks Abort. 
                //if (result == DialogResult.Abort)
                //    Environment.Exit(0);
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

        protected void bgOutputWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Console.WriteLine("----" + e.ProgressPercentage.ToString());
            Refresh("ItemUsers");
        }

        protected void bgOutputWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            oWatch.Stop();

            AutoReset = true;
            Refresh("AutoReset");

            Console.WriteLine("------------------- Finished ------------------" + oWatch.ElapsedTicks.ToString());
            oWatch.Reset();
            LblStatus = @" " + _duplicateFilesFound.ToString() + " files found." + "                   Opereation Finsihed.";
            Refresh("LblStatus");
            isOperationStarted = false;
            Refresh("IsVisiblilityForSearch");
            Refresh("IsVisiblility");
            mainWindowCommand.RefreshCanExecute();
            
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

        private void OpenSearchDirectory()
        {
            WPFFolderBrowserDialog folderDialog = new WPFFolderBrowserDialog();
            if(folderDialog.ShowDialog() == true)
            {
                searchDirectory = folderDialog.FileName;
                Refresh("searchDirectory");
                mainWindowCommand.RefreshCanExecute();
            }

        }

        private void OpenOutputFile()
        {
            SaveFileDialog openFileDialog = new SaveFileDialog();
            
            openFileDialog.Filter = "Text files (*.txt)|*.txt";

            if (openFileDialog.ShowDialog() == true)
            {
                outputFile = openFileDialog.FileName;
                Refresh("outputFile");
                mainWindowCommand.RefreshCanExecute();
            }
        }
    }

    

    public class User
    {
        public string Name { get; set; }

        public int Age { get; set; }

        public string Mail { get; set; }
    }

    public class MainWindowCommands : ICommand
    {

        private Func<string,bool> whatToExecute;
        private Func<string, bool> whenToExecute;
        public event EventHandler CanExecuteChanged;

        public MainWindowViewModel vmObj;

        public MainWindowCommands(Func<string, bool> What, Func<string, bool> When)
        {
            //vmObj = obj;
            whatToExecute = What;
            whenToExecute = When;
        }

        public bool CanExecute(object parameter)
        {
            Console.WriteLine("CanExecute : " + parameter.ToString());
            return whenToExecute(parameter.ToString());
        }

       

        public void RefreshCanExecute()
        {
            if (CanExecuteChanged!= null)
            CanExecuteChanged(this, EventArgs.Empty);
        }

        public void Execute(object parameter)
        {
            Console.WriteLine("Execute");
            //vmObj.HandleCommands(parameter.ToString());
            whatToExecute(parameter.ToString());
        }
    }
}
