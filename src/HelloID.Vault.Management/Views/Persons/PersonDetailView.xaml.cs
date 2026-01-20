using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Management.Views.Persons;
using HelloID.Vault.Management.ViewModels.Persons;
using Microsoft.Extensions.DependencyInjection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.Views.Persons;

/// <summary>
/// Interaction logic for PersonDetailView.xaml
/// </summary>
public partial class PersonDetailView : UserControl
{
    private PersonDetailViewModel? _viewModel;

    public PersonDetailView()
    {
        InitializeComponent();
    }

    public PersonDetailView(PersonDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
    }

}
