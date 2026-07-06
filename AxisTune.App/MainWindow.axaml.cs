using Avalonia.Controls;
using Avalonia.Threading;
using AxisTune.App.ViewModels;

namespace AxisTune.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ScrollToRequested += OnScrollToRequested;
    }

    /// <summary>게임패드 다이어그램 클릭으로 강조된 항목을 해당 탭의 리스트에서 스크롤해 보여준다.</summary>
    private void OnScrollToRequested(object target)
    {
        // 탭 전환 직후일 수 있으므로 레이아웃이 자리 잡은 뒤 스크롤한다.
        Dispatcher.UIThread.Post(() =>
        {
            switch (target)
            {
                case AxisTuningViewModel:
                    this.FindControl<ItemsControl>("ChannelsList")?.ScrollIntoView(target);
                    break;
                case BindingRowViewModel row:
                    var list = row.IsAxis
                        ? this.FindControl<ItemsControl>("AxisRowsList")
                        : this.FindControl<ItemsControl>("ButtonRowsList");
                    list?.ScrollIntoView(target);
                    break;
            }
        }, DispatcherPriority.Loaded);
    }
}
