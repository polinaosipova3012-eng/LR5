using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Lesson_Link
{
    /// <summary>
    /// Логика взаимодействия для TeacherGroupsView.xaml
    /// </summary>
    public partial class TeacherGroupsView : Window
    {
        private readonly User currentUser;

        public TeacherGroupsView(User user)
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            LoadGroups();
        }

        private void LoadGroups()
        {
            try
            {
                var groups = new List<TeacherGroupRow>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT g.Id, g.Name, ld.Name, esd.Name, g.CreatedDate, g.LevelId, g.EnrollmentStatusId,
                               g.DirectionId, g.AgeGroupId, g.LessonFormatId,
                               dd.Name, ag.Name, lf.Name, g.IsActive,
                               g.MaxStudents, g.CurrentStudents, g.PricePerMonth, g.Room, g.Description,
                               (SELECT COUNT(*) FROM dbo.[StudentGroup] sg
                                WHERE sg.GroupId = g.Id AND sg.OrganizationId = g.OrganizationId
                                  AND sg.LeaveDate IS NULL) AS EnrolledCount
                        FROM dbo.[Group] g
                        INNER JOIN dbo.LevelDict ld ON g.LevelId = ld.Id
                        INNER JOIN dbo.EnrollmentStatusDict esd ON g.EnrollmentStatusId = esd.Id
                        INNER JOIN dbo.DirectionDict dd ON g.DirectionId = dd.Id
                        INNER JOIN dbo.AgeGroupDict ag ON g.AgeGroupId = ag.Id
                        INNER JOIN dbo.LessonFormatDict lf ON g.LessonFormatId = lf.Id
                        INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId
                            AND tg.OrganizationId = g.OrganizationId
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                        ORDER BY g.Name", connection))
                    {
                        command.Parameters.AddWithValue("@UserId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                groups.Add(MapGroupRow(reader));
                            }
                        }
                    }
                }

                GroupsDataGrid.ItemsSource = groups;
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                LoadGroupsLegacy();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки групп: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGroupsLegacy()
        {
            try
            {
                var groups = new List<TeacherGroupRow>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT g.Id, g.Name, ld.Name, esd.Name, g.CreatedDate,
                               g.MaxStudents, g.CurrentStudents,
                               (SELECT COUNT(*) FROM dbo.[StudentGroup] sg
                                WHERE sg.GroupId = g.Id AND sg.LeaveDate IS NULL) AS EnrolledCount
                        FROM dbo.[Group] g
                        INNER JOIN dbo.LevelDict ld ON g.LevelId = ld.Id
                        INNER JOIN dbo.EnrollmentStatusDict esd ON g.EnrollmentStatusId = esd.Id
                        INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                        ORDER BY g.Name", connection))
                    {
                        command.Parameters.AddWithValue("@UserId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                groups.Add(new TeacherGroupRow
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Level = reader.GetString(2),
                                    EnrollmentStatus = reader.GetString(3),
                                    CreatedDate = reader.GetDateTime(4),
                                    MaxStudents = reader.IsDBNull(5) ? 15 : reader.GetInt32(5),
                                    CurrentStudents = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                    EnrolledCount = reader.GetInt32(7),
                                    IsActive = true
                                });
                            }
                        }
                    }
                }

                GroupsDataGrid.ItemsSource = groups;
                MessageBox.Show(
                    "В таблице dbo.[Group] не хватает столбцов. Выполните SchemaExtensions.sql для полного отображения.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки групп: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static TeacherGroupRow MapGroupRow(SqlDataReader reader)
        {
            return new TeacherGroupRow
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Level = reader.GetString(2),
                EnrollmentStatus = reader.GetString(3),
                CreatedDate = reader.GetDateTime(4),
                LevelId = reader.GetInt32(5),
                EnrollmentStatusId = reader.GetInt32(6),
                DirectionId = reader.GetInt32(7),
                AgeGroupId = reader.GetInt32(8),
                LessonFormatId = reader.GetInt32(9),
                DirectionName = reader.GetString(10),
                AgeGroupName = reader.GetString(11),
                LessonFormatName = reader.GetString(12),
                IsActive = reader.IsDBNull(13) ? true : reader.GetBoolean(13),
                MaxStudents = reader.IsDBNull(14) ? 15 : reader.GetInt32(14),
                CurrentStudents = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                PricePerMonth = reader.IsDBNull(16) ? (decimal?)null : reader.GetDecimal(16),
                Room = reader.IsDBNull(17) ? null : reader.GetString(17),
                Description = reader.IsDBNull(18) ? null : reader.GetString(18),
                EnrolledCount = reader.GetInt32(19)
            };
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = new TeacherDashboardView(currentUser);
            dashboard.Show();
            this.Close();
        }

        public class TeacherGroupRow : Group
        {
            public int EnrolledCount { get; set; }
        }
    }
}
