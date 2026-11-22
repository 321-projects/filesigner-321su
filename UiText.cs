using System.Collections.Generic;

namespace FileSigner321
{
    internal static class UiText
    {
        internal static bool English { get; set; }

        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
        {
            { "Создать сертификат", "Create certificate" },
            { "Экспорт .cer", "Export .cer" },
            { "Резервная копия .pfx", "Back up .pfx" },
            { "Добавить файлы…", "Add files…" },
            { "Убрать выбранные", "Remove selected" },
            { "Подписать копии", "Sign copies" },
            { "О программе", "About" },
            { "Сертификат подписи", "Signing certificate" },
            { "Файл", "File" },
            { "Папка", "Folder" },
            { "Результат", "Result" },
            { "Перетащи файлы в список или нажми «Добавить файлы».", "Drop files here or click “Add files”." },
            { "Отпечаток: ", "Thumbprint: " },
            { "Готов к подписи", "Ready to sign" },
            { "Тип файла не поддерживается", "Unsupported file type" },
            { "Подпись…", "Signing…" },
            { "Подпись: ", "Signing: " },
            { "Ошибка: ", "Error: " },
            { "Подписано: {0}. Ошибок: {1}.", "Signed: {0}. Failed: {1}." },
            { "Сертификат создан в личном хранилище. Автоматическое доверие не добавлено.", "Certificate created in the Personal store. Publisher trust is configured separately." },
            { "Публичный сертификат сохранён: ", "Public certificate saved: " },
            { "Резервная копия .pfx сохранена: ", "PFX backup saved: " },
            { "Разработчик: 321.su", "Developer: 321.su" },
            { "Создать сертификат подписи", "Create signing certificate" },
            { "Имя сертификата", "Certificate name" },
            { "Срок действия, дней", "Validity in days" },
            { "Самоподписанный сертификат в личном хранилище.", "Self-signed certificate in the Personal store." },
            { "Отмена", "Cancel" },
            { "Создать", "Create" },
            { "Введи имя из букв, цифр, пробелов или знаков . _ -.", "Use letters, digits, spaces, or the characters . _ -." },
            { "Пароль резервной копии .pfx", "PFX backup password" },
            { "Пароль для файла .pfx", "PFX password" },
            { "Повтори пароль", "Confirm password" },
            { "Сохранить .pfx", "Save .pfx" },
            { "Введи один и тот же непустой пароль дважды.", "Enter the same non-empty password twice." },
            { "Подпись проверена", "Signature verified" },
            { "Подписан, доверие не подтверждено (0x{0:X8})", "Signed, publisher trust unconfirmed (0x{0:X8})" },
            { "Подписан через rdpsign", "Signed with rdpsign" },
            { "Текущий пользователь", "Current user" },
            { "Компьютер", "Local machine" },
            { "{0}  |  до {1:dd.MM.yyyy}  |  {2}", "{0}  |  expires {1:dd.MM.yyyy}  |  {2}" },
            { "Публичный сертификат (*.cer)|*.cer", "Public certificate (*.cer)|*.cer" },
            { "Резервная копия сертификата (*.pfx)|*.pfx", "Certificate backup (*.pfx)|*.pfx" },
            { "Поддерживаемые файлы|*.ps1;*.psm1;*.psd1;*.exe;*.dll;*.msi;*.cab;*.cat;*.rdp|Все файлы|*.*", "Supported files|*.ps1;*.psm1;*.psd1;*.exe;*.dll;*.msi;*.cab;*.cat;*.rdp|All files|*.*" },
        };

        internal static string Get(string russian)
        {
            string english;
            return English && Translations.TryGetValue(russian, out english) ? english : russian;
        }
    }
}
