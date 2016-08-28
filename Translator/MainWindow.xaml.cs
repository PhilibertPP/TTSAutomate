﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Threading;
using System.Windows.Shell;
using System.Threading;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TTSAutomate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        Boolean filenameSelected = false;

        private bool isManualEditCommit;

        private Boolean needToSave = true;

        private Boolean keepPlaying = false;

        public Boolean KeepPlaying
        {
            get { return keepPlaying; }
            set { keepPlaying = value;
                OnPropertyChanged("KeepPlaying");
            }
        }

        private Boolean isPlaying = false;

        public Boolean IsPlaying
        {
            get { return isPlaying; }
            set { isPlaying = value;
                OnPropertyChanged("IsPlaying");
            }
        }


        private static Queue<PlayItem> playQueue = new Queue<PlayItem>();

        public static Queue<PlayItem> PlayQueue
        {
            get { return playQueue; }
            set
            {
                playQueue = value;
            }
        }



        public Boolean NeedToSave
        {
            get { return needToSave; }
            set
            {
                needToSave = value;
                OnPropertyChanged("NeedToSave");
                Title = String.Format("TTSAutomate {2} - {0} {1}", PhraseFileName, NeedToSave ? "(Unsaved)" : "", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            }
        }

        private List<TTSProvider> providers = new List<TTSProvider>();

        public List<TTSProvider> TTSEngines
        {
            get { return providers; }
            set
            {
                providers = value;
                OnPropertyChanged("TTSEngines");
            }
        }

        private TTSProvider selectedEngine;

        public TTSProvider SelectedEngine
        {
            get { return selectedEngine; }
            set
            {
                selectedEngine = value;
                Properties.Settings.Default.LastTTSProvider = SelectedEngine.Name;
                OnPropertyChanged("SelectedEngine");
                CheckFolderForVoices();

            }
        }

        private int selectedRowCount;

        public int SelectedRowCount
        {
            get { return selectedRowCount; }
            set
            {
                selectedRowCount = value;
                OnPropertyChanged("SelectedRowCount");
            }
        }

        private int playableRowCount;

        public int PlayableRowCount
        {
            get { return playableRowCount; }
            set
            {
                playableRowCount = value;
                OnPropertyChanged("PlayableRowCount");
            }
        }

        BackgroundWorker DownloaderWorker = new BackgroundWorker();

        private ObservableCollection<PhraseItem> phraseItems = new ObservableCollection<PhraseItem>();

        public ObservableCollection<PhraseItem> PhraseItems
        {
            get { return phraseItems; }
            set
            {
                phraseItems = value;
                OnPropertyChanged("PhraseItems");
            }
        }
        private String phraseFileName = "New Phrase File";

        public String PhraseFileName
        {
            get { return phraseFileName; }
            set
            {
                phraseFileName = value;
                OnPropertyChanged("PhraseFileName");
            }
        }

        private String outputDirectoryName;

        public String OutputDirectoryName
        {
            get { return outputDirectoryName; }
            set
            {
                outputDirectoryName = value;
                OnPropertyChanged("OutputDirectoryName");
            }
        }

        private Boolean workerFinished = true;

        public Boolean WorkerFinished
        {
            get { return workerFinished; }
            set
            {
                workerFinished = value;
                OnPropertyChanged("WorkerFinished");
            }
        }

        private int downloadCount = 0;
        private int alreadyDownloaded = 0;

        public int DownloadCount
        {
            get { return downloadCount; }
            set
            {
                downloadCount = value;
                OnPropertyChanged("DownloadCount");
            }
        }

        private int downloadProgress = 0;

        public int DownloadProgress
        {
            get { return downloadProgress; }
            set
            {
                downloadProgress = value;
                OnPropertyChanged("DownloadProgress");
            }
        }

        public BitmapImage HeaderImage { get; private set; }
        public BitmapImage SettingsImage { get; private set; }

        public static MediaPlayer media = new MediaPlayer();
        private int InitialPhraseItems = 499;
        public static bool LoadedWindow = false;

        public MainWindow()
        {
            InitializeComponent();

            media.MediaEnded += delegate
            {
                media.Close();
                if (KeepPlaying)
                {
                    PlayQueuedItems();
                }
                else
                {
                    IsPlaying = false;
                }
            };


            //NAudio.MediaFoundation.MediaFoundationApi.Startup();
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            HeaderImage = LoadImage("speech-bubble.png");
            SettingsImage = LoadImage("settings.png");

            List<PhraseItem> initialitems = new List<PhraseItem>();
            for (int i = 0; i < InitialPhraseItems; i++)
            {
                initialitems.Add(new PhraseItem { Phrase = "" });
            }

            PhraseItems = new ObservableCollection<PhraseItem>(initialitems);

            TTSEngines.Add(new IvonaTTSProvider());
            TTSEngines.Add(new GoogleTTSProvider());
            TTSEngines.Add(new MicrosoftTTSProvider());
            TTSEngines.Add(new BingTTSProvider());
            TTSEngines.Add(new FromTextToSpeechTTSProvider());
            //foreach (var voice in ssss.GetInstalledVoices())
            //{
            //    TTSEngines.Add(new TTSProvider { Name = voice.VoiceInfo.Name, ProviderType = VoiceProvider.Provider.Microsoft, ProviderClass = VoiceProvider.Class.Local });
            //}
            //TTSEngines.Add(new TTSProvider { Name = "fromtexttospeech.com", ProviderType = VoiceProvider.Provider.wwwfromtexttospeechcom, ProviderClass = VoiceProvider.Class.Web });


            Title = String.Format("TTSAutomate {2} - {0} {1}", PhraseFileName, "(Unsaved)", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            DownloaderWorker.DoWork += DownloaderWorker_DoWork;
            DownloaderWorker.WorkerReportsProgress = true;
            DownloaderWorker.WorkerSupportsCancellation = true;
            DownloaderWorker.RunWorkerCompleted += DownloaderWorker_RunWorkerCompleted;
            DownloaderWorker.ProgressChanged += DownloaderWorker_ProgressChanged;

            if (Properties.Settings.Default.SetOutputDirectory)
            {
                OutputDirectoryName = Properties.Settings.Default.LastOutputDirectory;
            }
            if (Properties.Settings.Default.ReopenLastPSVFile)
            {
                if (!String.IsNullOrEmpty(Properties.Settings.Default.LastPhraseFile))
                {
                    if (File.Exists(Properties.Settings.Default.LastPhraseFile))
                    {
                        LoadPhraseFile(Properties.Settings.Default.LastPhraseFile);
                        PhraseFileName = Properties.Settings.Default.LastPhraseFile;
                        NeedToSave = false;
                        filenameSelected = true;
                    }

                }
            }
            if (Properties.Settings.Default.RememberLanguageSettings)
            {
                if (TTSEngines.Find(n => n.Name == Properties.Settings.Default.LastTTSProvider) != null)
                {
                    SelectedEngine = TTSEngines.Find(n => n.Name == Properties.Settings.Default.LastTTSProvider);

                }
                else
                {
                    SelectedEngine = TTSEngines[0];
                    SelectedEngine.SelectedVoice = SelectedEngine.AvailableVoices[0];

                }
                SelectedEngine.SelectedDiscreteVolume = Properties.Settings.Default.LastTTSDiscreteVolume;
                SelectedEngine.SelectedDiscreteSpeed = Properties.Settings.Default.LastTTSDiscreteSpeed;
                SelectedEngine.SelectedNumericVolume = Convert.ToInt32(Properties.Settings.Default.LastTTSNumericVolume);
                SelectedEngine.SelectedNumericSpeed = Convert.ToInt32(Properties.Settings.Default.LastTTSNumericSpeed);

            }
            else
            {
                SelectedEngine = TTSEngines[0];
                SelectedEngine.SelectedVoice = SelectedEngine.AvailableVoices[0];

            }


            this.DataContext = this;
        }

        private void CheckFolderForVoices()
        {
            if (LoadedWindow && SelectedEngine != null)
            {
                if (SelectedEngine.SelectedVoice != null)
                {

                    String comment = String.Format("{0}, {1}, {2}, {3}", SelectedEngine.Name, SelectedEngine.SelectedVoice.Name, SelectedEngine.SelectedDiscreteSpeed, SelectedEngine.SelectedDiscreteVolume);
                    foreach (var item in PhraseItems)
                    {
                        string filename = String.Format("{0}\\mp3\\{1}\\{2}.mp3", OutputDirectoryName, item.Folder, item.FileName);
                        if (File.Exists(filename))
                        {
                            if (new System.IO.FileInfo(filename).Length > 0)
                            {

                                try
                                {
                                    using (TagLib.File file = TagLib.File.Create(new TagLib.File.LocalFileAbstraction(String.Format("{0}\\mp3\\{1}\\{2}.mp3", OutputDirectoryName, item.Folder, item.FileName))))
                                    {
                                        if (file.Tag.Title == item.Phrase && file.Tag.Comment == comment)
                                        {
                                            item.DownloadComplete = true;
                                            OnPropertyChanged("PhraseItems");
                                        }
                                    }
                                }
                                catch (TagLib.CorruptFileException ex)
                                {
                                    Debug.WriteLine("File {1} Exception", ex, item.Phrase);
                                }
                            }
                        }
                    }
                }
                //WordsListView.Focus();
            }
        }


        private void DownloaderWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DownloadProgress = e.ProgressPercentage;
            TaskbarItemInfo.ProgressValue = (double)(e.ProgressPercentage) / (double)(DownloadCount);
        }

        private void DownloaderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            WorkerFinished = true;
            DownloadProgress = DownloadCount;
            TaskbarItemInfo.ProgressValue = 1.0;
            WordsListView.Focus();
            //TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(String name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static BitmapImage LoadImage(string fileName)
        {
            var image = new BitmapImage();

            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            return image;
        }

        private void DownloaderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                foreach (var item in PhraseItems)
                {
                    if (!IsPhraseEmpty(item))
                    {
                        if (!DownloaderWorker.CancellationPending)
                        {
                            if (!item.DownloadComplete)
                            {
                                DownloadItem(item, false);
                            }
                        }
                        else
                        {
                            e.Result = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show("Couldn't download audio\r\n\r\n" + Ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            while (PhraseItems.Count(n => n.DownloadComplete) < PhraseItems.Count(n => !String.IsNullOrEmpty(n.Phrase)))
            {
                if (!DownloaderWorker.CancellationPending)
                {
                    DownloaderWorker.ReportProgress(PhraseItems.Count(n => n.DownloadComplete) - alreadyDownloaded);
                    Thread.Sleep(100);
                }
                else
                {
                    return;
                }

            }
        }

        private void DownloadItem(PhraseItem item, bool play)
        {
            System.IO.Directory.CreateDirectory(String.Format("{0}\\mp3\\{1}\\", OutputDirectoryName, item.Folder));
            System.IO.Directory.CreateDirectory(String.Format("{0}\\wav\\{1}\\", OutputDirectoryName, item.Folder));
            if (play)
            {
                SelectedEngine.DownloadAndPlayItem(item, OutputDirectoryName);
            }
            else
            {
                SelectedEngine.DownloadItem(item, OutputDirectoryName);
            }
            //item.DownloadComplete = true;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while ((dep != null) && !(dep is DataGridRow)) // Find the cell that was clicked
            {
                dep = VisualTreeHelper.GetParent(dep);
            }
            // now dep is the row that contains the button that was clicked

            if (((dep as DataGridRow).DataContext as PhraseItem).DownloadComplete)
            {
                PlayAudioFullPath(String.Format("{0}\\{2}\\{1}.{2}", OutputDirectoryName, ((Button)sender).CommandParameter, Properties.Settings.Default.EncodeToWav ? "wav" : "mp3"), false);
            }
            else
            {
                if (!IsPhraseEmpty((dep as DataGridRow).DataContext as PhraseItem))
                {
                    DownloadItem((dep as DataGridRow).DataContext as PhraseItem, true);
                }
            }
        }

        public static void PlayAudioFullPath(string file, Boolean? deleteAfterPlay = false)
        {
            if (LoadedWindow)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    PlayAudio(new PlayItem { Filename = file, ShowAsPlaying = false });
                    //PlayAudio(file, deleteAfterPlay);
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(
                      DispatcherPriority.Background,
                      new Action(() =>
                      {
                      PlayAudio(new PlayItem { Filename = file, ShowAsPlaying = false });
                    //PlayAudio(file, deleteAfterPlay);
                }));
                }
            }
        }

        public void PlayQueuedItems()
        {
            if (PlayQueue.Count > 0)
            {
                IsPlaying = true;
                PlayItem item = PlayQueue.Dequeue();
                PlayAudio(item, false);
                if (item.ShowAsPlaying)
                {
                    WordsListView.ScrollIntoView(WordsListView.SelectedItems[0]);
                    WordsListView.SelectedItems.Remove(WordsListView.Items[item.PhraseIndex]);
                }
            }
            else
            {
                IsPlaying = false;
            }

        }

        private static void PlayAudio(PlayItem item, bool? deleteAfterPlay = false)
        {
            media.Open(new Uri(item.Filename, UriKind.RelativeOrAbsolute));
            media.Volume = 1;
            media.Play();
        }

        private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetItemsAsDirty();
        }

        public void SetItemsAsDirty()
        {
            PhraseItems.ToList().ForEach(n => n.DownloadComplete = false);
            CheckFolderForVoices();

        }

        private void WordsListView_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit && (e.Column.Header.ToString() != "Play"))
            {
                int rowIndex = e.Row.GetIndex();
                if (!isManualEditCommit)
                {
                    isManualEditCommit = true;
                    DataGrid grid = (DataGrid)sender;
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    isManualEditCommit = false;
                }
                (WordsListView.Items[rowIndex] as PhraseItem).DownloadComplete = false;
                NeedToSave = true;
            }
        }

        private void LoadPhraseFile(String filename)
        {
            PhraseItems.Clear();
            Regex r = new Regex(@"(?<Folder>.+)\|(?<FileName>.*)\|(?<Phrase>.*)\s{2}");

            List<PhraseItem> items = new List<PhraseItem>();

            for (int i = 0; i < InitialPhraseItems; i++)
            {
                items.Add(new PhraseItem { Phrase = "" });
            }

            int j = 0;

            using (System.IO.StreamReader file = new StreamReader(new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite)))
            {
                string contents = file.ReadToEnd();

                foreach (Match match in r.Matches(contents))
                {
                    if (j >= items.Count)
                    {
                        items.Add(new PhraseItem { Phrase = "" });
                    }
                    items[j++] = (new PhraseItem { Index = PhraseItems.Count, Folder = match.Groups["Folder"].Value, FileName = match.Groups["FileName"].Value, Phrase = match.Groups["Phrase"].Value, DownloadComplete = false });
                }
            }
            PhraseItems = new ObservableCollection<PhraseItem>(items);
            CheckFolderForVoices();
        }

        private void SavePhraseFileCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !String.IsNullOrEmpty(PhraseFileName);
        }

        private void BrowsePhraseFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (NeedToSave && PhraseItems.Count(n => !IsPhraseEmpty(n)) > 0)
            {
                switch (MessageBox.Show("Your phrases file is not saved. Would you like to save it before you open another file?", "Unsaved Phrases file", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        SaveOrSaveAs();
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        break;
                }
            }

            var dlg = new System.Windows.Forms.OpenFileDialog();
            if (!String.IsNullOrEmpty(Properties.Settings.Default.LastPhraseFile))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(Properties.Settings.Default.LastPhraseFile);

            }
            dlg.FileName = Properties.Settings.Default.LastPhraseFile;

            dlg.Title = "Open a Phrase file";
            dlg.Filter = "Phrase Files (*.psv)|*.psv|All Files (*.*)|*.*";
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                PhraseFileName = dlg.FileName;
                LoadPhraseFile(PhraseFileName);
                filenameSelected = true;
                Properties.Settings.Default.LastPhraseFile = dlg.FileName; //Path.GetDirectoryName(dlg.FileName);
                NeedToSave = false;
            }
        }

        private void CreatePhraseFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (NeedToSave && PhraseItems.Count(n => !IsPhraseEmpty(n)) > 0)
            {
                switch (MessageBox.Show("Your phrases file is not saved. Would you like to save it before you create another file?", "Unsaved Phrases file", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        if (SaveOrSaveAs())
                        {
                            break;
                        }
                        else
                        {
                            return;
                        }
                    case MessageBoxResult.No:
                        break;
                    default:
                        return;
                }
            }
            PhraseFileName = "New Phrase File";
            filenameSelected = false;
            NeedToSave = false;
        }

        private void BrowseOutputDirectoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var dlg = new CommonOpenFileDialog();
                dlg.Title = "Select output directory";
                dlg.IsFolderPicker = true;
                dlg.InitialDirectory = Properties.Settings.Default.LastOutputDirectory;
                dlg.AddToMostRecentlyUsedList = false;
                dlg.AllowNonFileSystemItems = false;
                dlg.EnsurePathExists = true;
                dlg.EnsureReadOnly = false;
                dlg.EnsureValidNames = true;
                dlg.Multiselect = false;
                dlg.ShowPlacesList = true;

                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    OutputDirectoryName = dlg.FileName;
                    Properties.Settings.Default.LastOutputDirectory = dlg.FileName;
                    SetItemsAsDirty();
                }
            }
            catch  // Are we on windows XP? then we must use the old folder browser dialog
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;// 
                dlg.SelectedPath = Properties.Settings.Default.LastOutputDirectory;
                dlg.ShowNewFolderButton = true;
                //dlg.Title = "Select output directory";
                //dlg.IsFolderPicker = true;
                //dlg.InitialDirectory 
                //dlg.AddToMostRecentlyUsedList = false;
                //dlg.AllowNonFileSystemItems = false;
                //dlg.EnsurePathExists = true;
                //dlg.EnsureReadOnly = false;
                //dlg.EnsureValidNames = true;
                //dlg.Multiselect = false;
                //dlg.ShowPlacesList = true;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputDirectoryName = dlg.SelectedPath;
                    Properties.Settings.Default.LastOutputDirectory = dlg.SelectedPath;
                    SetItemsAsDirty();
                }

            }
        }

        private void SavePhraseFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveOrSaveAs();
        }

        private Boolean SaveOrSaveAs()
        {
            if (filenameSelected)
            {
                SavePhraseFile(PhraseFileName);
                return true;
            }
            else
            {
                return SaveAs();
            }
        }

        private void SavePhraseFile(string Filename)
        {
            using (System.IO.StreamWriter file = new StreamWriter(new FileStream(Filename, FileMode.Create, FileAccess.Write)))
            {
                foreach (var item in PhraseItems)
                {
                    if (!IsPhraseEmpty(item))
                    {
                        file.WriteLine(String.Format("{0}|{1}|{2}", item.Folder, item.FileName, item.Phrase));
                    }
                }
                PhraseFileName = Filename;
                filenameSelected = true;
                NeedToSave = false;
            }
        }

        private bool IsPhraseEmpty(PhraseItem item)
        {
            return String.IsNullOrEmpty(item.Phrase);
        }

        private void SaveAsPhraseFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveAs();
        }

        private Boolean SaveAs()
        {
            var dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Title = "Save Phrase file";
            dlg.Filter = "Phrase Files (*.psv)|*.psv|All Files (*.*)|*.*";
            dlg.InitialDirectory = Properties.Settings.Default.LastPhraseFile;
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                SavePhraseFile(dlg.FileName);
                Properties.Settings.Default.LastPhraseFile = dlg.FileName;
            }
            return result == System.Windows.Forms.DialogResult.OK;
        }

        private void StartDownloadingCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            bool canDownload = (!String.IsNullOrEmpty(OutputDirectoryName));
            foreach (var item in PhraseItems)
            {
                item.CanDownload = canDownload;
            }
            e.CanExecute = canDownload && !String.IsNullOrEmpty(PhraseFileName) && WorkerFinished;
        }

        private void StartDownloadingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!DownloaderWorker.IsBusy)
            {
                WorkerFinished = false;
                DownloaderWorker.RunWorkerAsync();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

                DownloadCount = PhraseItems.Count(n => (n.DownloadComplete == false && !IsPhraseEmpty(n)));
                alreadyDownloaded = PhraseItems.Count(n => (n.DownloadComplete == true));
            }
        }
        private void StopDownloadingCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = DownloaderWorker.IsBusy;
        }

        private void StopDownloadingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DownloaderWorker.IsBusy)
            {
                DownloaderWorker.CancelAsync();
            }
        }

        private void InsertRowsAboveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = WordsListView.SelectedItems.Count > 0;
        }

        private void InsertRowsAboveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int rowsToAdd = WordsListView.SelectedItems.Count;
            for (int i = 0; i < rowsToAdd; i++)
            {
                PhraseItems.Insert(PhraseItems.IndexOf(WordsListView.SelectedItems[0] as PhraseItem), Properties.Settings.Default.CopyFolderWhenInsertingLines ? new PhraseItem { Folder = PhraseItems[PhraseItems.IndexOf(WordsListView.SelectedItems[0] as PhraseItem) - 1].Folder } : new PhraseItem());
                NeedToSave = true;
            }
        }

        private void InsertRowsBelowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = WordsListView.SelectedItems.Count > 0;
        }

        private void InsertRowsBelowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int rowsToAdd = WordsListView.SelectedItems.Count;
            for (int i = 0; i < rowsToAdd; i++)
            {
                PhraseItems.Insert(PhraseItems.IndexOf(WordsListView.SelectedItems[WordsListView.SelectedItems.Count - 1] as PhraseItem) + 1, Properties.Settings.Default.CopyFolderWhenInsertingLines ? new PhraseItem { Folder = (WordsListView.SelectedItems[WordsListView.SelectedItems.Count - 1] as PhraseItem).Folder } : new PhraseItem());
                NeedToSave = true;

            }
        }

        private void MoveRowsUpCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = WordsListView.SelectedItems.Count > 0;
        }

        private void MoveRowsUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            List<int> indices = new List<int>();
            foreach (var item in WordsListView.SelectedItems)
            {
                indices.Add(PhraseItems.IndexOf(item as PhraseItem));
            }
            indices.Sort();
            foreach (int index in indices)
            {
                PhraseItems.Move(index, index - 1);
                NeedToSave = true;
            }
        }

        private void MoveRowsDownCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = WordsListView.SelectedItems.Count > 0;
        }

        private void MoveRowsDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            List<int> indices = new List<int>();
            foreach (var item in WordsListView.SelectedItems)
            {
                indices.Add(PhraseItems.IndexOf(item as PhraseItem));
            }
            indices.Sort();
            indices.Reverse();
            foreach (int index in indices)
            {
                PhraseItems.Move(index, index + 1);
                NeedToSave = true;
            }
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {

            if (PhraseItems.Count > 0)
            {
                if (NeedToSave && PhraseItems.Count(n => !IsPhraseEmpty(n)) > 0)
                {
                    switch (MessageBox.Show("Your phrases file is not saved. Would you like to save it before you exit?", "Unsaved Phrases file", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                    {
                        case MessageBoxResult.Cancel:
                            e.Cancel = true;
                            break;
                        case MessageBoxResult.Yes:
                            SaveOrSaveAs();
                            break;
                        case MessageBoxResult.No:
                            break;
                        default:
                            break;
                    }
                }
                Properties.Settings.Default.Save();
                //NAudio.MediaFoundation.MediaFoundationApi.Shutdown();
            }
        }

        private void WordsListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) NeedToSave = true;
            if (e.Key == Key.Enter)
            {
                DependencyObject dep = (DependencyObject)e.OriginalSource;
                while ((dep != null) && !(dep is DataGridCell) && !(dep is System.Windows.Controls.Primitives.DataGridColumnHeader))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }
                if (dep is DataGridCell)
                {
                    if ((dep as DataGridCell).Column.Header.ToString() != "Play")
                    {
                        if ((dep as DataGridCell).IsEditing)
                        {
                            WordsListView.CommitEdit();
                            WordsListView.EndInit();

                        }
                        else
                        {
                            WordsListView.BeginEdit();
                        }
                        e.Handled = true;
                    }
                    else
                    {
                        DependencyObject row = dep;

                        while (!(row is DataGridRow))
                        {
                            row = VisualTreeHelper.GetParent(row);
                        }

                        if (((row as DataGridRow).DataContext as PhraseItem).DownloadComplete)
                        {
                            while (!(dep is Button))
                            {
                                dep = VisualTreeHelper.GetChild(dep, 0);
                            }
                            PlayAudioFullPath(String.Format("{0}\\{2}\\{1}.{2}", OutputDirectoryName, (dep as Button).CommandParameter, Properties.Settings.Default.EncodeToWav ? "wav" : "mp3"), false);
                        }
                        else
                        {
                            if (!IsPhraseEmpty((row as DataGridRow).DataContext as PhraseItem))
                            {
                                DownloadItem((row as DataGridRow).DataContext as PhraseItem, true);
                            }
                        }
                        e.Handled = true;
                    }
                }
            }
            if ((e.Key == Key.Right || e.Key == Key.Tab) && !((DependencyObject)e.OriginalSource is TextBox) && !((DependencyObject)e.OriginalSource is Button))
            {
                DependencyObject dep = (DependencyObject)e.OriginalSource;
                DependencyObject currentRow = dep;
                DependencyObject nextRow = dep;
                DataGridCell cell = dep as DataGridCell;
                cell.IsSelected = false;
                DependencyObject nextRowCell;
                if (cell.Column.Header.ToString() == "Play" || cell.Column.Header.ToString() == "Phrase to Speak")
                {

                    //}
                    nextRowCell = cell.PredictFocus(FocusNavigationDirection.Left);

                    while (!(nextRowCell is DataGridCell))
                    {
                        nextRowCell = VisualTreeHelper.GetParent(nextRowCell);

                    }
                    //    nextRowCell = cell.PredictFocus(FocusNavigationDirection.Left);

                    while ((nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Left) != null)
                    {
                        nextRowCell = (nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Left);
                    }
                    while ((currentRow != null) && !(currentRow is DataGridRow) && !(currentRow is System.Windows.Controls.Primitives.DataGridColumnHeader))
                    {
                        currentRow = VisualTreeHelper.GetParent(currentRow);
                    }

                    nextRowCell = cell.PredictFocus(FocusNavigationDirection.Down);
                    nextRow = VisualTreeHelper.GetParent(nextRowCell);
                    nextRowCell = (nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Left);
                    nextRowCell = (nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Left);
                    if (cell.Column.Header.ToString() == "Play")
                    {
                        nextRowCell = (nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Left);

                    }
                    while ((nextRow != null) && !(nextRow is DataGridRow) && !(nextRow is System.Windows.Controls.Primitives.DataGridColumnHeader))
                    {
                        nextRow = VisualTreeHelper.GetParent(nextRow);
                    }

                    SelectedRowCount = WordsListView.SelectedItems.Count;
                    GetPlayableRowCount();

                    if (Properties.Settings.Default.CopyFolderWhenSelectingEmptyRow && String.IsNullOrEmpty(((nextRow as DataGridRow).Item as PhraseItem).Folder))
                    {
                        //if (cell.Column.Header.ToString() != "Play")
                        //{
                        nextRowCell = (nextRowCell as DataGridCell).PredictFocus(FocusNavigationDirection.Right);
                        //}
                        int rowselected = PhraseItems.IndexOf(((nextRow as DataGridRow).Item as PhraseItem) as PhraseItem);
                        while (String.IsNullOrEmpty(PhraseItems[rowselected].Folder) && rowselected >= 0) { rowselected--; } // traverse upwards
                        if (rowselected >= 0)
                        {
                            (((nextRow as DataGridRow).Item as PhraseItem) as PhraseItem).Folder = PhraseItems[rowselected].Folder;
                            NeedToSave = true;
                        }
                    }

                    WordsListView.CurrentCell = new DataGridCellInfo(nextRowCell as DataGridCell);
                    (nextRowCell as DataGridCell).IsSelected = true;


                    e.Handled = true;
                }
            }
        }

        private void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoadedWindow)
            {
                Properties.Settings.Default.LastTTSDiscreteVolume = SelectedEngine.SelectedDiscreteVolume;
                Properties.Settings.Default.LastTTSDiscreteSpeed = SelectedEngine.SelectedDiscreteSpeed;
                Properties.Settings.Default.LastTTSVoice = SelectedEngine.SelectedVoice.Name;

            }
            SetItemsAsDirty();
        }

        private void SpeechRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetItemsAsDirty();

        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetItemsAsDirty();

        }

        private void WordsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedRowCount = WordsListView.SelectedItems.Count;
            GetPlayableRowCount();
            if (SelectedRowCount == 1 && e.AddedItems.Count > 0 && Properties.Settings.Default.CopyFolderWhenSelectingEmptyRow && String.IsNullOrEmpty((e.AddedItems[0] as PhraseItem).Folder))
            {
                int rowselected = PhraseItems.IndexOf(e.AddedItems[0] as PhraseItem);
                while (String.IsNullOrEmpty(PhraseItems[rowselected].Folder) && rowselected >= 0) { rowselected--; } // traverse upwards
                if (rowselected >= 0)
                {
                    (e.AddedItems[0] as PhraseItem).Folder = PhraseItems[rowselected].Folder;
                    NeedToSave = true;
                }
            }
        }

        private void GetPlayableRowCount()
        {
            PlayableRowCount = 0;
            foreach (var item in WordsListView.SelectedItems)
            {
                if ((item as PhraseItem).DownloadComplete)
                {
                    PlayableRowCount++;
                }
            }
        }

        private void Window_GotFocus(object sender, RoutedEventArgs e)
        {
            LoadedWindow = true;
            CheckFolderForVoices();
            foreach (var item in TTSEngines)
            {
                item.initialLoad = false;
            }
        }

        private void ShowSettingsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ShowSettingsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ConfigWindow cw = new ConfigWindow();
            cw.Owner = this;
            cw.ShowDialog();
        }

        private void PlaySelectedCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = PlayableRowCount > 0 && !IsPlaying;
        }

        private void PlaySelectedCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (PhraseItem item in WordsListView.SelectedItems)
            {
                if (item.DownloadComplete)
                {
                    PlayQueue.Enqueue(new PlayItem { Filename = String.Format("{0}\\{2}\\{1}.{2}", OutputDirectoryName, item.FullPathAndFile, Properties.Settings.Default.EncodeToWav ? "wav" : "mp3"), PhraseIndex = PhraseItems.IndexOf(item), ShowAsPlaying = true });

                }
            }
            KeepPlaying = true;
            PlayQueuedItems();

        }

        private void PlayAllCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = PhraseItems.Count(n => n.DownloadComplete) > 0 && !IsPlaying;
        }

        private void PlayAllCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (PhraseItem item in PhraseItems.Where(n => n.DownloadComplete))
            {
                PlayQueue.Enqueue(new PlayItem { Filename = String.Format("{0}\\{2}\\{1}.{2}", OutputDirectoryName, item.FullPathAndFile, Properties.Settings.Default.EncodeToWav ? "wav" : "mp3"), PhraseIndex = PhraseItems.IndexOf(item), ShowAsPlaying = true });
                WordsListView.SelectedItems.Add(item);
            }
            KeepPlaying = true;
            PlayQueuedItems();
        }


        private void PausePlayingCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = IsPlaying && KeepPlaying;
        }

        private void PausePlayingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            KeepPlaying = false;
        }

        private void ResumePlayingCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !KeepPlaying && IsPlaying;
        }

        private void ResumePlayingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            KeepPlaying = true;
            PlayQueuedItems();
        }


        private void StopPlayingCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = IsPlaying;
        }

        private void StopPlayingCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlayQueue.Clear();
            IsPlaying = false;
        }

    }

    public class PlayItem
    {
        public string Filename { get; set; }
        public bool ShowAsPlaying { get; set; }
        public int PhraseIndex { get; set; }
    }


    public class PhraseItem : INotifyPropertyChanged
    {
        public int Index { get; set; }

        private String folder;

        public String Folder
        {
            get { return folder; }
            set
            {
                folder = value;
                OnPropertyChanged("Folder");
                OnPropertyChanged("FullPathAndFile");
            }
        }

        private String fileName;

        public String FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                OnPropertyChanged("FileName");
                OnPropertyChanged("FullPathAndFile");
            }
        }

        public String FullPathAndFile
        {
            get
            {
                return String.Format("{0}\\{1}", Folder, FileName);
            }
        }

        private String phrase;

        public String Phrase
        {
            get { return phrase; }
            set
            {
                phrase = value;
                OnPropertyChanged("Phrase");
            }
        }

        private Boolean downloadComplete = false;

        public Boolean DownloadComplete
        {
            get { return downloadComplete; }
            set
            {
                downloadComplete = value;
                OnPropertyChanged("DownloadComplete");
            }
        }

        private Boolean canDownload = false;

        public Boolean CanDownload
        {
            get { return canDownload; }
            set
            {
                canDownload = value;
                OnPropertyChanged("CanDownload");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(String name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RowIndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((values[0] as DataGridRow).GetIndex() + 1).ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DataGridBehavior
    {
        #region DisplayRowNumber

        public static DependencyProperty DisplayRowNumberProperty =
            DependencyProperty.RegisterAttached("DisplayRowNumber",
                                                typeof(bool),
                                                typeof(DataGridBehavior),
                                                new FrameworkPropertyMetadata(false, OnDisplayRowNumberChanged));
        public static bool GetDisplayRowNumber(DependencyObject target)
        {
            return (bool)target.GetValue(DisplayRowNumberProperty);
        }
        public static void SetDisplayRowNumber(DependencyObject target, bool value)
        {
            target.SetValue(DisplayRowNumberProperty, value);
        }

        private static void OnDisplayRowNumberChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = target as DataGrid;
            if ((bool)e.NewValue == true)
            {
                EventHandler<DataGridRowEventArgs> loadedRowHandler = null;
                loadedRowHandler = (object sender, DataGridRowEventArgs ea) =>
                {
                    if (GetDisplayRowNumber(dataGrid) == false)
                    {
                        dataGrid.LoadingRow -= loadedRowHandler;
                        return;
                    }
                    ea.Row.Header = ea.Row.GetIndex() + 1;
                };
                dataGrid.LoadingRow += loadedRowHandler;

                ItemsChangedEventHandler itemsChangedHandler = null;
                itemsChangedHandler = (object sender, ItemsChangedEventArgs ea) =>
                {
                    if (GetDisplayRowNumber(dataGrid) == false)
                    {
                        dataGrid.ItemContainerGenerator.ItemsChanged -= itemsChangedHandler;
                        return;
                    }
                    GetVisualChildCollection<DataGridRow>(dataGrid).
                        ForEach(d => d.Header = d.GetIndex() + 1);
                };
                dataGrid.ItemContainerGenerator.ItemsChanged += itemsChangedHandler;
            }
        }

        #endregion // DisplayRowNumber

        #region Get Visuals

        private static List<T> GetVisualChildCollection<T>(object parent) where T : Visual
        {
            List<T> visualCollection = new List<T>();
            GetVisualChildCollection(parent as DependencyObject, visualCollection);
            return visualCollection;
        }

        private static void GetVisualChildCollection<T>(DependencyObject parent, List<T> visualCollection) where T : Visual
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T)
                {
                    visualCollection.Add(child as T);
                }
                if (child != null)
                {
                    GetVisualChildCollection(child, visualCollection);
                }
            }
        }

        #endregion // Get Visuals
    }
}
// http://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&q=Thousand&tl=En-au