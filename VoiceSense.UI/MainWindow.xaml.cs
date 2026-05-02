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
                Title = "Referans Ses Dosyanızı Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedVoicePath = openFileDialog.FileName;
                TxtSelectedVoice.Text = $"Seçildi: {Path.GetFileName(_selectedVoicePath)}";
                TxtSelectedVoice.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedVoicePath))
            {
                MessageBox.Show("Lütfen önce bir ses dosyası seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtInputText.Text))
            {
                MessageBox.Show("Lütfen okunacak metni girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGenerate.IsEnabled = false;
            ProgressBarStatus.IsIndeterminate = true;
            TxtStatus.Text = "Ses üretiliyor... (Bu işlem bilgisayarınızın hızına bağlı olarak biraz sürebilir)";

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

                    TxtStatus.Text = "Ses başarıyla üretildi!";
                    BtnPlay.IsEnabled = true;
                    BtnSave.IsEnabled = true;
                }
                else
                {
                    // API çalışmıyorsa simülasyon için hata göster, ama kullanıcıyı yönlendir.
                    TxtStatus.Text = "Hata: Arka plan yapay zeka servisi (Python API) çalışmıyor olabilir.";
                    MessageBox.Show($"Sunucu Hatası: {response.StatusCode}\nLütfen Python API sunucusunun arka planda çalıştığından emin olun.", "Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException)
            {
                TxtStatus.Text = "API'ye bağlanılamadı. Python sunucusunu başlattınız mı?";
                MessageBox.Show("Arka plan yapay zeka servisine (Python API) bağlanılamadı.\n\nLütfen Python scriptinin (api.py) çalışır durumda olduğundan emin olun.", "Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Beklenmeyen bir hata oluştu.";
                MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                Title = "Üretilen Sesi Kaydet",
                FileName = "UretilenSes.wav"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.Copy(_generatedAudioPath, saveFileDialog.FileName, true);
                MessageBox.Show("Dosya başarıyla kaydedildi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}