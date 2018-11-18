using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using WPFDuplicateScaner.Model;

namespace WPFDuplicateScaner.ViewModels
{   
    /// <summary>
    /// Use for Compute Hashes and checking for duplicates. 
    /// </summary>
    public class HashProcessor
    {
        private readonly List<DataStuctInfo> _fileMap;
        private readonly ThreadControl _threadCntrl;

        private readonly int _scanStartPos;
        private readonly int _scanEnd;
        private readonly int _threadId;        

        /// <summary>
        /// Event to execute on duplicate found.
        /// </summary>
        public event EventHandler<OutputEventArgs> OnMatchFound;

        /// <summary>
        /// Default class constructor.
        /// </summary>
        /// <param name="mapPointer">Object related to file map.</param>
        /// <param name="refStart">Scanner start index.</param>
        /// <param name="refLength">Length of the scanning session.</param>
        /// <param name="threadNo">Thread ID related to this scan session.</param>
        /// <param name="threadCnt">Object related to thread control class.</param>
        public HashProcessor(List<DataStuctInfo> mapPointer, int refStart, int refLength, int threadNo, ThreadControl threadCnt)
        {
            _fileMap = mapPointer;
            _scanStartPos = refStart;
            _scanEnd = refLength + _scanStartPos;
            _threadId = threadNo;
            _threadCntrl = threadCnt;

            OnMatchFound += OnUpdateUi;
        }

        /// <summary>
        /// Scan function to execute on thread instance. 
        /// </summary>
        public void ThreadProc()
        {
            //DialogResult result = DialogResult.Cancel;
            try
            {

                MD5 md5Proc = MD5.Create();
                int sPos = _scanStartPos;

                

                while (sPos < _scanEnd)
                {
                    if (!_threadCntrl.ThreadPause)
                    {
                        _fileMap[sPos].HashCode = Encoding.UTF8.GetString(md5Proc.ComputeHash(new FileStream(_fileMap[sPos].FileName, FileMode.Open, FileAccess.Read)));
                        PerformCrossMatch(sPos++);
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                //result = ShowThreadExceptionDialog("Error occured.", ex);
                //// Exits the program when the user clicks Abort. 
                //if (result == DialogResult.Abort)
                //    Environment.Exit(0);
            }
        }
        

        /// <summary>
        /// Routine to find duplicate entries in file map.
        /// </summary>
        /// <param name="matchPos"></param>
        public void PerformCrossMatch(int matchPos)
        {
          
            for (int scanPos = 0; scanPos < _fileMap.Count; scanPos++)
            {
                //Exculde same file from checking itself.
                if ((matchPos == scanPos) || (string.IsNullOrEmpty(_fileMap[scanPos].HashCode)))
                {
                    continue;
                }

                //Ignore already checked 
                if ((_fileMap[scanPos].IsIgnore) || (_fileMap[matchPos].IsIgnore))
                {
                    continue;
                }

                if (_fileMap[matchPos].HashCode == _fileMap[scanPos].HashCode)
                {
                    if (OnMatchFound != null)
                    {
                        _fileMap[matchPos].IsIgnore = true;
                        OutputEventArgs eventArgsData = new OutputEventArgs
                        {
                            OutputData = new OutputDataStruct(_fileMap[matchPos].FileName, _fileMap[scanPos].FileName, (_threadId + 1))
                        };
                        OnMatchFound(this, eventArgsData);
                    }
                }
            }
        }

        /// <summary>
        /// Routine to push detected duplicate entries to parent form.
        /// </summary>
        /// <param name="sender">Object related to the parent.</param>
        /// <param name="e">Event arugments.</param>
        public void OnUpdateUi(object sender, OutputEventArgs e)
        {
            while (!MainWindowViewModel.OutputList.TryAdd(e.OutputData))
            {
                Thread.Sleep(10);
            }
            MainWindowViewModel.fCount++;
            Console.WriteLine("+++++++++++++++++++ : " + MainWindowViewModel.fCount);
        }

        /// <summary>
        ///  Creates the Exception error message and displays it.
        /// </summary>
        /// <param name="title">Title for dialog</param>
        /// <param name="e">Exception</param>
        /// <returns>Dialog reults</returns>
        //private static DialogResult ShowThreadExceptionDialog(string title, Exception e)
        //{
        //    string errorMsg = "An application error occurred. Please contact the adminstrator " +
        //        "with the following information:\n\n";
        //    errorMsg = errorMsg + e.Message + "\n\nStack Trace:\n" + e.StackTrace;
        //    return MessageBox.Show(errorMsg, title, MessageBoxButtons.AbortRetryIgnore,
        //        MessageBoxIcon.Stop);
        //}
    }
}
