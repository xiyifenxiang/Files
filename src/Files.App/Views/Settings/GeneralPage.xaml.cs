// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.Views.Settings
{
	public sealed partial class GeneralPage : Page
	{

		public GeneralPage()
		{
			InitializeComponent();
			Loaded += (_, _) => ViewModel.StartListening();
			Unloaded += (_, _) => ViewModel.StopListening();
		}

		private void RemoveStartupPage(object sender, RoutedEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: PageOnStartupViewModel page })
				ViewModel.RemovePageCommand.Execute(page);
		}

		private void RemoveShellExtensionClick(object sender, RoutedEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: ShellExtensionOptionViewModel option })
				ViewModel.RemoveShellExtensionCommand.Execute(option);
		}

		private void ShellExtensionListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
		{
			if (args.InRecycleQueue || args.Item is not ShellExtensionOptionViewModel option)
				return;

			AutomationProperties.SetName(args.ItemContainer, option.Name);
		}

		private Visibility HasShellExtensionCandidatesToVisibility(bool hasCandidates)
			=> hasCandidates ? Visibility.Collapsed : Visibility.Visible;
	}
}
