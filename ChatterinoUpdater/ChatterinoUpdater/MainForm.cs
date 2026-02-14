using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;

namespace ChatterinoUpdater
{
    public partial class MainForm : Form
    {
        public Action SuccessCallback { get; set; }

        private readonly string _ownDirectory;
        private int _fileCount;
        private int _currentFile = 1;
        private ManualResetEvent _continueEvent = new ManualResetEvent(false);

        public MainForm()
        {
            InitializeComponent();

            try
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    new FileInfo(Assembly.GetEntryAssembly().Location).FullName);
            }
            catch { }

            labelStatus.Text = "";
            buttonRetry.Visible = false;

            buttonRetry.Click += (s, e) => _continueEvent.Set();
            buttonCancel.Click += (s, e) => Close();

            _ownDirectory = new FileInfo(Assembly.GetEntryAssembly().Location).Directory.Name.TrimEnd('/', '\\') + '/';

            startInstall();
        }

        private void startInstall()
        {
            Task.Run(() =>
            {
                string zipPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Chatterino\update2.zip");

                try
                {
                    using (var fileStream = File.OpenRead(zipPath))
                    using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                    {
                        _fileCount = zipArchive.Entries.Count(x => !string.IsNullOrEmpty(x.Name));

                        processZipFile(zipArchive);
                    }

                    File.Delete(zipPath);
                }
                catch
                {
                    this.Invoke(() =>
                    {
                        labelStatus.Text = "Error";
                        rtbError.Text = "Update package not found.";
                        buttonCancel.Text = "Close";
                    });
                    return;
                }

                this.Invoke(() =>
                {
                    labelStatus.Text = "Success!";
                    buttonCancel.Text = "OK";

                    SuccessCallback?.Invoke();
                    Close();
                });
            });
        }

        private void processZipFile(ZipArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                while (true)
                {
                    this.Invoke(() =>
                    {
                        rtbError.Text = "";
                        buttonRetry.Hide();
                    });

                    try
                    {
                        this.Invoke(() =>
                        {
                            labelStatus.Text = $@"Installing file {_currentFile} of {_fileCount}";
                        });

                        processEntry(entry);

                        break;
                    }
                    catch (Exception exc)
                    {
                        this.Invoke(() =>
                        {
                            buttonRetry.Show();

                            var message = exc.Message;
                            message += "\n\nIf you have the browser extension enabled you might need to close chrome.";

                            rtbError.Text = message;
                        });

                        Console.WriteLine(exc);
                    }

                    _continueEvent.WaitOne();
                    _continueEvent.Reset();
                }
            }
        }

        private void processEntry(ZipArchiveEntry entry)
        {
            // skip directories
            if (string.IsNullOrEmpty(entry.Name))
                return;

            // skip if same name as this directory
            var entryName = Regex.Replace(entry.FullName, "^Chatterino2/", "");

            if (entryName.StartsWith(_ownDirectory))
                return;

            // extract the file
            var outPath = Path.Combine("..", entryName);

            // create directory if needed
            var directoryName = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            // write the file
            using (var input = entry.Open())
            using (var output = File.Create(outPath))
            {
                input.CopyTo(output);
            }

            _currentFile++;
        }
    }
}
