using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using System.Text;
using Windows.Graphics.Printing3D;

namespace FilmNyilvantarto
{
    public class Movie
    {
        public string Title { get; set; }
        public int ReleaseYear { get; set; }
    }
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<Movie> AllMovies { get; set; } = new ObservableCollection<Movie>();

        public ObservableCollection<Movie> FilteredMovies { get; set; } = new ObservableCollection<Movie>();


        public MainWindow()
        {
            InitializeComponent();

            this.Title = "FilmNyilvántartó";

            SetWindowIcon();

            MovieListView.ItemsSource = FilteredMovies;

            (this.Content as FrameworkElement).Loaded += MainWindow_Loaded;
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(320, 500));
            }
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SETICON = 0x0080;
        private const IntPtr ICON_SMALL = 0;
        private const IntPtr ICON_BIG = 1;

        private void SetWindowIcon()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/film_icon.ico");

            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);

                IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x00000010 | 0x00008000);
                if (hIcon != IntPtr.Zero)
                {
                    SendMessage(hWnd, WM_SETICON, ICON_SMALL, hIcon);
                    SendMessage(hWnd, WM_SETICON, ICON_BIG, hIcon);
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMoviesFromFile();
            UpdateFilteredList("");
        }
        public async void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            var stackPanel = new StackPanel { Spacing = 10 };
            var titleBox = new TextBox { PlaceholderText = "Cím" };
            var yearBox = new TextBox { PlaceholderText = "Megjelenési év" };
            stackPanel.Children.Add(titleBox);
            stackPanel.Children.Add(yearBox);

            var dialog = new ContentDialog
            {
                Title = "Új film hozzáadása",
                Content = stackPanel,
                PrimaryButtonText = "Hozzáadás",
                CloseButtonText = "Mégse",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newTitle = titleBox.Text.Trim();
                string newYear = yearBox.Text.Trim();

                if (string.IsNullOrEmpty(newTitle) || string.IsNullOrEmpty(newYear)) return;


                bool exists = AllMovies.Any(m => m.Title.Equals(newTitle, StringComparison.OrdinalIgnoreCase) && m.ReleaseYear.ToString() == newYear);


                if (exists)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Hiba",
                        Content = "Ez a film már szerepel a listán",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
                else
                {
                    if (int.TryParse(newYear, out int year))
                    {
                        var newMovie = new Movie
                        {
                            Title = newTitle,
                            ReleaseYear = year
                        };
                        AllMovies.Add(newMovie);
                        await SaveMoviesToFile();
                        UpdateFilteredList(SearchTextBox.Text);
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Hiba",
                            Content = "A megjelenési évnek számnak kell lennie",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFilteredList(SearchTextBox.Text);
        }
        private void UpdateFilteredList(string query)
        {
            FilteredMovies.Clear();
            var results = AllMovies.Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var movie in results)
            {
                FilteredMovies.Add(movie);
            }
        }
        private string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "movies.json");

        private async Task SaveMoviesToFile()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(AllMovies, new JsonSerializerOptions { WriteIndented = true});
                await File.WriteAllTextAsync(filePath, jsonString);
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Hiba",
                    Content = $"Nem sikerült menteni a fájlt: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }

        }
        private async Task LoadMoviesFromFile()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string jsonString = await File.ReadAllTextAsync(filePath);
                    var loadedMovies = JsonSerializer.Deserialize<List<Movie>>(jsonString);
                    
                    if(loadedMovies != null)
                    {
                        AllMovies.Clear();
                        foreach (var movie in loadedMovies)
                        {
                            AllMovies.Add(movie);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Hiba",
                    Content = $"Nem sikerült betölteni a fájlt: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
        private async void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var movieToDelete = button.DataContext as Movie;

            if (movieToDelete != null)
            {
                AllMovies.Remove(movieToDelete);
                await SaveMoviesToFile();
                UpdateFilteredList(SearchTextBox.Text);
            }
        }

        private async void OnDownloadClick(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Excel CSV", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = "Filmlista";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                StringBuilder csvContent = new StringBuilder();
                csvContent.AppendLine("Cím;Megjelenési év");

                foreach (var movie in AllMovies)
                {
                    csvContent.AppendLine($"{movie.Title};{movie.ReleaseYear}");
                }
                await System.IO.File.WriteAllTextAsync(file.Path, csvContent.ToString(), Encoding.UTF8);
            }
        }
        private async void OnUploadClick(object sender,RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".csv");
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var lines = await System.IO.File.ReadAllLinesAsync(file.Path, Encoding.UTF8);
                for (int i = 1;i< lines.Length;i++)
                {
                    var parts = lines[i].Split(';');
                    if(parts.Length >= 2)
                    {
                        string title = parts[0].Trim();
                        if(int.TryParse(parts[1].Trim(), out int year))
                        {
                            bool exists = AllMovies.Any(m => m.Title.Equals(title, StringComparison.OrdinalIgnoreCase) && m.ReleaseYear == year);
                            if(!exists)
                            {
                                AllMovies.Add(new Movie { Title = title, ReleaseYear = year });
                            }
                        }
                    }
                }
            }
            await SaveMoviesToFile();
            UpdateFilteredList(SearchTextBox.Text);
        }
    }
}
