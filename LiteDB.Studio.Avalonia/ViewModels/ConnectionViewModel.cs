using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB.Studio.Avalonia.Entities;
using LiteDB.Studio.Avalonia.Enums;
using LiteDB.Studio.Avalonia.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LiteDB.Studio.Avalonia.ViewModels
{
    public partial class ConnectionViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private CRUDFlag _type = CRUDFlag.Create;

        [ObservableProperty]
        private ConnectionModel _model;

        [ObservableProperty]
        private List<string> _cultureTypes;

        [ObservableProperty]
        private List<string> _compareOptions;

        public ConnectionViewModel()
        {
            _model = new();
            _cultureTypes = CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .Select(x => x.LCID)
                .Distinct()
                .Where(x => x != 4096)
                .Select(x => CultureInfo.GetCultureInfo(x).Name)
                .ToList();
            _compareOptions = ["", .. Enum.GetNames(typeof(CompareOptions)).Cast<string>()];
        }

        [RelayCommand]
        private void Ok()
        {
            if (Model is null || Model.Item is null)
            {
                Close();
                return;
            }
            var result = new ConnectEntity
            {
                Filename = Model.Item.Filename,
                IsReadOnly = Model.Item.IsReadOnly,
                IsUpgrade = Model.Item.IsUpgrade,
                Password = Model.Item.Password,
                Mode = Model.Item.IsDirect == true? ConnectionModes.Direct : ConnectionModes.Shared,
                InitSizeMB = Model.Item.InitSizeMB,
                CultureType = Model.Item.CultureType,
                CompareOption = Model.Item.CompareOption,
                LastTime = DateTime.Now
            };
            Close(result);
        }

        [RelayCommand]
        public async Task ShowFilePicker()
        {
            if (App.MainWindow is null)
            {
                return;
            }
            var files = await App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select a file",
                FileTypeFilter = [FilePickerFileTypes.All],
                AllowMultiple = false,
            });
            if (Model is null || Model.Item is null)
            {
                return;
            }
            var file = files.FirstOrDefault();
            if (file is null || !File.Exists(file.TryGetLocalPath()))
            {
                return;
            }
            Model.Item.Filename = file.TryGetLocalPath();
        }
    }
}
