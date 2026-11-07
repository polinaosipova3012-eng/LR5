using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Windows;

namespace Lesson_Link
{
    /// <summary>
    /// Логика взаимодействия для TeacherDashboardView.xaml
    /// </summary>
    public partial class TeacherDashboardView : Window
    {
        private User currentUser;

        public TeacherDashboardView(User user)
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            WelcomeTextBlock.Text = $"Добро пожаловать, {user.FirstName} {user.LastName}!";
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    // Количество групп преподавателя
                    var groupsCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.[TeacherGroup] tg
                        INNER JOIN dbo.[Group] g ON tg.GroupId = g.Id
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId", connection);
                    groupsCmd.Parameters.AddWithValue("@UserId", currentUser.Id);
                    groupsCmd.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);
                    GroupsCount.Text = groupsCmd.ExecuteScalar().ToString();

                    // Количество занятий преподавателя (через группы)
                    var scheduleCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.[Schedule] s
                        INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                        INNER JOIN dbo.[Group] g ON s.GroupId = g.Id
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                          AND s.OrganizationId = @OrgId", connection);
                    scheduleCmd.Parameters.AddWithValue("@UserId", currentUser.Id);
                    scheduleCmd.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);
                    ScheduleCount.Text = scheduleCmd.ExecuteScalar().ToString();

                    // Количество учеников преподавателя (через группы)
                    var studentsCmd = new SqlCommand(@"
                        SELECT COUNT(DISTINCT sg.StudentId) FROM dbo.[StudentGroup] sg
                        INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                        INNER JOIN dbo.[Student] s ON sg.StudentId = s.Id
                        INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
                        WHERE tg.UserId = @UserId AND sg.LeaveDate IS NULL
                          AND s.OrganizationId = @OrgId AND g.OrganizationId = @OrgId", connection);
                    studentsCmd.Parameters.AddWithValue("@UserId", currentUser.Id);
                    studentsCmd.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);
                    StudentsCount.Text = studentsCmd.ExecuteScalar().ToString();

                    // Количество материалов в группах преподавателя
                    var materialsCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.[Material] m
                        INNER JOIN dbo.[TeacherGroup] tg ON m.GroupId = tg.GroupId
                        INNER JOIN dbo.[Group] g ON m.GroupId = g.Id
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                          AND m.OrganizationId = @OrgId", connection);
                    materialsCmd.Parameters.AddWithValue("@UserId", currentUser.Id);
                    materialsCmd.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);
                    MaterialsCount.Text = materialsCmd.ExecuteScalar().ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var view = new TeacherProfileView(currentUser);
            view.Show();
            this.Close();
        }

        private void GroupsButton_Click(object sender, RoutedEventArgs e)
        {
            var view = new TeacherGroupsView(currentUser);
            view.Show();
            this.Close();
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            var view = new TeacherScheduleView(currentUser);
            view.Show();
            this.Close();
        }

        private void StudentsButton_Click(object sender, RoutedEventArgs e)
        {
            var view = new TeacherStudentsView(currentUser);
            view.Show();
            this.Close();
        }

        private void MaterialsButton_Click(object sender, RoutedEventArgs e)
        {
            var view = new TeacherMaterialsView(currentUser);
            view.Show();
            this.Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            AppSession.Clear();
            new MainWindow().Show();
            Close();
        }

        private void AttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            new TeacherAttendanceWindow(currentUser).ShowDialog();
        }

        private void GradesButton_Click(object sender, RoutedEventArgs e)
        {
            new TeacherGradesWindow(currentUser).ShowDialog();
        }
    }
}
