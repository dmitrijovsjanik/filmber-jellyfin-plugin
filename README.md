# Filmber Sync for Jellyfin

Серверный Jellyfin-плагин для односторонней синхронизации прогресса и статуса
просмотра в Filmber. Плагин получает server-side события, сопоставляет тайтлы
по точным provider IDs, сохраняет операции в локальной outbox и отправляет их
исходящими запросами в Filmber.

Каждый Jellyfin user подключается к своему Filmber account одноразовым кодом.
Токен Filmber хранится в XML-конфигурации плагина, не возвращается странице
Dashboard и не попадает в логи.

## Проверенная версия

- Jellyfin Server: `10.11.8`
- Jellyfin packages: `10.11.8`
- target ABI: `10.11.0.0`
- .NET target: `net9.0`

Источник версий для сборки — `Directory.Build.props`. Реальная загрузка,
pairing и синхронизация фильма и эпизода проверены локально на `10.11.8`.
Совместимость с другой версией Jellyfin не заявляется.

## Команды

```bash
./scripts/dotnet.sh restore FilmberSyncForJellyfin.sln
./scripts/dotnet.sh build FilmberSyncForJellyfin.sln --configuration Release
./scripts/dotnet.sh test FilmberSyncForJellyfin.sln --configuration Release
./scripts/package.sh
docker compose up -d
python3 scripts/mock-filmber-endpoint.py
```

Локальные результаты `./scripts/package.sh` для сервера `10.11.8`:

- `artifacts/jellyfin-10.11.8/repository/filmber-sync_0.1.0.0_jellyfin-10.11.8.zip`
- `artifacts/jellyfin-10.11.8/repository/manifest.json`
- `artifacts/jellyfin-10.11.8/build.yaml`

Артефакт для другой patch-версии собирается отдельно:

```bash
JELLYFIN_VERSION=10.11.10 ./scripts/package.sh
```

Симулятор нужен только для полностью локальной проверки и реализует тот же
минимальный HTTP-контракт pairing/sync. Production Filmber не вызывается.

Локальная установка и end-to-end проверка описаны в
[`docs/local-jellyfin-test.md`](docs/local-jellyfin-test.md), подтверждённый
контракт Filmber — в
[`docs/filmber-api-contract.md`](docs/filmber-api-contract.md).
