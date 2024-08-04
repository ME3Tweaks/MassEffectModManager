using System.Linq;
using SevenZip.EventArguments;

namespace SevenZip
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;


    /// <summary>
    /// Archive extraction callback to handle the process of unpacking files
    /// </summary>
    internal sealed class ArchiveExtractCallback : CallbackBase, IArchiveExtractCallback, ICryptoGetTextPassword,
                                                   IDisposable
    {
        private List<uint> _actualIndexes;
        private IInArchive _archive;

        /// <summary>
        /// For Compressing event.
        /// </summary>
        private long _bytesCount;

        private long _bytesWritten;
        private long _bytesWrittenOld;
        private string _directory;

        /// <summary>
        /// Rate of the done work from [0, 1].
        /// </summary>
        private float _doneRate;

        private SevenZipExtractor _extractor;
        private FakeOutStreamWrapper _fakeStream;
        private uint? _fileIndex;
        private int _filesCount;
        private OutStreamWrapper _fileStream;
        private bool _directoryStructure;
        private int _currentIndex;
        private Func<ArchiveFileInfo, string> outputFilenameMapping;
        const int MEMORY_PRESSURE = 64 * 1024 * 1024; //64mb seems to be the maximum value

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>'
        /// <param name="extractor">The owner of the callback</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        /// <param name="directoryStructure">The value indicating whether to preserve directory structure of extracted files.</param>
        /// <param name="outputmappingcallback">Optional callback that is used to determine the output filepath of the entry being extracted</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, bool directoryStructure,
            List<uint> actualIndexes, SevenZipExtractor extractor, Func<ArchiveFileInfo, string> outputmappingcallback = null)
        {
            Init(archive, directory, filesCount, directoryStructure, actualIndexes, extractor, outputmappingcallback);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="password">Password for the archive</param>
        /// <param name="extractor">The owner of the callback</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        /// <param name="directoryStructure">The value indicating whether to preserve directory structure of extracted files.</param>
        /// <param name="outputmappingcallback">Optional callback that is used to determine the output filepath of the entry being extracted</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, bool directoryStructure,
            List<uint> actualIndexes, string password, SevenZipExtractor extractor, Func<ArchiveFileInfo, string> outputmappingcallback = null)
            : base(password)
        {
            Init(archive, directory, filesCount, directoryStructure, actualIndexes, extractor, outputmappingcallback);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="stream">The stream where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="fileIndex">The file index for the stream</param>
        /// <param name="extractor">The owner of the callback</param>
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex,
                                      SevenZipExtractor extractor)
        {
            Init(archive, stream, filesCount, fileIndex, extractor);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="stream">The stream where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="fileIndex">The file index for the stream</param>
        /// <param name="password">Password for the archive</param>
        /// <param name="extractor">The owner of the callback</param>
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex, string password,
                                      SevenZipExtractor extractor)
            : base(password)
        {
            Init(archive, stream, filesCount, fileIndex, extractor);
        }

        private void Init(IInArchive archive, string directory, int filesCount, bool directoryStructure,
            List<uint> actualIndexes, SevenZipExtractor extractor, Func<ArchiveFileInfo, string> outputMappingCallback = null)
        {
            CommonInit(archive, filesCount, extractor);
            _directory = directory;
            _actualIndexes = actualIndexes;
            _directoryStructure = directoryStructure;
            if (!directory.EndsWith("" + Path.DirectorySeparatorChar, StringComparison.CurrentCulture))
            {
                _directory += Path.DirectorySeparatorChar;
            }
            this.outputFilenameMapping = outputMappingCallback;
        }

        private void Init(IInArchive archive, Stream stream, int filesCount, uint fileIndex, SevenZipExtractor extractor)
        {
            CommonInit(archive, filesCount, extractor);
            _fileStream = new OutStreamWrapper(stream, false);
            _fileStream.BytesWritten += IntEventArgsHandler;
            _fileIndex = fileIndex;
        }

        private void CommonInit(IInArchive archive, int filesCount, SevenZipExtractor extractor)
        {
            _archive = archive;
            _filesCount = filesCount;
            _fakeStream = new FakeOutStreamWrapper();
            _fakeStream.BytesWritten += IntEventArgsHandler;
            _extractor = extractor;
            GC.AddMemoryPressure(MEMORY_PRESSURE);
        }
        #endregion

        #region Events

        /// <summary>
        /// Occurs when a new file is going to be unpacked
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an output stream for a new file to unpack in</remarks>
        public event EventHandler<FileInfoEventArgs> FileExtractionStarted;

        /// <summary>
        /// Occurs when a file has been successfully unpacked
        /// </summary>
        public event EventHandler<FileInfoEventArgs> FileExtractionFinished;

        /// <summary>
        /// Occurs when the archive is opened and 7-zip sends the size of unpacked data
        /// </summary>
        public event EventHandler<OpenEventArgs> Open;

        /// <summary>
        /// Occurs when the extraction is performed
        /// </summary>
        public event EventHandler<ProgressEventArgs> Extracting;

        /// <summary>
        /// Occurs during the extraction when a file already exists
        /// </summary>
        public event EventHandler<FileOverwriteEventArgs> FileExists;

        private void OnFileExists(FileOverwriteEventArgs e)
        {
            if (FileExists != null)
            {
                FileExists(this, e);
            }
        }

        private void OnOpen(OpenEventArgs e)
        {
            if (Open != null)
            {
                Open(this, e);
            }
        }

        private void OnFileExtractionStarted(FileInfoEventArgs e)
        {
            if (FileExtractionStarted != null)
            {
                FileExtractionStarted(this, e);
            }
        }

        private void OnFileExtractionFinished(FileInfoEventArgs e)
        {
            if (FileExtractionFinished != null)
            {
                FileExtractionFinished(this, e);
            }
        }

        private void OnExtracting(ProgressEventArgs e)
        {
            if (Extracting != null)
            {
                Extracting(this, e);
            }
        }

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            // If _bytesCount is not set, we can't update the progress.
            if (_bytesCount == 0)
            {
                return;
            }


            var pold = (int)((_bytesWrittenOld * 100) / _bytesCount);
            _bytesWritten += e.Value;
            var pnow = (int)((_bytesWritten * 100) / _bytesCount);

            if (pnow > pold)
            {
                if (pnow > 100)
                {
                    pold = pnow = 0;
                }
                _bytesWrittenOld = _bytesWritten;
                OnExtracting(new ProgressEventArgs((byte)pnow, (byte)(pnow - pold)));
            }
        }

        #endregion

        #region IArchiveExtractCallback Members

        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        public void SetTotal(ulong total)
        {
            _bytesCount = (long)total;
            OnOpen(new OpenEventArgs(total));
        }


        /// <summary>
        /// Sets the amount of work that has been completed.
        /// </summary>
        /// <param name="completeValue">Amount of work that has been completed (in bytes)</param>
        public void SetCompleted(ref ulong completeValue)
        {
            if (completeValue < (ulong)_bytesCount && completeValue >= 0)
            {
                OnProgressing(new DetailedProgressEventArgs(completeValue, (ulong) _bytesCount));
            }
        }

        public void OnProgressing(DetailedProgressEventArgs e)
        {
            if (Progressing != null)
            {
                Progressing(this, e);
            }
        }

        /// <summary>
        /// Occurs when 7z library has made progress on an operation
        /// </summary>
        public event EventHandler<DetailedProgressEventArgs> Progressing;

        /// <summary>
        /// Sets output stream for writing unpacked data
        /// </summary>
        /// <param name="index">Current file index</param>
        /// <param name="outStream">Output stream pointer</param>
        /// <param name="askExtractMode">Extraction mode</param>
        /// <returns>0 if OK</returns>
        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;

            if (Canceled)
            {
                return -1;
            }
            _currentIndex = (int)index;
            if (askExtractMode == AskMode.Extract)
            {
                var fileName = _directory;
                if (!_fileIndex.HasValue)
                {
                    #region Extraction to a file

                    if (_actualIndexes == null || _actualIndexes.Contains(index))
                    {
                        var data = new PropVariant();
                        _archive.GetProperty(index, ItemPropId.Path, ref data);
                        string entryName = NativeMethods.SafeCast(data, "");

                        #region Get entryName

                        if (String.IsNullOrEmpty(entryName))
                        {
                            if (_filesCount == 1)
                            {
                                var archName = Path.GetFileName(_extractor.FileName);
                                archName = archName.Substring(0, archName.LastIndexOf('.'));
                                if (!archName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                                {
                                    archName += ".tar";
                                }
                                entryName = archName;
                            }
                            else
                            {
                                entryName = "[no name] " + index.ToString(CultureInfo.InvariantCulture);
                            }
                        }

                        #endregion

                        //ExtractionPath - TODO: Allow customization of where file is output to.
                        if (outputFilenameMapping != null)
                        {
                            fileName = outputFilenameMapping.Invoke(_extractor.ArchiveFileData.FirstOrDefault(x => x.Index == index));
                        }
                        else
                        {
                            fileName = Path.Combine(_directory, _directoryStructure ? entryName : Path.GetFileName(entryName));
                        }

                        _archive.GetProperty(index, ItemPropId.IsDirectory, ref data);
                        try
                        {
                            ValidateFileNameAndCreateDirectory(fileName);
                        }
                        catch (Exception e)
                        {
                            AddException(e);
                            goto FileExtractionStartedLabel;
                        }
                        if (!NativeMethods.SafeCast(data, false))
                        {
                            #region Branch

                            _archive.GetProperty(index, ItemPropId.LastWriteTime, ref data);
                            var time = NativeMethods.SafeCast(data, DateTime.MinValue);
                            if (File.Exists(fileName))
                            {
                                var fnea = new FileOverwriteEventArgs(fileName);
                                OnFileExists(fnea);
                                if (fnea.Cancel)
                                {
                                    Canceled = true;
                                    return -1;
                                }
                                if (String.IsNullOrEmpty(fnea.FileName))
                                {
                                    outStream = _fakeStream;

                                    goto FileExtractionStartedLabel;
                                }
                                fileName = fnea.FileName;
                            }
                            try
                            {
                                if (File.Exists(fileName))
                                {
                                    //Ensure existing file is not read only
                                    FileAttributes attributes = File.GetAttributes(fileName);

                                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                    {
                                        // Make the file RW
                                        attributes = attributes & ~FileAttributes.ReadOnly;
                                        File.SetAttributes(fileName, attributes);
                                    }
                                }
                                _fileStream = new OutStreamWrapper(File.Create(fileName), fileName, time, true);
                            }
                            catch (Exception e)
                            {
                                if (e is FileNotFoundException)
                                {
                                    AddException(
                                        new IOException("The file \"" + fileName +
                                                        "\" was not extracted due to the File.Create fail."));
                                }
                                else
                                {
                                    AddException(e);
                                }
                                outStream = _fakeStream;
                                goto FileExtractionStartedLabel;
                            }
                            _fileStream.BytesWritten += IntEventArgsHandler;
                            outStream = _fileStream;

                            #endregion
                        }
                        else
                        {
                            #region Branch

                            if (!Directory.Exists(fileName))
                            {
                                try
                                {
                                    Directory.CreateDirectory(fileName);
                                }
                                catch (Exception e)
                                {
                                    AddException(e);
                                }
                                outStream = _fakeStream;
                            }

                            #endregion
                        }
                    }
                    else
                    {
                        outStream = _fakeStream;
                    }

                    #endregion
                }
                else
                {
                    #region Extraction to a stream

                    if (index == _fileIndex)
                    {
                        outStream = _fileStream;
                        _fileIndex = null;
                    }
                    else
                    {
                        outStream = _fakeStream;
                    }

                    #endregion
                }

            FileExtractionStartedLabel:
                _doneRate += 1.0f / _filesCount;
                var iea = new FileInfoEventArgs(
                    _extractor.ArchiveFileData[(int)index], PercentDoneEventArgs.ProducePercentDone(_doneRate));
                OnFileExtractionStarted(iea);
                if (iea.Cancel)
                {
                    if (!String.IsNullOrEmpty(fileName))
                    {
                        _fileStream.Dispose();
                        if (File.Exists(fileName))
                        {
                            try
                            {
                                File.Delete(fileName);
                            }
                            catch (Exception e)
                            {
                                AddException(e);
                            }
                        }
                    }
                    Canceled = true;
                    return -1;
                }
            }
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode) { }

        /// <summary>
        /// Called when the archive was extracted
        /// </summary>
        /// <param name="operationResult"></param>
        public void SetOperationResult(OperationResult operationResult)
        {
            if (operationResult != OperationResult.Ok && ReportErrors)
            {
                switch (operationResult)
                {
                    case OperationResult.CrcError:
                        AddException(new ExtractionFailedException("File is corrupted. Crc check has failed."));
                        break;
                    case OperationResult.DataError:
                        AddException(new ExtractionFailedException("File is corrupted. Data error has occurred."));
                        break;
                    case OperationResult.UnsupportedMethod:
                        AddException(new ExtractionFailedException("Unsupported method error has occurred."));
                        break;
                }
            }
            else
            {
                if (_fileStream != null && !_fileIndex.HasValue)
                {
                    try
                    {
                        _fileStream.BytesWritten -= IntEventArgsHandler;
                        _fileStream.Dispose();
                    }
                    catch (ObjectDisposedException) { }
                    _fileStream = null;
                    //GC.Collect();
                    //GC.WaitForPendingFinalizers();
                }
                var iea = new FileInfoEventArgs(
                    _extractor.ArchiveFileData[_currentIndex], PercentDoneEventArgs.ProducePercentDone(_doneRate));
                OnFileExtractionFinished(iea);
                if (iea.Cancel)
                {
                    Canceled = true;
                }
            }
        }

        #endregion

        #region ICryptoGetTextPassword Members

        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            GC.RemoveMemoryPressure(MEMORY_PRESSURE);

            if (_fileStream != null)
            {
                try
                {
                    _fileStream.Dispose();
                }
                catch (ObjectDisposedException) { }
                _fileStream = null;
            }
            if (_fakeStream != null)
            {
                try
                {
                    _fakeStream.Dispose();
                }
                catch (ObjectDisposedException) { }
                _fakeStream = null;
            }
        }

        #endregion

        /// <summary>
        /// Validates the file name and ensures that the directory to the file name is valid and creates intermediate directories if necessary
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>The valid file name</returns>
        private static void ValidateFileNameAndCreateDirectory(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new SevenZipArchiveException("Some archive name is null or empty.");
            }

            var splitFileName = new List<string>(fileName.Split(Path.DirectorySeparatorChar));

            if (splitFileName.Count > 2)
            {
                var tempFileName = splitFileName[0];

                for (var i = 1; i < splitFileName.Count - 1; i++)
                {
                    tempFileName += Path.DirectorySeparatorChar + splitFileName[i];

                    if (!Directory.Exists(tempFileName))
                    {
                        // Don't try to create a UNC share root, eg "\\localhost\".
                        if (i == 2 && tempFileName.StartsWith($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}") && tempFileName.LastIndexOf(Path.DirectorySeparatorChar) != 1)
                        {
                            continue;
                        }

                        Directory.CreateDirectory(tempFileName);
                    }
                }
            }
        }
    }
}
