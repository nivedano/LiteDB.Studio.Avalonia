using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LiteDB.Studio.Avalonia.Dtos;
using LiteDB.Studio.Avalonia.Entities;
using LiteDB.Studio.Avalonia.Enums;
using LiteDB.Studio.Avalonia.Helpers;
using LiteDB.Studio.Avalonia.ItemModels;
using LiteDB.Studio.Avalonia.Views;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using SvgImage = Avalonia.Svg.Skia.Svg;

namespace LiteDB.Studio.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<object> _historyItems;

        [ObservableProperty]
        private ObservableCollection<object> _dbItems;

        [ObservableProperty]
        private ObservableCollection<Dictionary<string, object>>? _gridItems;

        [ObservableProperty]
        private string? _resultText;

        [ObservableProperty]
        private bool? _isBusy = false;

        [ObservableProperty]
        private int? _lineNum = 0;

        [ObservableProperty]
        private int? _columnNum = 0;

        [ObservableProperty]
        private bool _resultCountIsVisible = false;

        [ObservableProperty]
        private bool _isConnOnAppStart = false;

        /// <summary>
        /// Whether the database is connected.
        /// </summary>
        [ObservableProperty]
        private bool _isDbConnected = false;

        [ObservableProperty]
        private string _resultCountText = string.Empty;

        [ObservableProperty]
        private string _resultElapsedText = string.Empty;

        [ObservableProperty]
        private TaskData? _activeTask = null;

        [ObservableProperty]
        private TabItem? _activeTabItem = null;

        private LiteDatabase? _db = null;
        private LiteDatabase? _sysDb = null;
        private FileInfo _sysDbFileInfo;
        private string? _filename = null; 
        private DatabaseDebugger? _debugger = null;
        private int _queryNum = 1; 
        private CompletionWindow? completionWindow;
        private List<SqlWordCompletionData> sqlWords = SqlWordCompletionData.Instances;
        private bool initLoading = true;

        public MainWindowViewModel()
        {
            _historyItems = new();
            _dbItems = new();

            _sysDbFileInfo = new FileInfo(Path.Combine(App.AppExeDirectory.FullName, "system.db"));
        }

        public async void Initialize()
        {
            if (!_sysDbFileInfo.Exists)
            {
                _sysDb = new LiteDatabase(new ConnectionString("Filename=system.db"));
                _ = _sysDb.UserVersion;
            }
            _sysDb ??= new LiteDatabase(new ConnectionString("Filename=system.db"));

            if (!await CheckSystemDbConnStatus())
            {
                return;
            }

            var col = _sysDb.GetCollection<SystemConfigEntity>("sys_config");
            col.EnsureIndex(x => x.Key);
            var connOnAppStart = col.FindOne(x => x.Key == "conn_on_app_start");
            if (connOnAppStart is not null)
            {
                var flag = connOnAppStart.Value as bool?;
                if (flag == true)
                {
                    IsConnOnAppStart = true;
                    await OpenLastDbFilename();
                }
            }

            // Load history
            await RefreshHistory();
            initLoading = false;
        }

        public async void SetConnOnAppStart(bool flag)
        {
            if(!await CheckSystemDbConnStatus() || initLoading)
            {
                return;
            }
            var col = _sysDb?.GetCollection<SystemConfigEntity>("sys_config");
            var connOnAppStart = col?.FindOne(x => x.Key == "conn_on_app_start");
            connOnAppStart ??= new() 
            {
                Key = "conn_on_app_start",
            };
            connOnAppStart.Value = flag;
            col?.Upsert(connOnAppStart);
        }

        public async Task<bool> CheckSystemDbConnStatus()
        {
            if (_sysDb is null)
            {
                await MessageBox.Show($"Can't find system database file!", "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return false;
            }
            return true;
        }

        public void OnClosed()
        {
            _debugger?.Dispose();
        }

        [RelayCommand]
        private async Task ClearHistory()
        {
            IsBusy = true;
            try
            {
                if (!await CheckSystemDbConnStatus())
                {
                    return;
                }
                var col = _sysDb?.GetCollection<ConnectEntity>("sys_conn_history");
                col?.DeleteAll();
                await RefreshHistory();
            }
            catch(Exception ex)
            {
                await MessageBox.Show(ex.Message, "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ValidateHistory()
        {
            IsBusy = true;
            try
            {
                if (!await CheckSystemDbConnStatus())
                {
                    return;
                }
                var col = _sysDb?.GetCollection<ConnectEntity>("sys_conn_history");
                var list = col?.FindAll().ToList();
                if (list is null)
                {
                    return;
                }
                foreach (var item in list)
                {
                    if (!File.Exists(item.Filename))
                    {
                        col?.Delete(item.Id);
                    }
                }
                await RefreshHistory();
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message, "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshHistory()
        {
            IsBusy = true;
            try
            {
                if (!await CheckSystemDbConnStatus())
                {
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    HistoryItems.Clear();
                    HistoryItems.Add(new MenuItem
                    {
                        Header = "Clear Recent List",
                        Command = ClearHistoryCommand
                    });
                    HistoryItems.Add(new MenuItem
                    {
                        Header = "Validate Recent List",
                        Command = ValidateHistoryCommand
                    });
                });
                var col = _sysDb?.GetCollection<ConnectEntity>("sys_conn_history");
                var list = col?.FindAll().ToList();
                if (list is null || list.Count == 0)
                {
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    HistoryItems.Add(new Separator());
                });
                foreach (var p in list)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        HistoryItems.Add(new MenuItem
                        {
                            Header = p.Filename,
                            Command = OpenDbFilenameCommand,
                            CommandParameter = p,
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message, "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RunSql()
        {
            IsBusy = true;
            await CheckDbStatus();
            if (ActiveTabItem == null)
            {
                return;
            }
            var sqlTextEditor = ActiveTabItem?.Content as TextEditor;
            ActiveTask = ActiveTabItem?.Tag as TaskData;
            if (sqlTextEditor is null)
            {
                IsBusy = false;
                return;
            }
            if (ActiveTask is null)
            {
                IsBusy = false;
                return;
            }
            await ExecuteSqlAsync(sqlTextEditor.Text.Trim());
            await LoadResultAsync(ActiveTask);
            IsBusy = false;
        }

        private async Task CheckDbStatus()
        {
            bool flag = true;
            if (_db is null || IsDbConnected != true)
            {
                flag = false;
            }
            if (!flag)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsBusy = false;
                });
                await MessageBox.Show($"The database has not been opened!", "Error", MessageViewButtons.Ok);
                return;
            }
        }

        private async Task ExecuteSqlAsync(string sql)
        {
            if (this.ActiveTask is null)
            {
                return;
            }
            if (this.ActiveTask.Executing == false)
            {
                this.ActiveTask.Sql = sql;
            }
            var sw = new Stopwatch();
            sw.Start();

            ActiveTask.IsGridLoaded = ActiveTask.IsTextLoaded = ActiveTask.IsParametersLoaded = false;
            ActiveTask.Parameters = new BsonDocument();

            var sqlStringReader = new StringReader(ActiveTask.Sql);

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    while (sqlStringReader.Peek() >= 0 && _db != null)
                    {
                        using (var reader = _db.Execute(sqlStringReader, ActiveTask.Parameters))
                        {
                            ActiveTask.ReadResult(reader);
                        }
                    }
                });
                ActiveTask.Exception = null;
            }
            catch (Exception ex)
            {
                ActiveTask.Result = null;
                ActiveTask.Exception = ex;
            }
            finally
            {
                ActiveTask.Elapsed = sw.Elapsed;
                ActiveTask.Executing = false;
            }
            sw.Stop();
        }

        public async Task LoadResultAsync(TaskData data)
        {
            if (data == null)
            {
                return;
            }
            await Dispatcher.UIThread.InvokeAsync(() => 
            {
                ResultCountIsVisible = true;
                ResultCountText = data.Result == null ? "" :
                        data.Result.Count == 0 ? "no documents" :
                        data.Result.Count == 1 ? "1 document" :
                        data.Result.Count + (data.LimitExceeded ? "+" : "") + " documents";
                ResultElapsedText = data.Elapsed.ToString();
            });
            await ClearPageTaskDataResult();
            await Task.Factory.StartNew(() =>
            {
                if (data.Exception != null)
                {
                    GridBindErrorMessage(data.Sql, data.Exception);
                    TextBindErrorMessage(data.Sql, data.Exception);
                    ParametersBindErrorMessage(data);
                }
                else if (data.Result is not null)
                {
                    RefreshGridBindBsonData(data);
                    data.IsGridLoaded = true;

                    RefreshTextBindBsonData(data);
                    data.IsTextLoaded = true;

                    RefreshParametersBind(data);
                    data.IsParametersLoaded = true;
                }
            });
        }

        private void GridBindErrorMessage(string sql, Exception ex)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            GridItems ??= new();
            DataGrid? dg = null;
            Dispatcher.UIThread.Invoke(() =>
            {
                dg = App.MainWindow.FindControl<DataGrid>("dg");
            });
            if (dg is null)
            {
                return;
            }
            Dispatcher.UIThread.Invoke(() =>
            {
                dg.IsVisible = true;
                dg.Columns.Clear();
            });
            Dispatcher.UIThread.Invoke(() =>
            {
                DataTable dataTable = new DataTable();
                var col = dataTable.Columns.Add("err");
                dg.Columns.Add(new DataGridTextColumn
                {
                    Header = "Error",
                    FontSize = 12,
                    Binding = new Binding($"[{col}]"),
                    IsReadOnly = true,
                });
                var row = dataTable.NewRow();
                row[$"{col}"] = ex.Message;
                dataTable.Rows.Add(row);
                GridItems = ToExpando(dataTable);
            });
        }

        private void TextBindErrorMessage(string sql, Exception ex)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            TextEditor? txt_result = null;
            Dispatcher.UIThread.Invoke(() =>
            {
                txt_result = App.MainWindow.FindControl<TextEditor>("txt_result");
            });
            if (txt_result is null)
            {
                return;
            }
            var sb = new StringBuilder();

            if (!(ex is LiteException))
            {
                sb.AppendLine(ex.Message);
                sb.AppendLine();
                sb.AppendLine("===================================================");
                sb.AppendLine(ex.StackTrace);
            }
            else if (ex is LiteException lex)
            {
                sb.AppendLine(ex.Message);

                if (lex.ErrorCode == LiteException.UNEXPECTED_TOKEN && sql != null)
                {
                    var p = (int)lex.Position;
                    var start = (int)Math.Max(p - 30, 1) - 1;
                    var end = Math.Min(p + 15, sql.Length);
                    var length = end - start;

                    var str = sql.Substring(start, length).Replace('\n', ' ').Replace('\r', ' ');
                    var t = length - (end - p);

                    sb.AppendLine();
                    sb.AppendLine(str);
                    sb.AppendLine("".PadLeft(t, '-') + "^");
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                txt_result.SyntaxHighlighting = null;
                txt_result.Clear();
                txt_result.Text = sb.ToString();
            });
        }

        private void ParametersBindErrorMessage(TaskData data)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            TextEditor? txt_parameters = null;
            Dispatcher.UIThread.Invoke(() =>
            {
                txt_parameters = App.MainWindow.FindControl<TextEditor>("txt_parameters");
            });
            if (txt_parameters is null)
            {
                return;
            }
            Dispatcher.UIThread.Invoke(() =>
            {
                txt_parameters.Clear();
                txt_parameters.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
            });

            var sb = new StringBuilder();

            using (var writer = new StringWriter(sb))
            {
                var w = new JsonWriter(writer)
                {
                    Pretty = true,
                    Indent = 2
                };

                w.Serialize(data.Parameters ?? BsonValue.Null);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                txt_parameters.Text = sb.ToString();
            });
        }

        private void RefreshGridBindBsonData(TaskData data)
        {
            GridItems ??= new();
            if (App.MainWindow is null)
            {
                return;
            }
            DataGrid? dg = null;
            Dispatcher.UIThread.Invoke(() =>
            {
                dg = App.MainWindow.FindControl<DataGrid>("dg");
            });
            if (dg is null)
            {
                return;
            }
            Dispatcher.UIThread.Invoke(() =>
            {
                dg.IsVisible = false;
                dg.Columns.Clear();
            });
            DataTable dataTable = new DataTable();
            if (data.Result is not null && data.Result.Count > 0)
            {
                foreach (var value in data.Result)
                {
                    var doc = value.IsDocument ? value.AsDocument : new BsonDocument { ["[val]"] = value };

                    if (doc.Keys.Count == 0)
                        doc["[root]"] = "{}";

                    foreach (var key in doc.Keys)
                    {
                        var col = dataTable.Columns[key];

                        if (col is null)
                        {
                            col = value.IsDocument ? dataTable.Columns.Add(key) : dataTable.Columns.Add("val");
                            col.ReadOnly = key == "_id";
                        }
                    }
                    if (dg.Columns.Count == 0)
                    {
                        foreach (var col in dataTable.Columns)
                        {
                            Dispatcher.UIThread.Invoke(() =>
                            {
                                dg.Columns.Add(new DataGridTextColumn
                                {
                                    Header = value.IsDocument ? col : "[value]",
                                    FontSize = 12,
                                    Binding = value.IsDocument ? new Binding($"[{col}]") : new Binding($"[val]"),
                                    IsReadOnly = $"{col}" == "_id" ? true : false,
                                });
                            });
                        }
                    }

                    var row = dataTable.NewRow();

                    foreach (var key in doc.Keys)
                    {
                        if (value.IsDocument)
                        {
                            row[key] = value[key];
                        }
                        else
                        {
                            row["val"] = value;
                        }
                    }
                    dataTable.Rows.Add(row);
                }
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    dg.Columns.Add(new DataGridTextColumn
                    {
                        Header = "[no result]",
                        FontSize = 12,
                        IsReadOnly = true,
                    });
                });
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                GridItems = ToExpando(dataTable);
                dg.IsVisible = true;
                dg.HeadersVisibility = DataGridHeadersVisibility.Column;
            });
        }

        private ObservableCollection<Dictionary<string, object>> ToExpando(DataTable dt)
        {
            var list = new ObservableCollection<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                var exp = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                    exp[col.ColumnName] = row[col];
                list.Add(exp);
            }
            return list;
        }

        private void RefreshTextBindBsonData(TaskData data)
        {
            if (data is null || data.Result is null)
            {
                return;
            }
            if (App.MainWindow is null)
            {
                return;
            }
            TextEditor? txt_result = null;
            Dispatcher.UIThread.Invoke(() => 
            {
                txt_result = App.MainWindow.FindControl<TextEditor>("txt_result");
            });
            if (txt_result is null)
            {
                return;
            }
            var index = 0;
            var sb = new StringBuilder();

            using (var writer = new StringWriter(sb))
            {
                var json = new JsonWriter(writer)
                {
                    Pretty = true,
                    Indent = 2
                };

                if (data.Result.Count > 0)
                {
                    foreach (var value in data.Result)
                    {
                        if (data.Result?.Count > 1)
                        {
                            sb.AppendLine($"/* {index++ + 1} */");
                        }

                        json.Serialize(value);
                        sb.AppendLine();
                    }

                    if (data.LimitExceeded)
                    {
                        sb.AppendLine();
                        sb.AppendLine("/* Limit exceeded */");
                    }
                }
                else
                {
                    sb.AppendLine("no result");
                }
            }
            Dispatcher.UIThread.Invoke(() =>
            {
                txt_result.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
                txt_result.Text = sb.ToString();
            });
        }

        private void RefreshParametersBind(TaskData data)
        {
        }

        [RelayCommand]
        private async Task OpenLastDbFilename()
        {
            IsBusy = true;
            try
            {
                if (!await CheckSystemDbConnStatus())
                {
                    return;
                }
                var col = _sysDb?.GetCollection<SystemConfigEntity>("sys_config");
                var connOnAppStart = col?.FindOne(x => x.Key == "last_conn_id");
                connOnAppStart ??= new() 
                {
                    Key = "last_conn_id",
                };
                if (connOnAppStart.Value is null || connOnAppStart.Value is not int _id)
                {
                    await MessageBox.Show($"No database has been opened!", "Warning", MessageViewButtons.Ok, MessageViewIcons.Warning);
                    return;
                }
                var connCol = _sysDb?.GetCollection<ConnectEntity>("sys_conn_history");
                var lastConn = connCol?.FindOne(x => x.Id == _id);
                if (lastConn is not null)
                {
                    await OpenDbFilename(lastConn);
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message, "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenDbFilename(ConnectEntity? connect)
        {
            IsBusy = true;
            try
            {
                if (!await CheckSystemDbConnStatus())
                {
                    return;
                }
                if (_db is not null)
                {
                    _db.Dispose();
                    _db = null;
                }
                if (connect is null || !File.Exists(connect.Filename))
                {
                    return;
                }
                _filename = connect.Filename;
                var filename = $"Filename={connect.Filename}";
                var connection = $"Connection={(connect.Mode == ConnectionModes.Shared ? ConnectionModes.Shared : ConnectionModes.Direct)}";
                var password = $"Password={connect.Password}";
                var initialSize = $"InitialSize={connect.InitSizeMB}";
                var readOnly = $"ReadOnly={(connect.IsReadOnly == true ? true : false)}";
                var upgrade = $"Upgrade={(connect.IsUpgrade == true ? true : false)}";
                var connString = $"{filename};{connection};{password};{initialSize};{readOnly};{upgrade};";
                _db = new LiteDatabase(connString);
                _ = _db.UserVersion;
                IsDbConnected = true;
                await LoadTreeViewAsync();
                connect.LastTime = DateTime.Now;
                
                var connCol = _sysDb?.GetCollection<ConnectEntity>("sys_conn_history");
                connCol?.Upsert(connect);
                connect = connCol?.FindOne(x => x.Filename == connect.Filename);
                if (connect is not null && connect.Id > 0)
                {
                    var sysCol = _sysDb?.GetCollection<SystemConfigEntity>("sys_config");
                    var connOnAppStart = sysCol?.FindOne(x => x.Key == "last_conn_id");
                    connOnAppStart ??= new()
                    {
                        Key = "last_conn_id",
                    };
                    connOnAppStart.Value = connect.Id;
                    sysCol?.Upsert(connOnAppStart);
                }
                await RefreshHistory();
            }
            catch(Exception ex)
            {
                await MessageBox.Show(ex.Message, "Error", MessageViewButtons.Ok, MessageViewIcons.Error);
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectDb()
        {
            IsBusy = true;
            await ClearPageTaskDataResult();
            DbItems?.Clear();
            _queryNum = 1;
            if (App.MainWindow is not null)
            {
                TabControl? tab_querys = null;
                Dispatcher.UIThread.Invoke(() =>
                {
                    tab_querys = App.MainWindow.FindControl<TabControl>("tab_querys");
                    tab_querys?.Items?.Clear();
                });
            }
            _db?.Dispose();
            _db = null;
            IsDbConnected = false;
            IsBusy = false;
        }

        public async Task ClearPageTaskDataResult()
        {
            IsBusy = true;
            await Task.Delay(10);
            GridItems?.Clear();
            if (App.MainWindow is not null)
            {
                TextEditor? txt_result = null;
                TextEditor? txt_parameters = null;
                DataGrid? dg = null;
                Dispatcher.UIThread.Invoke(() =>
                {
                    txt_result = App.MainWindow.FindControl<TextEditor>("txt_result");
                    txt_parameters = App.MainWindow.FindControl<TextEditor>("txt_parameters");
                    dg = App.MainWindow.FindControl<DataGrid>("dg");
                    txt_result?.Clear();
                    txt_parameters?.Clear();
                    dg?.Columns?.Clear();
                    if (dg is not null)
                    {
                        dg.HeadersVisibility = DataGridHeadersVisibility.None;
                    }
                });
            }
            IsBusy = false;
        }

        [RelayCommand]
        private async Task LoadSql()
        {
            await CheckDbStatus();
            if (_db is null || IsDbConnected != true)
            {
                return;
            }
            IsBusy = true;
            await Task.Delay(10);
            if (App.MainWindow is null)
            {
                IsBusy = false;
                return;
            }
            var files = await App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select SQL file",
                FileTypeFilter = [new FilePickerFileType("SQL File")
                {
                    Patterns = new string[1] { "*.sql" },
                    AppleUniformTypeIdentifiers = new string[1] { "public.sql" },
                    MimeTypes = new[] { "application/sql", "text/x-sql" },
                }],
                AllowMultiple = false,
            });
            var filename = files.FirstOrDefault()?.Path.AbsolutePath;
            if (string.IsNullOrEmpty(filename) || string.IsNullOrWhiteSpace(filename) || !File.Exists(filename))
            {
                IsBusy = false;
                return;
            }
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = filename;
            var item = CreateTable(filename, ref task, true);
            if (item is null)
            {
                return;
            }
            ToolTip.SetTip(item, filename);
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
            IsBusy = false;
        }

        [RelayCommand]
        private async Task SaveSql()
        {
            await CheckDbStatus();
            if (ActiveTabItem is null)
            {
                return;
            }
            if (ActiveTabItem.Content is not TextEditor txt_sql)
            {
                return;
            }
            if (txt_sql is null)
            {
                return;
            }
            IsBusy = true;
            await Task.Delay(10);
            if (App.MainWindow is null)
            {
                IsBusy = false;
                return;
            }
            var file = await App.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Create SQL file",
                FileTypeChoices = [new FilePickerFileType("SQL File")
                {
                    Patterns = new string[1] { "*.sql" },
                    AppleUniformTypeIdentifiers = new string[1] { "public.sql" },
                    MimeTypes = new[] { "application/sql", "text/x-sql" },
                }],
            });
            if (file is null)
            {
                IsBusy = false;
                return;
            }
            // Open a writable file stream.
            await using var stream = await file.OpenWriteAsync();
            using var streamWriter = new StreamWriter(stream);
            // Write content to the file.
            await streamWriter.WriteAsync(txt_sql.Text);
            IsBusy = false;
        }

        [RelayCommand]
        private async Task Begin()
        {
            IsBusy = true;
            await CheckDbStatus();
            await ExecuteSqlAsync("BEGIN");
            IsBusy = false;
        }

        [RelayCommand]
        private async Task Commit()
        {
            IsBusy = true;
            await CheckDbStatus();
            await ExecuteSqlAsync("COMMIT");
            IsBusy = false;
        }

        [RelayCommand]
        private async Task Rollback()
        {
            IsBusy = true;
            await CheckDbStatus();
            await ExecuteSqlAsync("ROLLBACK");
            IsBusy = false;
        }

        [RelayCommand]
        private async Task Checkpoint()
        {
            IsBusy = true;
            await CheckDbStatus();
            await ExecuteSqlAsync("CHECKPOINT");
            IsBusy = false;
        }

        [RelayCommand]
        private async Task Debug()
        {
            IsBusy = true;
            await CheckDbStatus();
            if (_db is null)
            {
                return;
            }
            _debugger ??= new DatabaseDebugger(_db, new Random().Next(8000, 9000));
            _ = _debugger.Start();
            var url = $"http://localhost:{_debugger.Port}/";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true   // Must be true; otherwise it may try to execute directly.
                };
                Process.Start(psi);
            }
            catch
            {
            }
            IsBusy = false;
        }

        [RelayCommand]
        private async Task RefreshDb()
        {
            IsBusy = true;
            await CheckDbStatus();
            await LoadTreeViewAsync();
            IsBusy = false;
        }

        private async Task LoadTreeViewAsync()
        {
            if (DbItems is null || _db is null)
            {
                return;
            }
            await Task.Delay(10);
            // Multi-database is not supported yet; clear previous database tree.
            DbItems.Clear();
            var dbItem = GetDbNode();
            dbItem.Items.Add(GetSystemNode());
            GetTableNodes(_db).ForEach(x =>
            {
                dbItem.Items.Add(x);
            });
            DbItems.Add(dbItem);
        }

        private TreeViewItem GetDbNode()
        {
            var dbHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            dbHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/db_conn.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            dbHeader.Children.Add(new TextBlock()
            {
                Text = Path.GetFileName(_filename),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var menu = new ContextMenu();
            // Database Info MenuItem
            var dbInfoMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            dbInfoMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            dbInfoMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Database Info",
                VerticalAlignment = VerticalAlignment.Center
            });
            var dbInfoMenuItem = new MenuItem
            {
                Header = dbInfoMenuItemHeader,
                Command = CreateTableDatabaseInfoCommand,
            };
            // Import from JSON MenuItem
            var importJsonMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            importJsonMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            importJsonMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Import from JSON",
                VerticalAlignment = VerticalAlignment.Center
            });
            var importJsonMenuItem = new MenuItem
            {
                Header = importJsonMenuItemHeader,
                Command = CreateTableImportFromJsonCommand,
            };
            // Rebuild MenuItem
            var rebuildMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            rebuildMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            rebuildMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Rebuild",
                VerticalAlignment = VerticalAlignment.Center
            });
            var rebuildMenuItem = new MenuItem
            {
                Header = rebuildMenuItemHeader,
                Command = CreateTableRebuildCommand,
            };
            menu.Items.Add(dbInfoMenuItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(importJsonMenuItem);
            menu.Items.Add(rebuildMenuItem);
            var dbItem = new TreeViewItem
            {
                Header = dbHeader,
                Padding = new Thickness(0, 0, 0, 0),
                IsExpanded = true,
                ContextMenu = menu,
            };
            return dbItem;
        }

        private List<TreeViewItem> GetTableNodes(LiteDatabase db)
        {
            var items = new List<TreeViewItem>();
            db.GetCollectionNames().ToList().ForEach(x => 
            {
                items.Add(GetTableNode(x));
            });
            return items;
        }

        private TreeViewItem GetSystemNode()
        {
            var itemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            itemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/floder.svg",
                Width = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            itemHeader.Children.Add(new TextBlock()
            {
                Text = "System",
                VerticalAlignment = VerticalAlignment.Center,
            });
            var item = new TreeViewItem
            {
                Header = itemHeader,
                Padding = new Thickness(0, 0, 0, 0)
            };
            item.Items.Add(GetTableSetNode("$cols"));
            item.Items.Add(GetTableSetNode("$database"));
            item.Items.Add(GetTableSetNode("$dump"));
            item.Items.Add(GetTableSetNode("$file"));
            item.Items.Add(GetTableSetNode("$indexes"));
            item.Items.Add(GetTableSetNode("$open_cursors"));
            item.Items.Add(GetTableSetNode("$page_list"));
            item.Items.Add(GetTableSetNode("$query"));
            item.Items.Add(GetTableSetNode("$sequences"));
            item.Items.Add(GetTableSetNode("$snapshots"));
            item.Items.Add(GetTableSetNode("$transactions"));
            return item;
        }

        private TreeViewItem GetTableNode(string title)
        {
            var itemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            itemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            itemHeader.Children.Add(new TextBlock()
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center
            });
            var item = new TreeViewItem
            {
                Header = itemHeader,
                Padding = new Thickness(0, 0, 0, 0),
                ContextMenu = GetTableNodeContextMenu(title)
            };
            item.DoubleTapped += (sender, args) => 
            {
                CreateTableQuery(title);
            };
            return item;
        }

        private ContextMenu GetTableNodeContextMenu(string table)
        {
            var menu = new ContextMenu();
            // Query MenuItem
            var queryMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            queryMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            queryMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Query",
                VerticalAlignment = VerticalAlignment.Center
            });
            var queryMenuItem = new MenuItem 
            {
                Header = queryMenuItemHeader,
                Command = CreateTableQueryCommand,
                CommandParameter = table
            };
            // Count MenuItem
            var countMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            countMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            countMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Count",
                VerticalAlignment = VerticalAlignment.Center
            });
            var countMenuItem = new MenuItem 
            {
                Header = countMenuItemHeader,
                Command = CreateTableCountCommand,
                CommandParameter = table
            };
            // Explain MenuItem
            var explainMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            explainMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            explainMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Explain",
                VerticalAlignment = VerticalAlignment.Center
            });
            var explainMenuItem = new MenuItem 
            {
                Header = explainMenuItemHeader,
                Command = CreateTableExplainCommand,
                CommandParameter = table
            };
            // Indexes MenuItem
            var indexesMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            indexesMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            indexesMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Indexes",
                VerticalAlignment = VerticalAlignment.Center
            });
            var indexesMenuItem = new MenuItem 
            {
                Header = indexesMenuItemHeader,
                Command = createTableIndexesCommand,
                CommandParameter = table
            };
            // ExportJson MenuItem
            var exportJsonMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            exportJsonMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            exportJsonMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Export to JSON",
                VerticalAlignment = VerticalAlignment.Center
            });
            var exportJsonMenuItem = new MenuItem 
            {
                Header = exportJsonMenuItemHeader,
                Command = CreateTableExportCommand,
                CommandParameter = table
            };
            // Analyze MenuItem
            var analyzeMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            analyzeMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            analyzeMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Analyze",
                VerticalAlignment = VerticalAlignment.Center
            });
            var analyzeMenuItem = new MenuItem 
            {
                Header = analyzeMenuItemHeader,
                Command = CreateTableAnalyzeCommand,
                CommandParameter = table
            };
            // Rename MenuItem
            var renameMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            renameMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            renameMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Rename",
                VerticalAlignment = VerticalAlignment.Center
            });
            var renameMenuItem = new MenuItem 
            {
                Header = renameMenuItemHeader,
                Command = CreateTableRenameCommand,
                CommandParameter = table
            };
            // Drop MenuItem
            var dropMenuItemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            dropMenuItemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            dropMenuItemHeader.Children.Add(new TextBlock()
            {
                Text = "Drop",
                VerticalAlignment = VerticalAlignment.Center
            });
            var dropMenuItem = new MenuItem 
            {
                Header = dropMenuItemHeader,
                Command = CreateTableDropCommand,
                CommandParameter = table
            };
            menu.Items.Add(queryMenuItem);
            menu.Items.Add(countMenuItem);
            menu.Items.Add(explainMenuItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(indexesMenuItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exportJsonMenuItem);
            menu.Items.Add(analyzeMenuItem);
            menu.Items.Add(renameMenuItem);
            menu.Items.Add(dropMenuItem);
            return menu;
        }

        private TreeViewItem GetTableSetNode(string title)
        {
            var itemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            itemHeader.Children.Add(new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
            {
                Path = "avares://LiteDB.Studio.Avalonia/Assets/table-set.svg",
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            itemHeader.Children.Add(new TextBlock()
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center
            });
            var item = new TreeViewItem
            {
                Header = itemHeader,
                Padding = new Thickness(0, 0, 0, 0)
            };
            return item;
        }

        [RelayCommand]
        public void CreateTableConsole()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"";
            var item = CreateTable(null, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableRebuild()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"REBUILD {{collation: 'en-US/IgnoreCase',  password: 'newpassword' }};";
            var item = CreateTable(null, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableDatabaseInfo()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT $ FROM $database;";
            var item = CreateTable(null, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableImportFromJson()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT ${Environment.NewLine}  INTO new_col{Environment.NewLine}  FROM $file('C:/temp/file.json');";
            var item = CreateTable(null, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableQuery(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT $ FROM {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableCount(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT COUNT(*) FROM {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableExplain(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"EXPLAIN SELECT $ FROM {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableIndexes(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT $ FROM $indexes WHERE collection = \"{table}\";";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableExport(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"SELECT $" +
                $"{Environment.NewLine}  INTO $file('{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{table}.json")}')".Replace('\\', '/') +
                $"{Environment.NewLine}  FROM {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableAnalyze(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"ANALYZE {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableRename(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"RENAME COLLECTION {table} TO new_name;";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        [RelayCommand]
        public void CreateTableDrop(object table)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            var task = new TaskData();
            task.Filename = null;
            task.Sql = $"DROP COLLECTION {table};";
            var item = CreateTable(table, ref task);
            if (item is null)
            {
                return;
            }
            tabControl.Items.Add(item);
            tabControl.SelectedItem = item;
        }

        private TabItem? CreateTable(object? table, ref TaskData? task, bool is_sqlfile = false)
        {
            if (task is null)
            {
                return null;
            }
            task.Id = _queryNum++;
            var item = new TabItem
            {
                MinHeight = 26,
                Padding = new Thickness(8, 0),
                Tag = task
            };
            var itemHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            var name = (task.Filename == null ? "Query " + task.Id : Path.GetFileName(task.Filename));
            itemHeader.Children.Add(new TextBlock
            {
                Name = $"{name}",
                Text = $"{name}",
                FontSize = 12,
            });
            var itemHeaderCloseButton = new Button
            {
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(0, 0, 0, 0),
                Background = Brushes.Transparent,
                Content = new SvgImage(new Uri("avares://LiteDB.Studio.Avalonia/"))
                {
                    Path = "avares://LiteDB.Studio.Avalonia/Assets/close.svg",
                    Width = 14,
                },
                Command = CloseQueryTableCommand,
                CommandParameter = task
            };
            itemHeader.Children.Add(itemHeaderCloseButton);
            var itemHeaderMenu = new ContextMenu();
            itemHeaderMenu.Items.Add(new MenuItem
            {
                Header = "Close All",
                Command = CloseAllQueryTableCommand,
            });
            itemHeaderMenu.Items.Add(new MenuItem
            {
                Header = "Close Other",
                Command = CloseOtherQueryTableCommand,
                CommandParameter = table
            });
            itemHeader.ContextMenu = itemHeaderMenu;
            item.Header = itemHeader;
            var txt_sql = new TextEditor()
            {
                Padding = new Thickness(2, 2, 2, 0),
                BorderBrush = new SolidColorBrush(Color.Parse("#CFCFCF")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                FontFamily = "Consolas",
                ShowLineNumbers = true,
            };
            if (is_sqlfile)
            {
                if (table is not null && table is string file)
                {
                    txt_sql.Text = File.ReadAllText((string)table);
                }
            }
            else if (!string.IsNullOrEmpty(task.Sql) && !string.IsNullOrWhiteSpace(task.Sql))
            {
                txt_sql.Text = task.Sql.Trim();
            }
            var uri = new Uri("avares://LiteDB.Studio.Avalonia/Assets/sql.xshd");
            using (var stream = AssetLoader.Open(uri))
            using (var reader = new XmlTextReader(stream))
            {
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                txt_sql.SyntaxHighlighting = highlighting;
            }

            txt_sql.TextArea.Caret.PositionChanged += (s, e) =>
            {
                LineNum = txt_sql.TextArea.Caret.Line;
                ColumnNum = txt_sql.TextArea.Caret.Column;
            };

            txt_sql.TextArea.TextEntering += TextArea_TextEntering;
            txt_sql.TextArea.AddHandler(
                InputElement.KeyDownEvent,
                TextArea_KeyDown,
                RoutingStrategies.Tunnel
            );
            item.Content = txt_sql;
            return item;
        }

        private void TextArea_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextArea txt_sql)
            {
                return;
            }
            if (e.Key == Key.Back && completionWindow != null)
            {
                int caretOffset = txt_sql.Caret.Offset;
                string result = FindCaretOffsetTextWord(txt_sql.Document.Text, string.Empty, caretOffset);
                if (result is not null && result.Length > 0)
                {
                    completionWindow.Tag = result.Substring(0, result.Length - 1);
                }
                RefreshCompletionWindow();
            }
        }

        private void TextArea_TextEntering(object? sender, global::Avalonia.Input.TextInputEventArgs e)
        {
            if (sender is not TextArea txt_sql)
            {
                return;
            }
            if (e.Text is not null && e.Text.Length > 0 && e.Text[0] == ' ')
            {
                if (completionWindow != null)
                {
                    completionWindow.Tag = string.Empty;
                    completionWindow.Close();
                }
                return;
            }
            int caretOffset = txt_sql.Caret.Offset;
            string result = FindCaretOffsetTextWord(txt_sql.Document.Text, e.Text ?? string.Empty, caretOffset);
            char preChar = ' ';
            if (caretOffset > 0)
            {
                preChar = txt_sql.Document.GetCharAt(caretOffset - 1);
            }
            if (txt_sql.Selection.Length != 0)
            {
                result = e.Text ?? string.Empty;
            }
            if (result is not null && result.Length > 0)
            {
                if (completionWindow == null && !char.IsLetter(preChar))
                {
                    completionWindow ??= new CompletionWindow(txt_sql)
                    {
                    };
                    completionWindow.Closed += delegate {
                        completionWindow = null;
                    };
                    completionWindow.TextArea.KeyDown += completionWindowTextArea_KeyDown;
                }
                if (completionWindow != null)
                {
                    completionWindow.Show();
                    completionWindow.Tag = result;
                }
                RefreshCompletionWindow();
            }
        }

        private string FindCaretOffsetTextWord(string text, string m, int offset)
        {
            if ((string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text)) && (string.IsNullOrEmpty(m) || string.IsNullOrWhiteSpace(m)))
            {
                return string.Empty;
            }
            var len = text.Length;
            StringBuilder word = new StringBuilder();
            for (int i = offset; i > 0; i--)
            {
                var @char = text[i - 1];
                if ((@char >= 'a' && @char <= 'z') || (@char >= 'A' && @char <= 'Z'))
                {
                    word.Insert(0, @char);
                }
                else
                {
                    break;
                }
            }
            if (!string.IsNullOrEmpty(m) && !string.IsNullOrWhiteSpace(m))
            {
                if ((m[0] >= 'a' && m[0] <= 'z') || (m[0] >= 'A' && m[0] <= 'Z'))
                {
                    word.Append(m);
                }
            }
            return word.ToString();
        }

        private void completionWindowTextArea_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                e.Handled = true;
            }
        }

        private void RefreshCompletionWindow()
        {
            if (completionWindow != null)
            {
                var result = completionWindow.Tag as string;
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                data.Clear();
                if (!string.IsNullOrEmpty(result))
                {
                    sqlWords.Where(x => x.Text.StartsWith(result, StringComparison.OrdinalIgnoreCase))
                        .ToList()
                        .ForEach(data.Add);
                }
            }
        }

        [RelayCommand]
        public void CloseAllQueryTable()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            if (tabControl.Items.Count == 0) 
            {
                return;
            }
            tabControl.Items.Clear();
        }

        [RelayCommand]
        public void CloseQueryTable(object tag)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            if (tabControl.Items.Count == 0) 
            {
                return;
            }
            TabItem? item = null;
            foreach (var tabItem in tabControl.Items)
            {
                if (tabItem is null)
                {
                    continue;
                }
                if (tabItem is not TabItem _item)
                {
                    continue;
                }
                if (_item.Tag == tag)
                {
                    item = _item; 
                    break;
                }
            }
            if (item is null)
            {
                return;
            }
            tabControl.Items.Remove(item);
        }

        [RelayCommand]
        public void CloseOtherQueryTable(object tag)
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var tabControl = App.MainWindow.FindControl<TabControl>("tab_querys");
            if (tabControl is null)
            {
                return;
            }
            if (tabControl.Items.Count == 0) 
            {
                return;
            }
            int index = -1;
            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                var tabItem = tabControl.Items[i];
                if (tabItem is null)
                {
                    continue;
                }
                if (tabItem is not TabItem _item)
                {
                    continue;
                }
                if (_item.Tag == tag)
                {
                    index = i; 
                    break;
                }
            }
            if (index == -1)
            {
                return;
            }
            for (int i = 0; i < index; i++)
            {
                tabControl.Items.RemoveAt(0);
            }
            var count = tabControl.Items.Count;
            for (int i = 0; i < count - 1; i++)
            {
                tabControl.Items.RemoveAt(1);
            }
        }

        [RelayCommand]
        public async Task ShowConnectionView()
        {
            IsBusy = true;
            if (App.MainWindow is null)
            {
                IsBusy = false;
                return;
            }
            DialogWindow window = new DialogWindow(new ConnectionViewModel 
            {
                Title = "Connection Manager",
                Width = 600,
                Height = 500,
            });
            var conn = await window.ShowDialog<ConnectEntity>();
            if (conn == null)
            {
                IsBusy = false;
                return;
            }
            await OpenDbFilename(conn);
            IsBusy = false;
        }

        [RelayCommand]
        public async Task ShowAbout()
        {
            IsBusy = true;
            if (App.MainWindow is null)
            {
                IsBusy = false;
                return;
            }
            DialogWindow window = new DialogWindow(new AboutViewModel
            {
                Title = "About",
                Width = 600,
                Height = 500,
            });
            await window.ShowDialog();
            IsBusy = false;
        }
    }
}
