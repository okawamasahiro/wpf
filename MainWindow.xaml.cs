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

namespace wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OpenAIClient _client;

        public MainWindow()
        {
            InitializeComponent();

            // ★ APIキーを設定（環境変数がオススメ）
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _client = new OpenAIClient(apiKey);
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
}