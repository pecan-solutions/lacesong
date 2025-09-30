using CommunityToolkit.Mvvm.ComponentModel;
using Lacesong.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;

namespace Lacesong.Avalonia.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    public HomeViewModel(ILogger<HomeViewModel> logger) : base(logger)
    {
    }
}
