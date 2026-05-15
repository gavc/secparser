using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecParser.UI.ViewModels
{
    public partial class UserFilterItem : ObservableObject
    {
        public Action? SelectionChangedCallback { get; set; }

        [ObservableProperty]
        private string userName = string.Empty;

        [ObservableProperty]
        private bool isSelected;
        
        partial void OnIsSelectedChanged(bool value)
        {
            SelectionChangedCallback?.Invoke();
        }
    }
}