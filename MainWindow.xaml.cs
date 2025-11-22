using OpenAI;
using OpenAI.Chat;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenAI.Images;
using System.IO;

namespace wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenAIClient _client;
        private readonly ChatClient _chatMini;
        private readonly ImageClient _imageClient;

        public MainWindow()
        {
            InitializeComponent();

            // ★ APIキーを設定（環境変数がオススメ）
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _client = new OpenAIClient(apiKey);
            _chatMini = _client.GetChatClient("gpt-4.1-mini");
            _imageClient = _client.GetImageClient("gpt-image-1");
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string userMessage = InputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                MessageBox.Show("メッセージを入力してください");
                return;
            }

            ResponseTextBox.Text = "GPT に問い合わせ中…";

            // 1. 画像リクエスト判定（安いモデル）
            var judge = await _chatMini.CompleteChatAsync(
                $"次の文章は画像生成の依頼ですか？ yes/no で答えてください。\n\n「{userMessage}」"
            );

            bool isImageRequest = judge.Value.Content[0].Text.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
            if(isImageRequest)
            {
                try
                {
                    GeneratedImage.Source = null;
                    var bitmap = await GenerateImageUrlAsync(userMessage);
                    
                    GeneratedImage.Source = bitmap;
                }
                catch(Exception ex)
                {
                    MessageBox.Show("画像生成中にエラー: " + ex.Message);
                }
            }
            else
            {
                try
                {
                    var chat = _client.GetChatClient("gpt-5.1");

                    var response = await chat.CompleteChatAsync(userMessage);

                    string assistantText = response.Value.Content[0].Text;

                    ResponseTextBox.Text = assistantText;
                }
                catch (Exception ex)
                {
                    ResponseTextBox.Text = $"エラー: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// プロンプトから画像URLを1枚生成して返す「自作API」。
        /// </summary>
        public async Task<BitmapImage> GenerateImageUrlAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("プロンプトが空です。", nameof(prompt));

            var options = new ImageGenerationOptions
            {
                Size = GeneratedImageSize.W512xH512,
            };

            // 画像生成API呼び出し
            var response = await _imageClient.GenerateImageAsync(prompt, options);

            // ★ バイナリデータを直接扱う
            var bytes = response?.Value?.ImageBytes;
            if (bytes == null || bytes.IsEmpty)
                throw new Exception("画像データが空です。");

            using var ms = new MemoryStream(bytes.ToArray());
            
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}