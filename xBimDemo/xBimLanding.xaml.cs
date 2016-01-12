using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;
using Xbim.XbimExtensions.Interfaces;
using xBimDemo.Annotations;
using XbimGeometry.Interfaces;

namespace xBimDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class xBimLanding : INotifyPropertyChanged
    {
        #region Field

        private string _ifcFilename;
        private BackgroundWorker _worker;
        private string _temporaryXbimFileName;
        private string _mainWindowsName;
        private bool _isLoading;
        private string _openedModelFileName;
        private ICommand _buttonCommand;

        #endregion Field

        #region Properties

        public string MainWindowsName
        {
            get { return _mainWindowsName; }
            set
            {
                _mainWindowsName = value;
                OnPropertyChanged(nameof(MainWindowsName));
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public ICommand ButtonCommand
        {
            get
            {
                return _buttonCommand ??
                    (_buttonCommand = new RelayCommand(
                    param => ButtonBase_OnClick(),
                    param => CanImport()));
            }
        }

        #endregion Properties

        public event PropertyChangedEventHandler PropertyChanged;

        public xBimLanding()
        {
            InitializeComponent();
            CreateWorker();
            StatusMsg.Text = "Ready";
            _isLoading = false;
        }

        /// <summary>
        /// Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(IPersistIfcEntity), typeof(MainWindow),
                                        new UIPropertyMetadata(null, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        public IPersistIfcEntity SelectedItem
        {
            get { return (IPersistIfcEntity)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        private ObjectDataProvider ModelProvider => MainFrame.DataContext as ObjectDataProvider;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool CanImport()
        {
            return !_isLoading;
        }

        private void ButtonBase_OnClick()
        {
            var dialog = new OpenFileDialog();
            // open file dialogue
            if (dialog.ShowDialog() != true) return;

            // return if selected same file
            if (dialog.FileName == _openedModelFileName) return;
            _ifcFilename = dialog.FileName;

            // call worker
            _worker.DoWork += OpenFile;
            _worker.RunWorkerAsync();
        }

        private void OpenFile(object s, DoWorkEventArgs args)
        {
            var worker = s as BackgroundWorker;
            var model = new XbimModel();
            try
            {
                if (worker != null)
                {
                    IsLoading = true;
                    _openedModelFileName = _ifcFilename;
                    _temporaryXbimFileName = Path.GetTempFileName();
                    model.CreateFrom(_ifcFilename,
                        _temporaryXbimFileName,
                        worker.ReportProgress,
                        true);

                    //upgrade to new geometry represenation, uses the default 3D model
                    var context = new Xbim3DModelContext(model);
                    context.CreateContext(XbimGeometryType.PolyhedronBinary, worker.ReportProgress, false);

                    if (worker.CancellationPending)
                    {
                        try
                        {
                            model.Close();
                            if (File.Exists(_temporaryXbimFileName))
                            {
                                File.Delete(_temporaryXbimFileName);
                                _temporaryXbimFileName = null;
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
                args.Result = model;
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Error reading " + _ifcFilename);
                var indent = "\t";
                while (ex != null)
                {
                    sb.AppendLine(indent + ex.Message);
                    ex = ex.InnerException;
                    indent += "\t";
                }

                args.Result = new Exception(sb.ToString());
            }
        }

        private void CreateWorker()
        {
            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _worker.ProgressChanged += delegate (object s, ProgressChangedEventArgs args)
            {
                if (args.ProgressPercentage < 0 || args.ProgressPercentage > 100)
                    return;
                Bar.Value = args.ProgressPercentage;
                StatusMsg.Text = (string)args.UserState;
            };

            _worker.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs args)
            {
                if (args.Result is XbimModel)
                {
                    ModelProvider.ObjectInstance = args.Result;
                    ModelProvider.Refresh();
                }
                else if (args.Result is Exception)
                {
                    MessageBox.Show(args.Result.ToString());
                }
                Bar.Value = 0;
                StatusMsg.Text = "";
                IsLoading = false;
            };
        }
    }
}