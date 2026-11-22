# Выпуск Windows-версии

## Сборка и проверка

Собери Release через `publish.bat`. Открой `publish\FileSigner321.exe`, проверь переключение **RU → EN → RU**, подпись тестового файла и экспорт PFX с паролем. Проверь сборку на Windows с .NET Framework 4.8.

## Подпись публичного EXE

Для GitHub Releases подпиши окончательный EXE сертификатом публично доверенного издателя или через Azure Artifact Signing. Используй постоянную удостоверенную учётную запись издателя для всех релизов. Для сертификата, видимого в личном хранилище текущего пользователя, пример с SignTool из Windows SDK выглядит так. Укажи отпечаток своего публично доверенного сертификата и RFC 3161 URL сервера отметок времени от поставщика подписи.

```bat
signtool sign /sha1 CERTIFICATE_THUMBPRINT /fd SHA256 /tr RFC3161_TIMESTAMP_URL /td SHA256 "publish\FileSigner321.exe"
signtool verify /pa /v "publish\FileSigner321.exe"
```

После успешной проверки вычисли SHA-256 подписанного файла.

```powershell
Get-FileHash -Algorithm SHA256 .\publish\FileSigner321.exe
```

В GitHub создай релиз с тегом `v1.2.0`, приложи подписанный EXE и опубликуй контрольную сумму в описании релиза. PFX, закрытый ключ и пароль храни отдельно от репозитория.

## SmartScreen

На GitHub предупреждение о новом EXE может появляться даже с действительной публичной подписью. SmartScreen учитывает репутацию загрузки и издателя. Самоподписанный сертификат не формирует публичного доверия. Сертификат EV тоже не даёт автоматического исключения. Для распространения через Microsoft Store можно подготовить MSIX и отправить его на сертификацию. Store подписывает такой пакет при публикации.

[SmartScreen для разработчиков](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation) · [Варианты подписи Windows](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options) · [SignTool](https://learn.microsoft.com/windows/win32/seccrypto/signtool)
