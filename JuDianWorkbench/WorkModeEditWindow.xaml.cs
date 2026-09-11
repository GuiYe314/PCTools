using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;

namespace JuDianWorkbench;

public partial class WorkModeEditWindow : Window
{
    public IReadOnlyList<ProgramRecord> Programs { get; }
    public int[] DelayOptions => WorkModeService.DelayOptions;

    public WorkModeEditWindow(WorkModeRecord mode, IReadOnlyList<ProgramRecord> programs, Window owner, bool isNew)
    {
        Programs = programs;
        InitializeComponent();
        Owner = owner;
        DataContext = mode;
        DialogTitle.Text = isNew ? "新建工作模式" : "编辑工作模式";
        Title = DialogTitle.Text;
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkModeRecord mode || AvailableProgramList.SelectedItem is not ProgramRecord program) return;
        if (mode.Steps.Any(x => x.ProgramId == program.Id))
        {
            MessageBox.Show(this, "这个启动项已经在当前模式中。", "工作模式", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var step = new WorkModeStep { ProgramId = program.Id, ProgramName = program.Name };
        mode.Steps.Add(step);
        StepList.SelectedItem = step;
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkModeRecord mode || StepList.SelectedItem is not WorkModeStep step) return;
        mode.Steps.Remove(step);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void Delay_SelectionChanged(object sender, SelectionChangedEventArgs e) => StepList?.Items.Refresh();

    private void MoveSelected(int offset)
    {
        if (DataContext is not WorkModeRecord mode || StepList.SelectedItem is not WorkModeStep step) return;
        var oldIndex = mode.Steps.IndexOf(step);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= mode.Steps.Count) return;
        mode.Steps.Move(oldIndex, newIndex);
        StepList.SelectedItem = step;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkModeRecord mode) return;
        var error = WorkModeService.ValidateMode(mode, Programs);
        if (error is not null) { MessageBox.Show(this, error, "工作模式", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DialogResult = true;
    }
}
