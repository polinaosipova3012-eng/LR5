using Lesson_Link.Helpers;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            EventManager.RegisterClassHandler(
                typeof(DataGrid),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(DataGridColumnHelper.OnDataGridLoaded));

            base.OnStartup(e);
        }
    }
}
