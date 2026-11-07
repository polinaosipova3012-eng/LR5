using System.Windows;

namespace Lesson_Link
{
    /// <summary>
    /// Возврат с встроенных экранов админки на главную панель (без нового окна).
    /// Если контрол показан в отдельном окне (например, учитель), закрывает это окно.
    /// </summary>
    public static class AdminNavigationHelper
    {
        public static void NavigateToAdminHome(DependencyObject view)
        {
            var w = Window.GetWindow(view);
            if (w is AdminDashboardView dash)
            {
                dash.ShowDashboardHome();
                return;
            }
            w?.Close();
        }
    }
}
