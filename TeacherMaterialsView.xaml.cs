using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Lesson_Link
{
    public partial class TeacherMaterialsView : Window
    {
        private readonly User _teacher;
        private Material _selectedMaterial;

        public TeacherMaterialsView(User user)
        {
            _teacher = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            LoadMaterialsTree();
        }

        private void LoadMaterialsTree()
        {
            try
            {
                LoadMaterialsTreeFull();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                LoadMaterialsTreeLegacy();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки материалов: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMaterialsTreeFull()
        {
            var materialsByGroup = new Dictionary<int, List<Material>>();

            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
                    SELECT m.Id, m.GroupId, m.OrganizationId, m.Title, m.Description, m.FilePath,
                           m.FileTypeId,
                           ISNULL(NULLIF(LTRIM(RTRIM(ft.Extension)), ''), ft.Name) AS FileTypeLabel,
                           m.IsRequired, m.UploadDate, m.UploadedByUserId, g.Name,
                           u.FirstName + ' ' + u.LastName AS UploadedByUserName
                    FROM dbo.[Material] m
                    INNER JOIN dbo.[Group] g ON m.GroupId = g.Id AND g.OrganizationId = m.OrganizationId
                    INNER JOIN dbo.FileTypeDict ft ON m.FileTypeId = ft.Id
                    INNER JOIN dbo.[TeacherGroup] tg ON m.GroupId = tg.GroupId
                        AND tg.OrganizationId = m.OrganizationId
                    LEFT JOIN dbo.[User] u ON m.UploadedByUserId = u.Id
                    WHERE m.OrganizationId = @OrganizationId AND tg.UserId = @UserId
                    ORDER BY g.Name, m.UploadDate DESC", connection))
                {
                    command.Parameters.AddWithValue("@OrganizationId", _teacher.OrganizationId);
                    command.Parameters.AddWithValue("@UserId", _teacher.Id);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int groupId = reader.GetInt32(1);
                            if (!materialsByGroup.ContainsKey(groupId))
                                materialsByGroup[groupId] = new List<Material>();

                            materialsByGroup[groupId].Add(new Material
                            {
                                Id = reader.GetInt32(0),
                                GroupId = groupId,
                                OrganizationId = reader.GetInt32(2),
                                Title = reader.GetString(3),
                                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                                FilePath = reader.GetString(5),
                                FileTypeId = reader.GetInt32(6),
                                FileType = reader.GetString(7),
                                IsRequired = !reader.IsDBNull(8) && reader.GetBoolean(8),
                                UploadDate = reader.GetDateTime(9),
                                UploadedByUserId = reader.GetInt32(10),
                                GroupName = reader.GetString(11),
                                UploadedByUserName = reader.IsDBNull(12) ? "—" : reader.GetString(12)
                            });
                        }
                    }
                }

                var teacherGroups = LoadTeacherGroups(connection);
                MaterialsTreeView.ItemsSource = teacherGroups.Select(g => new GroupWithMaterials
                {
                    Id = g.Id,
                    Name = g.Name,
                    Materials = materialsByGroup.ContainsKey(g.Id)
                        ? materialsByGroup[g.Id]
                        : new List<Material>()
                }).ToList();
            }
        }

        private void LoadMaterialsTreeLegacy()
        {
            var materialsByGroup = new Dictionary<int, List<Material>>();

            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
                    SELECT m.Id, m.GroupId, m.Title, m.Description, m.FilePath,
                           ISNULL(NULLIF(LTRIM(RTRIM(ft.Extension)), ''), ft.Name),
                           m.UploadDate, m.UploadedByUserId, g.Name
                    FROM dbo.[Material] m
                    INNER JOIN dbo.[Group] g ON m.GroupId = g.Id
                    INNER JOIN dbo.FileTypeDict ft ON m.FileTypeId = ft.Id
                    INNER JOIN dbo.[TeacherGroup] tg ON m.GroupId = tg.GroupId
                    WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId AND m.OrganizationId = @OrgId
                    ORDER BY g.Name, m.UploadDate DESC", connection))
                {
                    command.Parameters.AddWithValue("@UserId", _teacher.Id);
                    command.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int groupId = reader.GetInt32(1);
                            if (!materialsByGroup.ContainsKey(groupId))
                                materialsByGroup[groupId] = new List<Material>();

                            materialsByGroup[groupId].Add(new Material
                            {
                                Id = reader.GetInt32(0),
                                GroupId = groupId,
                                OrganizationId = _teacher.OrganizationId,
                                Title = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                                FilePath = reader.GetString(4),
                                FileType = reader.GetString(5),
                                IsRequired = true,
                                UploadDate = reader.GetDateTime(6),
                                UploadedByUserId = reader.GetInt32(7),
                                GroupName = reader.GetString(8),
                                UploadedByUserName = "—"
                            });
                        }
                    }
                }

                var teacherGroups = LoadTeacherGroups(connection);
                MaterialsTreeView.ItemsSource = teacherGroups.Select(g => new GroupWithMaterials
                {
                    Id = g.Id,
                    Name = g.Name,
                    Materials = materialsByGroup.ContainsKey(g.Id)
                        ? materialsByGroup[g.Id]
                        : new List<Material>()
                }).ToList();
            }

            MessageBox.Show(
                "В таблице Material не хватает столбцов (IsRequired и др.). Выполните SchemaExtensions.sql.",
                "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private List<Group> LoadTeacherGroups(SqlConnection connection)
        {
            var groups = new List<Group>();
            using (var cmd = new SqlCommand(@"
                SELECT g.Id, g.Name
                FROM dbo.[Group] g
                INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId
                    AND tg.OrganizationId = g.OrganizationId
                WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                  AND (g.IsActive = 1 OR g.IsActive IS NULL)
                ORDER BY g.Name", connection))
            {
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        groups.Add(new Group
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.IsDBNull(1) ? ("Группа #" + reader.GetInt32(0)) : reader.GetString(1)
                        });
                    }
                }
            }

            return groups;
        }

        private void MaterialsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (MaterialsTreeView.SelectedItem is Material material)
            {
                _selectedMaterial = material;
                ViewButton.IsEnabled = true;

                string requiredText = material.IsRequired ? "Да" : "Нет";
                MaterialTitleTextBlock.Text = material.Title;
                MaterialInfoTextBlock.Text =
                    "№ " + material.Id + " · Организация: " + material.OrganizationId + "\n" +
                    "Группа: " + material.GroupName + "\n" +
                    "Тип файла: " + material.FileType + "\n" +
                    "Обязательный: " + requiredText + "\n" +
                    "Загружено: " + material.UploadDate.ToString("dd.MM.yyyy HH:mm",
                        CultureInfo.GetCultureInfo("ru-RU")) + "\n" +
                    "Загрузил: " + material.UploadedByUserName;

                MaterialDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(material.Description)
                    ? "Описание отсутствует"
                    : material.Description;

                ClearPreview();
            }
            else
            {
                _selectedMaterial = null;
                ViewButton.IsEnabled = false;
                MaterialTitleTextBlock.Text = "Выберите материал из списка";
                MaterialInfoTextBlock.Text = "";
                MaterialDescriptionTextBlock.Text = "";
                ClearPreview();
            }
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMaterial == null) return;

            if (!TeacherCanAccessMaterial(_selectedMaterial.Id))
            {
                MessageBox.Show("Материал недоступен: он не относится к вашим группам.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!File.Exists(_selectedMaterial.FilePath))
                {
                    MessageBox.Show("Файл не найден по указанному пути.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fileType = (_selectedMaterial.FileType ?? "").ToUpperInvariant();

                ImagePreview.Visibility = Visibility.Collapsed;
                VideoPreview.Visibility = Visibility.Collapsed;
                DocumentPreview.Visibility = Visibility.Collapsed;
                NoPreviewTextBlock.Visibility = Visibility.Collapsed;

                if (fileType.Contains("JPG") || fileType.Contains("JPEG") || fileType.Contains("PNG"))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_selectedMaterial.FilePath);
                    bitmap.EndInit();
                    ImagePreview.Source = bitmap;
                    ImagePreview.Visibility = Visibility.Visible;
                }
                else if (fileType.Contains("MP4"))
                {
                    VideoPreview.Source = new Uri(_selectedMaterial.FilePath);
                    VideoPreview.Visibility = Visibility.Visible;
                    VideoPreview.Play();
                }
                else if (fileType.Contains("PDF"))
                {
                    DocumentPreview.Navigate(_selectedMaterial.FilePath);
                    DocumentPreview.Visibility = Visibility.Visible;
                }
                else if (fileType.Contains("DOCX") || fileType.Contains("DOC") ||
                         fileType.Contains("PPTX") || fileType.Contains("PPT"))
                {
                    MessageBox.Show(
                        "Файл откроется во внешнем приложении.\n\n" + _selectedMaterial.FilePath,
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(_selectedMaterial.FilePath);
                }
                else
                {
                    System.Diagnostics.Process.Start(_selectedMaterial.FilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при открытии: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TeacherCanAccessMaterial(int materialId)
        {
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT 1
                    FROM dbo.[Material] m
                    INNER JOIN dbo.[TeacherGroup] tg ON m.GroupId = tg.GroupId
                        AND tg.OrganizationId = m.OrganizationId
                    WHERE m.Id = @MaterialId
                      AND m.OrganizationId = @OrgId
                      AND tg.UserId = @UserId", connection))
                {
                    cmd.Parameters.AddWithValue("@MaterialId", materialId);
                    cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                    cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        private void ClearPreview()
        {
            ImagePreview.Visibility = Visibility.Collapsed;
            VideoPreview.Visibility = Visibility.Collapsed;
            VideoPreview.Stop();
            DocumentPreview.Visibility = Visibility.Collapsed;
            NoPreviewTextBlock.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = new TeacherDashboardView(_teacher);
            dashboard.Show();
            Close();
        }

        public class GroupWithMaterials
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public List<Material> Materials { get; set; }
        }
    }
}
