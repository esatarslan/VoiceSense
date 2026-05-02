using Microsoft.Win32;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace VoiceSense.UI
{
    public partial class MainWindow : Window
    {
        private string? _selectedVoicePath;
        private string? _generatedAudioPath;
        private MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly HttpClient _httpClient = new HttpClient();
        
        // Python API'nin çalışacağı varsayılan adres
        private const string ApiUrl = "http://127.0.0.1:8000/clone-voice";

        public MainWindow()
        {
            InitializeComponent();
            _httpClient.Timeout = TimeSpan.FromMinutes(60);
        }

        private void BtnSelectVoice_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3",
                Title = "Select Reference Audio File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedVoicePath = openFileDialog.FileName;
                TxtSelectedVoice.Text = $"Selected: {Path.GetFileName(_selectedVoicePath)}";
                TxtSelectedVoice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedVoicePath))
            {
                MessageBox.Show("Please select an audio file first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtInputText.Text))
            {
                MessageBox.Show("Please enter the text to generate.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGenerate.IsEnabled = false;
            ProgressBarStatus.IsIndeterminate = true;
            TxtStatus.Text = "Generating voice... (This may take a while depending on your hardware)";

            try
            {
                using var form = new MultipartFormDataContent();
                
                // Metin
                form.Add(new StringContent(TxtInputText.Text), "text");
                form.Add(new StringContent(TxtRefText.Text), "ref_text");
                
                // Ses Dosyası
                var fileBytes = await File.ReadAllBytesAsync(_selectedVoicePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                form.Add(fileContent, "audio_file", Path.GetFileName(_selectedVoicePath));

                // API İsteği
                var response = await _httpClient.PostAsync(ApiUrl, form);

                if (response.IsSuccessStatusCode)
                {
                    var responseBytes = await response.Content.ReadAsByteArrayAsync();
                    
                    // Geçici klasöre kaydet
                    _generatedAudioPath = Path.Combine(Path.GetTempPath(), $"voicesense_{Guid.NewGuid()}.wav");
                    await File.WriteAllBytesAsync(_generatedAudioPath, responseBytes);

                    TxtStatus.Text = "Voice generated successfully!";
                    BtnPlay.IsEnabled = true;
                    BtnSave.IsEnabled = true;
                }
                else
                {
                    // API çalışmıyorsa simülasyon için hata göster, ama kullanıcıyı yönlendir.
                    TxtStatus.Text = "Error: Background AI service (Python API) might not be running.";
                    MessageBox.Show($"Server Error: {response.StatusCode}\nPlease ensure the Python API server is running in the background.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException)
            {
                TxtStatus.Text = "Could not connect to API. Did you start the Python server?";
                MessageBox.Show("Could not connect to the background AI service (Python API).\n\nPlease ensure the Python script (api.py) is running.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "An unexpected error occurred.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGenerate.IsEnabled = true;
                ProgressBarStatus.IsIndeterminate = false;
                ProgressBarStatus.Value = 100;
            }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_generatedAudioPath) && File.Exists(_generatedAudioPath))
            {
                _mediaPlayer.Open(new Uri(_generatedAudioPath));
                _mediaPlayer.Play();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_generatedAudioPath) || !File.Exists(_generatedAudioPath)) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "WAV File|*.wav",
                Title = "Save Generated Audio",
                FileName = "GeneratedAudio.wav"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.Copy(_generatedAudioPath, saveFileDialog.FileName, true);
                MessageBox.Show("File saved successfully!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}