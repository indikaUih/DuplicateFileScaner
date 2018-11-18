using System;

namespace DuplicateFileScaner
{
    /// <summary>
    /// Use to store files to scan collection.
    /// </summary>
    public class DataStuctInfo
    {
        /// <summary>
        /// Default class constructor.
        /// </summary>
        /// <param name="newFileName">Full path and filename of the scanned file.</param>
        public DataStuctInfo(string newFileName)
        {
            FileName = newFileName;
            HashCode = null;
            IsIgnore = false;
        }

        /// <summary>
        /// Full file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// md5 hash related to the file.
        /// </summary>
        public string HashCode { get; set; }

        /// <summary>
        /// Flag to ignore the file on scanning operation.
        /// </summary>
        public bool IsIgnore { get; set; }
    }

    
    /// <summary>
    /// Keeping duplicate files found.
    /// </summary>
    public class OutputDataStruct
    {
        private readonly string _fileNameA;
        private readonly string _fileNameB;
        private readonly int _threadId;

        /// <summary>
        /// Default class constructor.
        /// </summary>
        /// <param name="fileA">Source file path and name.</param>
        /// <param name="fileB">Matching file path and name.</param>
        /// <param name="threadNum">Scanned thread ID.</param>
        public OutputDataStruct(string fileA, string fileB, int threadNum)
        {
            _fileNameA = fileA;
            _fileNameB = fileB;
            _threadId = threadNum;
        }

        /// <summary>
        /// Source file path and name.
        /// </summary>
        public string FileName1
        {
            get { return _fileNameA; }
        }

        /// <summary>
        /// Matching file path and name.
        /// </summary>
        public string FileName2
        {
            get { return _fileNameB; }
        }

        /// <summary>
        /// Scanned thread ID.
        /// </summary>
        public int ThreadId
        {
            get { return _threadId; }
        }
    }

    /// <summary>
    /// Thread control class.
    /// </summary>
    public class ThreadControl
    {
        /// <summary>
        /// Thread pause status.
        /// </summary>
        public bool ThreadPause { get; set; }
    }

    /// <summary>
    /// Output event class related to the file scanner.
    /// </summary>
    public class OutputEventArgs : EventArgs
    {
        private OutputDataStruct _outputData;

        /// <summary>
        /// Scanner result structure.
        /// </summary>
        public OutputDataStruct OutputData
        {
            get { return _outputData; }
            set
            {
                _outputData = new OutputDataStruct(value.FileName1, value.FileName2, value.ThreadId);
            }
        }
    }

}
