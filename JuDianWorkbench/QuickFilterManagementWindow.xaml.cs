using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JuDianWorkbench.Models;
using JuDianWorkbench.ViewModels;

namespace JuDianWorkbench;

public partial class QuickFilterManagementWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly string _module;
    private Point _dragStart;
    private QuickFilterRecord? _draggedFilter;
    private ObservableCollection<QuickFilterRecord> Filters { get; }

    public QuickFilterManagementWindow(MainViewModel viewModel, string module, Window owner)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _module = module;
        Owner = owner;
        Filters = new(viewModel.GetQuickFilterLayout(module));
        DataContext = Filters;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (FilterList.SelectedItem is not QuickFilterRecord filter) return;
        var oldIndex = Filters.IndexOf(filter);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= Filters.Count) return;
        Filters.Move(oldIndex, newIndex);
        FilterList.SelectedItem = filter;
    }

    private void FilterList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(FilterList);
        _draggedFilter = FindItem(e.OriginalSource as DependencyObject)?.DataContext as QuickFilterRecord;
    }

    private void FilterList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedFilter is null) return;
        var point = e.GetPosition(FilterList);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(FilterList, _draggedFilter, DragDropEffects.Move);
    }

    private void FilterList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(QuickFilterRecord)) is not QuickFilterRecord source) return;
        var target = FindItem(e.OriginalSource as DependencyObject)?.DataContext as QuickFilterRecord;
        var oldIndex = Filters.IndexOf(source);
        var newIndex = target is null ? Filters.Count - 1 : Filters.IndexOf(target);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex) Filters.Move(oldIndex, newIndex);
        _draggedFilter = null;
    }

    private static ListBoxItem? FindItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem item) return item;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveQuickFilterLayout(_module, Filters);
        DialogResult = true;
    }
}
