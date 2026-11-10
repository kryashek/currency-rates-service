# Currency Rates Service

Тестовое задание на C# / .NET 8: два микросервиса, фоновое получение курсов Банка России, JWT, API Gateway, отдельное приложение миграций и unit-тесты.

## Требования

- .NET SDK 8; для сборки в IDE — Visual Studio 2022 с поддержкой установленного SDK (для 8.0.4xx — версия 17.11 или новее).
- Для полного запуска системы потребуются Docker с Docker Compose; окружение запуска будет добавлено при реализации сервисов.

## Сборка

```powershell
dotnet restore CurrencyRates.sln
dotnet build CurrencyRates.sln --configuration Release --no-restore
```
