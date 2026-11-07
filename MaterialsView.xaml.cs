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
using Microsoft.Win32;

namespace Lesson_Link
{
    public partial class MaterialsView : UserControl
    {
        private readonly User _admin;
        private Material selectedMaterial;
        private string selectedFilePath;

        public MaterialsView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            OrganizationIdDisplay.Text = _admin.OrganizationId.ToString(CultureInfo.InvariantCulture);

            try
            {
                LoadFileTypes();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить типы файлов: " + ex.Message, "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            LoadGroups();
            LoadMaterialsTree();
        }

        private void LoadFileTypes()
        {
            var types = new List<FileTypeItem>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(
                    "SELECT Id, Name, ISNULL(Extension, '') FROM dbo.FileTypeDict ORDER BY Name", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string ext = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string name = reader.IsDBNull(1) ? ("#" + reader.GetInt32(0)) : reader.GetString(1);
                        types.Add(new FileTypeItem
                        {
                            Id = reader.GetInt32(0),
                            Name = string.IsNullOrEmpty(ext) ? name : name + " (." + ext.TrimStart('.') + ")"
                        });
                    }
                }
            }

            FileTypeComboBox.ItemsSource = types;
            if (types.Count > 0)
                FileTypeComboBox.SelectedIndex = 0;
        }

        private void LoadGroups()
        {
            try
            {
                var groups = new List<Group>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT g.Id, g.Name
                        FROM dbo.[Group] g
                        WHERE g.OrganizationId = @OrganizationId
                          AND (g.IsActive = 1 OR g.IsActive IS NULL)
                        ORDER BY g.Name", connection))
                    {
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        using (var reader = command.ExecuteReader())
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
                }

                GroupComboBox.ItemsSource = groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки групп: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMaterialsTree()
        {
            try
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
                        LEFT JOIN dbo.[User] u ON m.UploadedByUserId = u.Id
                        WHERE m.OrganizationId = @OrganizationId
                        ORDER BY g.Name, m.UploadDate DESC", connection))
                    {
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
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
                }

                var allGroups = new List<Group>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Id, Name FROM dbo.[Group] WHERE OrganizationId = @OrganizationId ORDER BY Name", connection))
                    {
                        cmd.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allGroups.Add(new Group
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                }

                var groupsWithMaterials = allGroups.Select(g => new GroupWithMaterials
                {
                    Id = g.Id,
                    Name = g.Name,
                    Materials = materialsByGroup.ContainsKey(g.Id) ? materialsByGroup[g.Id] : new List<Material>()
                }).ToList();

                MaterialsTreeView.ItemsSource = groupsWithMaterials;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки материалов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int? GetSelectedFileTypeId()
        {
            if (FileTypeComboBox.SelectedValue is int id)
                return id;
            return null;
        }

        private void SelectFileTypeByExtension(string extension)
        {
            if (FileTypeComboBox.ItemsSource == null || string.IsNullOrWhiteSpace(extension))
                return;

            string ext = extension.Trim().TrimStart('.').ToUpperInvariant();
            foreach (FileTypeItem item in FileTypeComboBox.Items)
            {
                if (item.Name != null && item.Name.ToUpperInvariant().Contains(ext))
                {
                    FileTypeComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Все поддерживаемые|*.pdf;*.docx;*.pptx;*.mp4;*.jpg;*.jpeg;*.png|PDF|*.pdf|Word|*.docx|PowerPoint|*.pptx|Видео|*.mp4|Изображения|*.jpg;*.jpeg;*.png",
                Title = "Выберите файл"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            selectedFilePath = openFileDialog.FileName;
            SelectedFileTextBlock.Text = "Выбран: " + Path.GetFileName(selectedFilePath);

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
                TitleTextBox.Text = Path.GetFileNameWithoutExtension(selectedFilePath);

            SelectFileTypeByExtension(Path.GetExtension(selectedFilePath));
        }

        private bool ValidateMaterialForm(bool requireFile, bool requireGroup)
        {
            if (requireGroup && GroupComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Укажите название материала.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!GetSelectedFileTypeId().HasValue)
            {
                MessageBox.Show("Выберите тип файла.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (requireFile && string.IsNullOrWhiteSpace(selectedFilePath))
            {
                MessageBox.Show("Выберите файл для загрузки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private string CopyFileToStorage(int groupId, string sourcePath)
        {
            string materialsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Materials");
            if (!Directory.Exists(materialsFolder))
                Directory.CreateDirectory(materialsFolder);

            string groupFolder = Path.Combine(materialsFolder, "Org_" + _admin.OrganizationId, "Group_" + groupId);
            if (!Directory.Exists(groupFolder))
                Directory.CreateDirectory(groupFolder);

            string fileName = Path.GetFileName(sourcePath);
            string uniqueFileName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + fileName;
            string destinationPath = Path.Combine(groupFolder, uniqueFileName);
            File.Copy(sourcePath, destinationPath, true);
            return destinationPath;
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateMaterialForm(requireFile: true, requireGroup: true))
                return;

            try
            {
                int groupId = (int)GroupComboBox.SelectedValue;
                string destinationPath = CopyFileToStorage(groupId, selectedFilePath);
                int fileTypeId = GetSelectedFileTypeId().Value;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        INSERT INTO dbo.[Material] (
                            GroupId, OrganizationId, Title, Description, FilePath, FileTypeId,
                            IsRequired, UploadDate, UploadedByUserId)
                        VALUES (
                            @GroupId, @OrganizationId, @Title, @Description, @FilePath, @FileTypeId,
                            @IsRequired, SYSUTCDATETIME(), @UploadedByUserId)", connection))
                    {
                        command.Parameters.AddWithValue("@GroupId", groupId);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        command.Parameters.AddWithValue("@Title", TitleTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@Description",
                            string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? (object)DBNull.Value : DescriptionTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@FilePath", destinationPath);
                        command.Parameters.AddWithValue("@FileTypeId", fileTypeId);
                        command.Parameters.AddWithValue("@IsRequired", IsRequiredCheckBox.IsChecked == true);
                        command.Parameters.AddWithValue("@UploadedByUserId", _admin.Id);
                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Материал загружен.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadMaterialsTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterial == null)
            {
                MessageBox.Show("Выберите материал в дереве справа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateMaterialForm(requireFile: false, requireGroup: true))
                return;

            try
            {
                int groupId = (int)GroupComboBox.SelectedValue;
                int fileTypeId = GetSelectedFileTypeId().Value;
                string filePath = selectedMaterial.FilePath;

                if (!string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    if (File.Exists(selectedMaterial.FilePath))
                    {
                        try { File.Delete(selectedMaterial.FilePath); }
                        catch { /* старый файл мог быть удалён вручную */ }
                    }

                    filePath = CopyFileToStorage(groupId, selectedFilePath);
                }

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        UPDATE dbo.[Material]
                        SET GroupId = @GroupId,
                            Title = @Title,
                            Description = @Description,
                            FilePath = @FilePath,
                            FileTypeId = @FileTypeId,
                            IsRequired = @IsRequired
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection))
                    {
                        command.Parameters.AddWithValue("@Id", selectedMaterial.Id);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        command.Parameters.AddWithValue("@GroupId", groupId);
                        command.Parameters.AddWithValue("@Title", TitleTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@Description",
                            string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? (object)DBNull.Value : DescriptionTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@FilePath", filePath);
                        command.Parameters.AddWithValue("@FileTypeId", fileTypeId);
                        command.Parameters.AddWithValue("@IsRequired", IsRequiredCheckBox.IsChecked == true);

                        int n = command.ExecuteNonQuery();
                        if (n == 0)
                        {
                            MessageBox.Show("Материал не найден или принадлежит другой организации.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                MessageBox.Show("Материал обновлён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadMaterialsTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearFormButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            selectedMaterial = null;
            selectedFilePath = null;
            MaterialIdDisplay.Text = "";
            TitleTextBox.Text = "";
            DescriptionTextBox.Text = "";
            GroupComboBox.SelectedItem = null;
            if (FileTypeComboBox.Items.Count > 0)
                FileTypeComboBox.SelectedIndex = 0;
            IsRequiredCheckBox.IsChecked = true;
            SelectedFileTextBlock.Text = "Файл не выбран";
            ViewButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            ClearTreeViewSelection(MaterialsTreeView);
            MaterialTitleTextBlock.Text = "Выберите материал из списка";
            MaterialInfoTextBlock.Text = "";
            MaterialDescriptionTextBlock.Text = "";
            ClearPreview();
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterial == null) return;

            try
            {
                if (!File.Exists(selectedMaterial.FilePath))
                {
                    MessageBox.Show("Файл не найден по указанному пути.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fileType = (selectedMaterial.FileType ?? "").ToUpperInvariant();

                ImagePreview.Visibility = Visibility.Collapsed;
                VideoPreview.Visibility = Visibility.Collapsed;
                DocumentPreview.Visibility = Visibility.Collapsed;
                NoPreviewTextBlock.Visibility = Visibility.Collapsed;

                if (fileType == "JPG" || fileType == "JPEG" || fileType == "PNG")
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selectedMaterial.FilePath);
                    bitmap.EndInit();
                    ImagePreview.Source = bitmap;
                    ImagePreview.Visibility = Visibility.Visible;
                }
                else if (fileType == "MP4")
                {
                    VideoPreview.Source = new Uri(selectedMaterial.FilePath);
                    VideoPreview.Visibility = Visibility.Visible;
                    VideoPreview.Play();
                }
                else if (fileType == "PDF")
                {
                    DocumentPreview.Navigate(selectedMaterial.FilePath);
                    DocumentPreview.Visibility = Visibility.Visible;
                }
                else if (fileType == "DOCX" || fileType == "PPTX")
                {
                    MessageBox.Show(
                        "Файлы " + fileType + " открываются во внешнем приложении.\n\n" + selectedMaterial.FilePath,
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(selectedMaterial.FilePath);
                }
                else
                {
                    MessageBox.Show("Просмотр типа «" + fileType + "» не поддерживается.", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при открытии: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterial == null)
            {
                MessageBox.Show("Выберите материал для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Удалить этот материал?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                if (File.Exists(selectedMaterial.FilePath))
                    File.Delete(selectedMaterial.FilePath);

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(
                        "DELETE FROM dbo.[Material] WHERE Id = @Id AND OrganizationId = @OrganizationId", connection))
                    {
                        command.Parameters.AddWithValue("@Id", selectedMaterial.Id);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        int n = command.ExecuteNonQuery();
                        if (n == 0)
                        {
                            MessageBox.Show("Материал не найден.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                MessageBox.Show("Материал удалён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadMaterialsTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при удалении: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FillFormFromMaterial(Material material)
        {
            MaterialIdDisplay.Text = material.Id.ToString(CultureInfo.InvariantCulture);
            TitleTextBox.Text = material.Title ?? "";
            DescriptionTextBox.Text = material.Description ?? "";
            IsRequiredCheckBox.IsChecked = material.IsRequired;
            SelectedFileTextBlock.Text = "Текущий файл: " + Path.GetFileName(material.FilePath);
            selectedFilePath = null;

            foreach (Group g in GroupComboBox.Items)
            {
                if (g.Id == material.GroupId)
                {
                    GroupComboBox.SelectedItem = g;
                    break;
                }
            }

            foreach (FileTypeItem ft in FileTypeComboBox.Items)
            {
                if (ft.Id == material.FileTypeId)
                {
                    FileTypeComboBox.SelectedItem = ft;
                    break;
                }
            }
        }

        private void MaterialsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (MaterialsTreeView.SelectedItem is Material material)
            {
                selectedMaterial = material;
                ViewButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
                FillFormFromMaterial(material);

                string requiredText = material.IsRequired ? "Да" : "Нет";
                MaterialTitleTextBlock.Text = material.Title;
                MaterialInfoTextBlock.Text =
                    "Группа: " + material.GroupName + "\n" +
                    "Тип: " + material.FileType + "\n" +
                    "Обязательный: " + requiredText + "\n" +
                    "Загружено: " + material.UploadDate.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU")) + "\n" +
                    "Загрузил: " + material.UploadedByUserName;
                MaterialDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(material.Description)
                    ? "Описание отсутствует"
                    : material.Description;
                ClearPreview();
            }
            else
            {
                selectedMaterial = null;
                ViewButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
                MaterialTitleTextBlock.Text = "Выберите материал из списка";
                MaterialInfoTextBlock.Text = "";
                MaterialDescriptionTextBlock.Text = "";
                ClearPreview();
            }
        }

        private static void ClearTreeViewSelection(TreeView treeView)
        {
            if (treeView == null) return;
            for (int i = 0; i < treeView.Items.Count; i++)
            {
                var item = treeView.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (item != null)
                    UnselectTreeViewItem(item);
            }
        }

        private static void UnselectTreeViewItem(TreeViewItem item)
        {
            item.IsSelected = false;
            for (int i = 0; i < item.Items.Count; i++)
            {
                var child = item.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child != null)
                    UnselectTreeViewItem(child);
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
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        public class GroupWithMaterials
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public List<Material> Materials { get; set; }
        }

        private sealed class FileTypeItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
