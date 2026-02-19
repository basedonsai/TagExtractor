using OCRTool.Application;
using OCRTool.Core.Configuration;
using OCRTool.Core.Models;
using OCRTool.Infrastructure.Excel;
using OCRTool.Infrastructure.Logging;
using OCRTool.Infrastructure.OCR;
using OCRTool.Infrastructure.PDF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OCRTool.UI
{
    /// <summary>
    /// Main window for the OCR Tool
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _selectedFolder = string.Empty;
        private string _outputFolder = string.Empty;
        private string _outputFolderText = string.Empty;
        private int _progressValue;
        private string _statusMessage = "Ready";
        private bool _isProcessing;
        private ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();
        private readonly ConfigurationManager _configManager;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _configManager = ConfigurationManager.CreateDefault();
        }

        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                _selectedFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanProcess));
                LoadFiles();
            }
        }

        public string OutputFolderText
        {
            get => _outputFolderText;
            set
            {
                _outputFolderText = value;
                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanProcess));
            }
        }

        public bool CanProcess => !IsProcessing && Files.Count > 0;

        public ObservableCollection<FileItem> Files
        {
            get => _files;
            set
            {
                _files = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanProcess));
            }
        }
       

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // First, try to allow selecting both files and folders using a custom approach
            // We'll show a dialog that allows selecting multiple PDF files
            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select PDF files to process",
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                // User selected files directly
                var selectedFiles = fileDialog.FileNames;
                if (selectedFiles.Any())
                {
                    // Get the folder containing the first file (for reference only)
                    var folder = Path.GetDirectoryName(selectedFiles[0]);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        // Store the folder path but DON'T load all files from it
                        // Only add the specifically selected files
                        _selectedFolder = folder;
                        FolderPathTextBox.Text = folder;
                        OnPropertyChanged(nameof(SelectedFolder));
                        
                        // Clear existing files and add only selected ones
                        Files.Clear();
                        
                        foreach (var file in selectedFiles)
                        {
                            var fileName = Path.GetFileName(file);
                            // Check if file already exists in the list
                            if (!Files.Any(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                            {
                                Files.Add(new FileItem
                                {
                                    Name = fileName,
                                    Path = file,
                                    Status = "Pending",
                                    StatusColor = Brushes.Black
                                });
                            }
                        }
                        StatusMessage = $"{Files.Count} files ready to process";
                        OnPropertyChanged(nameof(CanProcess));
                    }
                }
            }
            else
            {
                // No files selected, fall back to folder selection
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select folder containing PDF or image files"
                };

                if (folderDialog.ShowDialog() == true)
                {
                    SelectedFolder = folderDialog.FolderName;
                    FolderPathTextBox.Text = folderDialog.FolderName;
                }
            }
        }

        private void LoadFiles()
        {
            Files.Clear();

            if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
                return;

            var supportedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp" };

            var files = Directory.GetFiles(SelectedFolder)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                Files.Add(new FileItem
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    Status = "Pending",
                    StatusColor = Brushes.Black
                });
            }

            if (Files.Count == 0)
            {
                StatusMessage = "No supported files found in selected folder";
            }
            else
            {
                StatusMessage = $"{Files.Count} files ready to process";
            }
            
            OnPropertyChanged(nameof(CanProcess));
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (Files.Count == 0)
            {
                MessageBox.Show("No files to process", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if output folder is already set
            if (string.IsNullOrEmpty(_outputFolder))
            {
                // First, ask user for output location
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select output folder for results"
                };

                if (folderDialog.ShowDialog() != true)
                {
                    // User cancelled - don't start processing
                    return;
                }

                _outputFolder = folderDialog.FolderName;
                OutputFolderText = _outputFolder;
            }

            IsProcessing = true;
            ProgressValue = 0;
            StatusMessage = "Initializing...";

            // Variables to store results from events
            List<ExtractionResult>? results = null;
            List<ProcessingLog>? logs = null;
            string? outputPath = null;
            string? errorMessage = null;

            try
            {
                // Reset file status
                foreach (var file in Files)
                {
                    file.Status = "Pending";
                    file.StatusColor = Brushes.Black;
                }

                // Initialize components
                var logger = new FileLogger();
                
                // Create batch processor using static factory method
                var processor = BatchProcessor.Create(_configManager, logger);
                
                // Subscribe to progress updates
                processor.ProgressChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressValue = args.ProgressPercentage;
                        StatusMessage = $"Processing... {args.ProgressPercentage}%";
                    });
                };
                
                // Subscribe to file processing updates
                processor.FileProcessing += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Processing {args.CurrentFile} of {args.TotalFiles}: {args.FileName}";
                        
                        // Update file status in the list
                        var currentFile = Files.FirstOrDefault(f => f.Name == args.FileName);
                        if (currentFile != null)
                        {
                            currentFile.Status = args.Status;
                            currentFile.StatusColor = args.Status == "Complete" ? Brushes.Green : Brushes.Blue;
                        }
                    });
                };
                
                // Subscribe to completion
                processor.ProcessingCompleted += (s, args) =>
                {
                    results = args.Results;
                    logs = args.Logs;
                    outputPath = args.OutputPath;
                    if (!args.Success)
                    {
                        errorMessage = args.Message;
                    }
                };

                // Process on background thread - use specific file paths from the UI list
                var filePaths = Files.Select(f => f.Path).ToList();
                await Task.Run(() => processor.ProcessSpecificFilesAsync(filePaths, _outputFolder));

                // Check for errors
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    StatusMessage = $"Error: {errorMessage}";
                    MessageBox.Show(
                        $"An error occurred:\n\n{errorMessage}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Export results if we have them
                if (results != null && results.Count > 0)
                {
                    // Generate separate .txt log file
                    var loggerForExport = new FileLogger();
                    foreach (var log in logs ?? new List<ProcessingLog>())
                    {
                        loggerForExport.Log(log);
                    }
                    var logPath = Path.Combine(_outputFolder, $"OCR_Processing_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    SaveLogToTextFile(logPath, logs);

                    // Show summary
                    var totalTags = results.Sum(r => r.Tags.Count);
                    var totalEquipment = results.Sum(r => r.Equipment.Count);

                    StatusMessage = $"Complete! Results saved to: {Path.GetFileName(outputPath)}";
                    ProgressValue = 100;

                    // Show summary
                    MessageBox.Show(
                        $"Processing complete!\n\nPages Processed: {results.Count}\nTags found: {totalTags}\nEquipment found: {totalEquipment}\n\nResults saved to:\n{outputPath}\n\nLog saved to:\n{logPath}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "No results to export";
                    MessageBox.Show(
                        "Processing complete but no results were generated.",
                        "Info",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"An error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Saves processing log to a separate .txt file
        /// </summary>
        private void SaveLogToTextFile(string logPath, List<ProcessingLog>? logs)
        {
            if (logs == null || logs.Count == 0)
            {
                File.WriteAllText(logPath, "No processing logs available.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("OCR Processing Log");
            sb.AppendLine("====================");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("File                      | Page | Type        | Items | Status  | Message");
            sb.AppendLine("-------------------------|------|-------------|-------|---------|---------");

            foreach (var log in logs)
            {
                sb.AppendLine($"{log.FileName,-25} | {log.PageNumber,4} | {log.PageType,-11} | {log.ItemsFound,5} | {log.Status,-7} | {log.Message}");
            }

            File.WriteAllText(logPath, sb.ToString());
        }

        private void OnProgressUpdated(object? sender, ProgressReport report)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressValue = report.PercentComplete;
                StatusMessage = $"Processing {report.FileNumber} of {report.TotalFiles}";

                if (!string.IsNullOrEmpty(report.CurrentFile))
                {
                    var currentFile = Files.FirstOrDefault(f => f.Path == report.CurrentFile);
                    if (currentFile != null)
                    {
                        currentFile.Status = report.Status;
                        currentFile.StatusColor = report.Status == "Complete" ? Brushes.Green : Brushes.Blue;
                    }
                }
            });
        }

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the user-writable config path first
            // This ensures users can always edit the config file
            var userConfigPath = _configManager.GetUserConfigPath("extraction_config.xml");
            
            // Ensure default config exists (extracts from embedded resource if needed)
            var configPath = _configManager.GetConfigFilePathWithFallback("extraction_config.xml");

            if (File.Exists(configPath))
            {
                try
                {
                    // Try to open with the user's preferred editor
                    // Check if there's a custom editor configured, otherwise ask user
                    var editorPath = GetPreferredEditor();
                    
                    if (!string.IsNullOrEmpty(editorPath) && File.Exists(editorPath))
                    {
                        Process.Start(editorPath, $"\"{configPath}\"");
                    }
                    else
                    {
                        // Fall back to system default or let Windows handle it
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = configPath,
                            UseShellExecute = true
                        };
                        Process.Start(startInfo);
                    }
                }
                catch
                {
                    MessageBox.Show("Could not open config file. Please check your default file associations.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"Config file not found. Expected at:\n{userConfigPath}\n\nThe config will be automatically created on first run.", 
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private string? GetPreferredEditor()
        {
            // Check common editor locations
            var possibleEditors = new[]
            {
                @"C:\Program Files\Notepad++\notepad++.exe",
                @"C:\Program Files (x86)\Notepad++\notepad++.exe",
                @"C:\Program Files\Sublime Text\sublime_text.exe",
                @"C:\Program Files\VS Code\Code.exe",
                @"C:\Program Files\Microsoft VS Code\Code.exe"
            };

            foreach (var editor in possibleEditors)
            {
                if (File.Exists(editor))
                    return editor;
            }

            return null;
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            // First try to open the user-chosen output folder if it exists
            if (!string.IsNullOrEmpty(_outputFolder) && Directory.Exists(_outputFolder))
            {
                try
                {
                    Process.Start("explorer.exe", _outputFolder);
                    return;
                }
                catch
                {
                    MessageBox.Show("Could not open output folder", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            
            // If no output folder has been created yet, let user select one
            var folderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select output folder to open"
            };

            if (folderDialog.ShowDialog() == true)
            {
                try
                {
                    Process.Start("explorer.exe", folderDialog.FolderName);
                }
                catch
                {
                    MessageBox.Show("Could not open folder", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is FileItem fileItem)
            {
                Files.Remove(fileItem);
                StatusMessage = $"{Files.Count} files ready to process";
                OnPropertyChanged(nameof(CanProcess));
            }
        }

        private void ClearAllFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Files.Clear();
            _selectedFolder = string.Empty;
            FolderPathTextBox.Text = "Select a folder or PDF files...";
            StatusMessage = "Ready";
            OnPropertyChanged(nameof(CanProcess));
        }

        private void OutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select output folder for results (optional)"
            };

            if (folderDialog.ShowDialog() == true)
            {
                _outputFolder = folderDialog.FolderName;
                OutputFolderText = _outputFolder;
            }
        }
    }

    /// <summary>
    /// Represents a file in the UI list
    /// </summary>
    public class FileItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _path = string.Empty;
        private string _status = "Pending";
        private Brush _statusColor = Brushes.Black;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
