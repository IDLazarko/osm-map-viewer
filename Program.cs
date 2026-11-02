using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program {
    static void Main() {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        string currentDir = Directory.GetCurrentDirectory();
        const string port = "8000";
        const string baseUrl = "http://localhost:" + port + "/";

        while (true) {
            Console.Clear();
            Console.WriteLine("=======================================================");
            Console.WriteLine("         ВЫБЕРИТЕ КАРТУ ДЛЯ ПРОСМОТРА");
            Console.WriteLine("=======================================================");
            Console.WriteLine();

            var htmlFiles = Directory.GetFiles(currentDir, "*.html")
                                     .Select(Path.GetFileName)
                                     .ToList();

            if (htmlFiles.Count == 0) {
                Console.WriteLine("[ Ошибка ] В этой папке нет файлов с расширением .html");
                Console.WriteLine("Положите файлы карт в эту же папку и нажмите любую клавишу...");
                Console.ReadKey();
                continue;
            }

            for (int i = 0; i < htmlFiles.Count; i++) {
                Console.WriteLine(" [" + (i + 1) + "] " + htmlFiles[i]);
            }

            Console.WriteLine();
            Console.WriteLine("=======================================================");
            Console.Write("Введите номер карты (или 0 для выхода): ");
            string input = Console.ReadLine();

            if (input == "0" || string.IsNullOrWhiteSpace(input)) return;

            if (!int.TryParse(input, out int choice) || choice < 1 || choice > htmlFiles.Count) {
                Console.WriteLine("[ Ошибка ] Неверный номер! Попробуйте еще раз.");
                Thread.Sleep(2000);
                continue;
            }

            string selectedFile = htmlFiles[choice - 1];
            Console.Clear();
            Console.WriteLine("=======================================================");
            Console.WriteLine("  СЕРВЕР ЗАПУЩЕН И ОТДАЕТ КАРТУ");
            Console.WriteLine("=======================================================");
            Console.WriteLine("  Выбранный файл: " + selectedFile);
            Console.WriteLine();
            Console.WriteLine("  [!] Как только вы закроете вкладку карты в браузере,");
            Console.WriteLine("      это окно закроется АВТОМАТИЧЕСКИ через 2 секунды.");

            string targetUrl = baseUrl + Uri.EscapeDataString(selectedFile);
            
            // --- ГАРАНТИРОВАННЫЙ ЗАПУСК БРАУЗЕРА ИЗ ГЛАВНОГО ПОТОКА ---
            try {
                Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
            } catch {
                Console.WriteLine("[ Внимание ] Откройте ссылку вручную: " + targetUrl);
            }

            // Запускаем сервер, который сам закроет консоль после закрытия вкладки
            StartServerAndWatch(currentDir, baseUrl, selectedFile);
            
            return; 
        }
    }

    static void StartServerAndWatch(string rootDir, string url, string fileName) {
        using (var listener = new HttpListener()) {
            listener.Prefixes.Add(url);
            try { listener.Start(); } catch { return; }

            bool fileWasServed = false;

            while (listener.IsListening) {
                var contextTask = listener.GetContextAsync();
                
                // Проверяем запросы. Если браузер закрыт, ожидания (keep-alive) прекратятся
                if (!contextTask.Wait(2000)) {
                    // Если карта уже была успешно отдана, и за 2 секунды новых запросов нет — вкладка закрыта
                    if (fileWasServed) break; 
                    continue;
                }

                var context = contextTask.Result;
                var request = context.Request;
                using (var response = context.Response) {
                    string rawPath = Uri.UnescapeDataString(request.Url?.LocalPath ?? "");
                    string filePath = Path.Combine(rootDir, rawPath.TrimStart('/'));

                    if (File.Exists(filePath)) {
                        try {
                            byte[] bytes = File.ReadAllBytes(filePath);
                            response.ContentType = "text/html";
                            response.ContentLength64 = bytes.Length;
                            response.OutputStream.Write(bytes, 0, bytes.Length);
                            
                            if (rawPath.TrimStart('/') == fileName) {
                                fileWasServed = true; 
                            }
                        } catch { break; }
                    } else {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                }
            }
            listener.Close();
        }
    }
}
